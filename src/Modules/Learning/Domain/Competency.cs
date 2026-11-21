namespace EducationKeeptrack.Learning.Domain;

public sealed record MasteryRule(int Threshold, int MinTasks, int RequiredAttempts, int Version);

/// <summary>The confirmed final result of one student for one skill in one assessment; Source says how it was graded.</summary>
public sealed record ConfirmedResult(Guid AssessmentId, string Title, DateOnly Date, string Status, int TaskCount, int? Correct, string Source = ResultSource.Manual);

/// <summary>Where a confirmed skill result comes from: graded by the teacher, or an online exam graded by its answer key.</summary>
public static class ResultSource
{
    public const string Manual = "manual", Exam = "exam";
}

public sealed record EvidenceItem(Guid AssessmentId, string Title, DateOnly Date, string Status, int TaskCount, int? Correct, bool Counted, bool MeetsThreshold, string? Excluded);

public sealed record SkillCompetency(string State, int MissingAttempts, IReadOnlyList<EvidenceItem> Evidence);

public static class CompetencyState
{
    public const string Mastered = "mastered", NotMastered = "not_mastered", Insufficient = "insufficient", NotTaught = "not_taught";
}

/// <summary>
/// Decides mastery of one skill from confirmed results only (decision on #34, 11/11/2026):
/// a result is valid when it was assessed with at least MinTasks tasks; the RequiredAttempts most recent
/// valid results are judged, each on its own against the threshold, never added together. Results that
/// are not valid (too few tasks, not done) are kept as evidence with the reason but take no place among
/// the attempts. Fewer valid attempts than required means insufficient data, never "weak".
/// Online exams (ADR-0007, #82) hold only one or two questions per skill, so their results are pooled instead:
/// the most recent exam answers, up to MinTasks × RequiredAttempts questions, are added together and judged once
/// against the threshold when they reach MinTasks questions. When both kinds conclude, the newer evidence wins.
/// A skill not marked as taught gets no conclusion.
/// </summary>
public static class Competency
{
    public static SkillCompetency Evaluate(bool taught, MasteryRule rule, IEnumerable<ConfirmedResult> results)
    {
        var ordered = results.Where(x => x.Status != SkillResultStatus.Unassigned && x.Status != SkillResultStatus.Unassessed)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.AssessmentId).ToList();
        var manual = ordered.Where(x => x.Source != ResultSource.Exam).ToList();
        var valid = manual.Where(x => IsValid(x, rule)).ToList();
        var counted = valid.Take(rule.RequiredAttempts).ToHashSet();
        var pooled = Pool(ordered.Where(x => x.Source == ResultSource.Exam), rule);
        var evidence = ordered.Select(x => x.Source == ResultSource.Exam
            ? new EvidenceItem(x.AssessmentId, x.Title, x.Date, x.Status, x.TaskCount, x.Correct, pooled.Counted.Contains(x), Meets(x, rule),
                pooled.Counted.Contains(x) ? null : x.Status != SkillResultStatus.Assessed ? "Không làm bài nên không tính" : "Cũ hơn các lần gần nhất")
            : new EvidenceItem(x.AssessmentId, x.Title, x.Date, x.Status, x.TaskCount, x.Correct,
                counted.Contains(x), Meets(x, rule), counted.Contains(x) ? null : Excluded(x, rule))).ToList();
        if (!taught) return new(CompetencyState.NotTaught, 0, evidence);
        string? manualState = counted.Count < rule.RequiredAttempts ? null
            : counted.All(x => Meets(x, rule)) ? CompetencyState.Mastered : CompetencyState.NotMastered;
        var examState = pooled.Tasks < rule.MinTasks ? null
            : pooled.Correct * 100 >= rule.Threshold * pooled.Tasks ? CompetencyState.Mastered : CompetencyState.NotMastered;
        if (manualState is not null && examState is not null)
            return new(Latest(pooled.Counted) >= Latest(counted) ? examState : manualState, 0, evidence);
        if (manualState is not null) return new(manualState, 0, evidence);
        if (examState is not null) return new(examState, 0, evidence);
        return new(CompetencyState.Insufficient, Math.Max(rule.RequiredAttempts - counted.Count, 1), evidence);
    }

    /// <summary>The newest assessed exam results, whole, until the window of MinTasks × RequiredAttempts questions is full.</summary>
    private static (HashSet<ConfirmedResult> Counted, int Tasks, int Correct) Pool(IEnumerable<ConfirmedResult> newestFirst, MasteryRule rule)
    {
        var window = rule.MinTasks * rule.RequiredAttempts;
        var counted = new HashSet<ConfirmedResult>();
        int tasks = 0, correct = 0;
        foreach (var x in newestFirst.Where(x => x.Status == SkillResultStatus.Assessed && x.Correct is not null && x.TaskCount > 0))
        {
            if (tasks >= window) break;
            counted.Add(x); tasks += x.TaskCount; correct += x.Correct!.Value;
        }
        return (counted, tasks, correct);
    }

    private static DateOnly Latest(IEnumerable<ConfirmedResult> items) => items.Select(x => x.Date).DefaultIfEmpty(DateOnly.MinValue).Max();

    private static bool IsValid(ConfirmedResult x, MasteryRule rule) =>
        x.Status == SkillResultStatus.Assessed && x.Correct is not null && x.TaskCount >= rule.MinTasks;
    private static bool Meets(ConfirmedResult x, MasteryRule rule) =>
        x.Status == SkillResultStatus.Assessed && x.Correct is { } correct && correct * 100 >= rule.Threshold * x.TaskCount;
    private static string Excluded(ConfirmedResult x, MasteryRule rule) =>
        x.Status != SkillResultStatus.Assessed ? "Không làm bài nên không tính"
        : x.TaskCount < rule.MinTasks ? $"Dưới {rule.MinTasks} nhiệm vụ nên không tính"
        : "Cũ hơn các lần gần nhất";
}
