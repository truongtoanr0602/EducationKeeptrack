using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Reporting.Application;

public record ReportSourceSnapshot(ClassroomReportSnapshot Classroom, LearningReportSnapshot Learning, BillingReportSnapshot Billing, DateTimeOffset CapturedAt)
{
    public string Stamp => ReportStamp.Of(new { Classroom.Stamp, LearningStamp = Learning.Stamp, BillingStamp = Billing.Stamp });
}
public sealed class ReportSourceCoordinator(IClassroomReportSource classroom, ILearningReportSource learning, IBillingReportSource billing, TimeProvider clock)
{
    public async Task<ReportResult<ReportSourceSnapshot>> CaptureAsync(ReportScope scope, CancellationToken ct)
    {
        var c = await classroom.ReadAsync(scope, ct); if (c is null) return new(null, 404);
        var l = await learning.ReadAsync(scope, ct); var b = await billing.ReadAsync(scope, ct); if (l is null || b is null) return new(null, 404);
        var snapshot = new ReportSourceSnapshot(c, l, b, clock.GetUtcNow());
        if (b.Status is "pending" or "missing-rate" || c.FinancialSourceStamp != b.FinancialSourceStamp || !await ValidateAsync(scope, snapshot, ct))
            return new(null, 409, Message: "Nguồn đã thay đổi hoặc học phí chưa đầy đủ. Vui lòng làm mới báo cáo.");
        return new(snapshot);
    }
    public async Task<bool> ValidateAsync(ReportScope scope, ReportSourceSnapshot snapshot, CancellationToken ct)
    {
        var c = await classroom.ReadAsync(scope, ct); var l = await learning.ReadAsync(scope, ct); var b = await billing.ReadAsync(scope, ct);
        return c?.Stamp == snapshot.Classroom.Stamp && l?.Stamp == snapshot.Learning.Stamp && b?.Stamp == snapshot.Billing.Stamp
            && b?.Status is not ("pending" or "missing-rate") && c?.FinancialSourceStamp == b?.FinancialSourceStamp;
    }
}
