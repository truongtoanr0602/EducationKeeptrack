using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Domain;

namespace ApiTests;

/// <summary>The freshness decision on in-memory snapshots, including the state the API tests cannot reach deterministically.</summary>
public sealed class ExportFreshnessRulesTests
{
    private static readonly ReportScope Scope = new("owner", Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 1));
    private static readonly ReportFinance Money = new(0, 500000, 0, 500000, 500000, 0);

    private static DraftView Draft(long version) => new(Scope.ClassId, Scope.StudentId, "2026-10", null, null, null, "", "", "none", version, null);
    private static ReportSourceSnapshot Sources(string billingStatus = "ready", string billingStamp = "b1") => new(
        new ClassroomReportSnapshot("c1", "f1", "Lớp", "Toán", new("An", null, null, null), []),
        new LearningReportSnapshot("l1", []),
        new BillingReportSnapshot(billingStamp, "f1", billingStatus, 1, 1, Money, [], [], [], null), DateTimeOffset.UnixEpoch);

    private static ReportExport Export(long draftVersion) =>
        new(Scope, new ExportInput(Guid.NewGuid(), draftVersion, Guid.NewGuid()), DateTimeOffset.UnixEpoch);

    private static ExportFreshness Compare(ReportSourceSnapshot built, ReportSourceSnapshot now, long builtDraft = 1, long nowDraft = 1) =>
        ExportFreshnessService.Compare(Export(builtDraft), new PreviewPayload(Draft(builtDraft), built),
            new CurrentReportState(now.Classroom, now.Learning, now.Billing, Draft(nowDraft)));

    [Fact]
    public void Identical_sources_and_draft_are_current()
    {
        var result = Compare(Sources(), Sources());
        Assert.Equal("current", result.State);
        Assert.Empty(result.Changes);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("missing-rate")]
    public void While_billing_has_not_caught_up_nothing_unchanged_is_claimed_current(string status)
    {
        var result = Compare(Sources(), Sources(status));
        Assert.Equal("checking", result.State);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void A_visible_change_wins_over_checking_so_the_teacher_is_not_told_to_wait()
    {
        var result = Compare(Sources(), Sources("pending"), builtDraft: 1, nowDraft: 2);
        Assert.Equal("outdated", result.State);
        Assert.Equal(["draft"], result.Changes.Select(x => x.Area).ToArray());
    }

    [Fact]
    public void A_stamp_change_without_a_visible_difference_still_makes_the_export_outdated()
    {
        var result = Compare(Sources(), Sources(billingStamp: "b2"));
        Assert.Equal("outdated", result.State);
        var change = Assert.Single(result.Changes);
        Assert.Equal("fees", change.Area);
    }
}
