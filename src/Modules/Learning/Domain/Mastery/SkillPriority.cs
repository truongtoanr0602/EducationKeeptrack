namespace EducationKeeptrack.Learning.Domain;

/// <summary>What the model believes about one student and one skill right now.</summary>
public sealed record SkillBelief(Guid SkillId, double PKnown, int Attempts, DateOnly? LastAttempt, string RuleState);

public sealed record RankedSkill(Guid SkillId, double Score, double PKnown, double Gap, double Uncertainty, double Recency);

/// <summary>
/// Orders the review candidates of one student. The mastery rule stays the authority on "mastered": only skills the
/// rule has not marked mastered (not mastered, or not enough data) are candidates. Among them the score favours
/// a large gap to mastery, high uncertainty (the model is least sure near 0.5, so a new result teaches the most)
/// and a long time since the last attempt.
/// </summary>
public static class SkillPriority
{
    public const double WeightGap = 0.60, WeightUncertainty = 0.25, WeightRecency = 0.15;
    public const double RecencyDays = 28;

    public static bool IsCandidate(SkillBelief belief) =>
        belief.RuleState is CompetencyState.NotMastered or CompetencyState.Insufficient;

    public static RankedSkill Score(SkillBelief belief, DateOnly today)
    {
        var gap = 1 - belief.PKnown;
        var uncertainty = 4 * belief.PKnown * (1 - belief.PKnown);
        var recency = belief.LastAttempt is { } last ? Math.Clamp((today.DayNumber - last.DayNumber) / RecencyDays, 0, 1) : 1;
        var score = WeightGap * gap + WeightUncertainty * uncertainty + WeightRecency * recency;
        return new(belief.SkillId, score, belief.PKnown, gap, uncertainty, recency);
    }

    /// <summary>The top candidates, best first; ties break on the skill id so the result is stable.</summary>
    public static IReadOnlyList<RankedSkill> Rank(IEnumerable<SkillBelief> beliefs, DateOnly today, int top) =>
        beliefs.Where(IsCandidate).Select(x => Score(x, today)).OrderByDescending(x => x.Score).ThenBy(x => x.SkillId).Take(top).ToList();
}
