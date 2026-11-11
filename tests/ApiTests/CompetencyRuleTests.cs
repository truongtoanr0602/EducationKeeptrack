using EducationKeeptrack.Learning.Domain;
namespace ApiTests;

/// <summary>The mastery decision as a pure function; no database needed. Rule: 2 attempts, at least 5 tasks, 80%.</summary>
public sealed class CompetencyRuleTests
{
    private static readonly MasteryRule Rule = new(80, 5, 2, 1);
    private static int day;
    private static ConfirmedResult Done(int correct, int tasks, int daysAgo, string title = "Bài") =>
        new(Guid.NewGuid(), $"{title} {day++}", new DateOnly(2026, 11, 1).AddDays(-daysAgo), SkillResultStatus.Assessed, tasks, correct);
    private static ConfirmedResult Other(string status, int daysAgo) =>
        new(Guid.NewGuid(), $"Bài {day++}", new DateOnly(2026, 11, 1).AddDays(-daysAgo), status, 5, null);

    [Fact]
    public void Two_valid_attempts_at_or_above_the_threshold_are_mastered()
    {
        var result = Competency.Evaluate(true, Rule, [Done(4, 5, 1), Done(5, 5, 2)]);
        Assert.Equal(CompetencyState.Mastered, result.State);
        Assert.All(result.Evidence, x => { Assert.True(x.Counted); Assert.True(x.MeetsThreshold); Assert.Null(x.Excluded); });
    }

    [Fact]
    public void One_attempt_below_the_threshold_means_not_mastered()
    {
        var result = Competency.Evaluate(true, Rule, [Done(5, 5, 1), Done(3, 5, 2)]);
        Assert.Equal(CompetencyState.NotMastered, result.State);
        Assert.Equal([true, false], result.Evidence.Select(x => x.MeetsThreshold));
    }

    [Fact]
    public void Attempts_are_judged_one_by_one_and_never_added_together()
    {
        // 3/5 and 5/5 add up to 8/10 = 80%, but the first attempt alone is 60%.
        Assert.Equal(CompetencyState.NotMastered, Competency.Evaluate(true, Rule, [Done(3, 5, 2), Done(5, 5, 1)]).State);
    }

    [Theory]
    [InlineData(8, 10, true)]
    [InlineData(7, 10, false)]
    [InlineData(4, 5, true)]
    [InlineData(79, 100, false)]
    [InlineData(80, 100, true)]
    public void The_threshold_is_inclusive_and_exact(int correct, int tasks, bool meets)
    {
        var result = Competency.Evaluate(true, Rule, [Done(correct, tasks, 1), Done(correct, tasks, 2)]);
        Assert.Equal(meets ? CompetencyState.Mastered : CompetencyState.NotMastered, result.State);
    }

    [Fact]
    public void A_single_attempt_or_none_is_insufficient_data_and_never_weak()
    {
        var one = Competency.Evaluate(true, Rule, [Done(5, 5, 1)]);
        Assert.Equal(CompetencyState.Insufficient, one.State);
        Assert.Equal(1, one.MissingAttempts);
        var none = Competency.Evaluate(true, Rule, []);
        Assert.Equal(CompetencyState.Insufficient, none.State);
        Assert.Equal(2, none.MissingAttempts);
        Assert.Equal(CompetencyState.Insufficient, Competency.Evaluate(true, Rule, [Done(0, 5, 1)]).State);
    }

    [Fact]
    public void Attempts_with_too_few_tasks_are_listed_with_the_reason_but_take_no_place()
    {
        var short1 = Done(4, 4, 1);
        var result = Competency.Evaluate(true, Rule, [short1, Done(5, 5, 2)]);
        Assert.Equal(CompetencyState.Insufficient, result.State);
        Assert.Equal(1, result.MissingAttempts);
        var excluded = result.Evidence.Single(x => x.AssessmentId == short1.AssessmentId);
        Assert.False(excluded.Counted);
        Assert.Contains("Dưới 5 nhiệm vụ", excluded.Excluded);
        var fine = Competency.Evaluate(true, Rule, [short1, Done(5, 5, 2), Done(4, 5, 3)]);
        Assert.Equal(CompetencyState.Mastered, fine.State);
    }

    [Fact]
    public void Not_done_is_evidence_with_a_reason_and_takes_no_place_while_unassigned_is_not_evidence()
    {
        var result = Competency.Evaluate(true, Rule, [Other(SkillResultStatus.NotDone, 1), Other(SkillResultStatus.Unassigned, 1), Done(5, 5, 2), Done(5, 5, 3)]);
        Assert.Equal(CompetencyState.Mastered, result.State);
        Assert.Equal(3, result.Evidence.Count);
        Assert.Contains("Không làm bài", result.Evidence.Single(x => x.Status == SkillResultStatus.NotDone).Excluded);
        Assert.DoesNotContain(result.Evidence, x => x.Status == SkillResultStatus.Unassigned);
    }

    [Fact]
    public void Only_the_most_recent_valid_attempts_are_judged()
    {
        var old = Done(1, 5, 30);
        var result = Competency.Evaluate(true, Rule, [old, Done(5, 5, 2), Done(4, 5, 1)]);
        Assert.Equal(CompetencyState.Mastered, result.State);
        var older = result.Evidence.Single(x => x.AssessmentId == old.AssessmentId);
        Assert.False(older.Counted);
        Assert.Equal("Cũ hơn các lần gần nhất", older.Excluded);
        Assert.Equal(old.AssessmentId, result.Evidence[^1].AssessmentId);
    }

    [Fact]
    public void A_skill_that_is_not_taught_gets_no_conclusion_but_keeps_its_evidence()
    {
        var result = Competency.Evaluate(false, Rule, [Done(5, 5, 1), Done(5, 5, 2)]);
        Assert.Equal(CompetencyState.NotTaught, result.State);
        Assert.Equal(2, result.Evidence.Count);
        Assert.Equal(CompetencyState.NotTaught, Competency.Evaluate(false, Rule, []).State);
    }

    [Fact]
    public void The_rule_values_decide_the_outcome()
    {
        var results = new[] { Done(4, 5, 1), Done(4, 5, 2), Done(4, 5, 3) };
        Assert.Equal(CompetencyState.NotMastered, Competency.Evaluate(true, new(90, 5, 2, 2), results).State);
        Assert.Equal(CompetencyState.Insufficient, Competency.Evaluate(true, new(80, 5, 4, 2), results).State);
        Assert.Equal(CompetencyState.Insufficient, Competency.Evaluate(true, new(80, 6, 2, 2), results).State);
        Assert.Equal(CompetencyState.Mastered, Competency.Evaluate(true, new(80, 5, 1, 2), results).State);
    }
}
