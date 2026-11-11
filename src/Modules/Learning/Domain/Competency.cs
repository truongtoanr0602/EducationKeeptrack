namespace EducationKeeptrack.Learning.Domain;

public sealed record MasteryRule(int Threshold, int MinTasks, int RequiredAttempts, int Version);

/// <summary>The confirmed final result of one student for one skill in one assessment.</summary>
public sealed record ConfirmedResult(Guid AssessmentId, string Title, DateOnly Date, string Status, int TaskCount, int? Correct);

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
/// A skill not marked as taught gets no conclusion.
/// </summary>
public static class Competency
{
    public static SkillCompetency Evaluate(bool taught, MasteryRule rule, IEnumerable<ConfirmedResult> results)
    {
        var ordered = results.Where(x => x.Status != SkillResultStatus.Unassigned && x.Status != SkillResultStatus.Unassessed)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.AssessmentId).ToList();
        var valid = ordered.Where(x => IsValid(x, rule)).ToList();
        var counted = valid.Take(rule.RequiredAttempts).ToHashSet();
        var evidence = ordered.Select(x => new EvidenceItem(x.AssessmentId, x.Title, x.Date, x.Status, x.TaskCount, x.Correct,
            counted.Contains(x), Meets(x, rule), counted.Contains(x) ? null : Excluded(x, rule))).ToList();
        if (!taught) return new(CompetencyState.NotTaught, 0, evidence);
        if (counted.Count < rule.RequiredAttempts) return new(CompetencyState.Insufficient, rule.RequiredAttempts - counted.Count, evidence);
        return new(counted.All(x => Meets(x, rule)) ? CompetencyState.Mastered : CompetencyState.NotMastered, 0, evidence);
    }

    private static bool IsValid(ConfirmedResult x, MasteryRule rule) =>
        x.Status == SkillResultStatus.Assessed && x.Correct is not null && x.TaskCount >= rule.MinTasks;
    private static bool Meets(ConfirmedResult x, MasteryRule rule) =>
        x.Status == SkillResultStatus.Assessed && x.Correct is { } correct && correct * 100 >= rule.Threshold * x.TaskCount;
    private static string Excluded(ConfirmedResult x, MasteryRule rule) =>
        x.Status != SkillResultStatus.Assessed ? "Không làm bài nên không tính"
        : x.TaskCount < rule.MinTasks ? $"Dưới {rule.MinTasks} nhiệm vụ nên không tính"
        : "Cũ hơn các lần gần nhất";
}
