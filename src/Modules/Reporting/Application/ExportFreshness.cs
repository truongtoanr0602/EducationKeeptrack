using System.Globalization;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Application;

/// <param name="Area">draft, attendance, results, fees or payment-info.</param>
public record FreshnessChange(string Area, string Label, string Before, string After);

/// <param name="State">current, outdated, checking (billing has not caught up so nothing can be claimed) or unknown (export not ready).</param>
public record ExportFreshness(string State, IReadOnlyList<FreshnessChange> Changes);

public record ExportItemView(Guid Id, string State, DateTimeOffset CreatedAt, long ByteLength, string? Sha256, long DraftVersion, string SourceStamp,
    IReadOnlyList<string> Warnings, ExportFreshness Freshness);

/// <summary>What the sources and the draft look like right now, to compare against what each export was built from.</summary>
public sealed record CurrentReportState(ClassroomReportSnapshot Classroom, LearningReportSnapshot Learning, BillingReportSnapshot Billing, DraftView Draft);

/// <summary>
/// Tells the teacher which saved PDFs no longer match the data. It is computed on read from the source snapshot every export
/// stored (not from events), so a change made in any module is seen the next time the list is loaded. Existing PDFs are never
/// modified: a new export is a new object and the old one stays downloadable.
/// </summary>
public sealed class ExportFreshnessService(IReportingRepository repository, IClassroomReportSource classroom, ILearningReportSource learning,
    IBillingReportSource billing, DraftService drafts)
{
    public async Task<ReportResult<IReadOnlyList<ExportItemView>>> ListAsync(ReportScope scope, CancellationToken ct)
    {
        var c = await classroom.ReadAsync(scope, ct);
        if (c is null) return new(null, 404);
        var l = await learning.ReadAsync(scope, ct);
        var b = await billing.ReadAsync(scope, ct);
        var draft = await drafts.ReadAsync(scope, ct);
        if (l is null || b is null || draft.Value is null) return new(null, 404);
        var current = new CurrentReportState(c, l, b, draft.Value);
        var items = new List<ExportItemView>();
        foreach (var export in await repository.ExportsAsync(scope, ct))
        {
            var preview = await repository.PreviewAsync(scope, export.PreviewId, ct);
            if (preview is null) continue;
            var view = ExportService.View(export, preview);
            items.Add(new(view.Id, view.State, view.CreatedAt, view.ByteLength, view.Sha256, view.DraftVersion, view.SourceStamp, view.Warnings,
                export.State == "ready" ? Compare(export, preview.Content(), current) : new("unknown", [])));
        }
        return new(items);
    }

    public static ExportFreshness Compare(ReportExport export, PreviewPayload built, CurrentReportState now)
    {
        var changes = new List<FreshnessChange>();
        var was = built.Sources;
        if (now.Draft.Version != export.DraftVersion)
            changes.Add(new("draft", "Nhận xét, lộ trình hoặc tên hiển thị đã được sửa", $"Bản nháp {export.DraftVersion}", $"Bản nháp {now.Draft.Version}"));
        if (now.Classroom.Stamp != was.Classroom.Stamp)
            changes.Add(new("attendance", "Buổi học hoặc điểm danh đã thay đổi", Attendance(was.Classroom.Sessions), Attendance(now.Classroom.Sessions)));
        if (now.Learning.Stamp != was.Learning.Stamp) changes.AddRange(ResultChanges(was.Learning.Assessments, now.Learning.Assessments));
        if (now.Billing.Stamp != was.Billing.Stamp)
        {
            var money = MoneyChanges(was.Billing.Finance, now.Billing.Finance);
            changes.AddRange(money);
            if (now.Billing.RecipientVersion != was.Billing.RecipientVersion)
                changes.Add(new("payment-info", "Thông tin nhận tiền đã thay đổi", Recipient(was.Billing.Recipient), Recipient(now.Billing.Recipient)));
            else if (money.Count == 0)
                changes.Add(new("fees", "Học phí hoặc thanh toán đã được cập nhật", "Theo bản đã xuất", "Theo dữ liệu hiện tại"));
        }
        // While billing is still catching up, the numbers on screen may lag: say so instead of claiming the PDF is current.
        var state = changes.Count > 0 ? "outdated" : now.Billing.Status is "pending" or "missing-rate" ? "checking" : "current";
        return new(state, changes);
    }

    private static string Attendance(IReadOnlyList<ReportSession> sessions)
    {
        var held = sessions.Where(x => x.SessionStatus == "held").ToArray();
        return $"{held.Count(x => x.AttendanceStatus == "present")} buổi có mặt trên {held.Length} buổi đã diễn ra";
    }

    private static IEnumerable<FreshnessChange> ResultChanges(IReadOnlyList<ReportAssessment> before, IReadOnlyList<ReportAssessment> after)
    {
        static string Text(ReportAssessment? a) => a is not { HasConfirmedResult: true } ? "Chưa có kết quả được xác nhận"
            : a.Status switch
            {
                "assessed" => $"Hoàn thành {a.Completion}%, làm đúng {a.Correctness}%",
                "no_correct" => $"Hoàn thành {a.Completion}%, không có câu đúng",
                "not_done" => "Không làm bài",
                "unassigned" => "Không giao bài",
                _ => "Chưa đánh giá"
            };
        var old = before.ToDictionary(x => x.Id);
        var seen = new HashSet<Guid>();
        foreach (var a in after)
        {
            seen.Add(a.Id);
            old.TryGetValue(a.Id, out var was);
            if (Text(was) != Text(a)) yield return new("results", $"Kết quả đánh giá: {a.Title}", Text(was), Text(a));
        }
        foreach (var gone in before.Where(x => !seen.Contains(x.Id)))
            yield return new("results", $"Bài đánh giá không còn trong báo cáo: {gone.Title}", Text(gone), "Không còn");
    }

    private static List<FreshnessChange> MoneyChanges(ReportFinance before, ReportFinance after)
    {
        var changes = new List<FreshnessChange>();
        void Add(string label, decimal a, decimal b) { if (a != b) changes.Add(new("fees", label, Vnd(a), Vnd(b))); }
        Add("Học phí phát sinh trong tháng", before.PeriodFeesVnd, after.PeriodFeesVnd);
        Add("Nợ cũ", before.OldDebtVnd, after.OldDebtVnd);
        Add("Đã phân bổ cho tháng này", before.PeriodPaidVnd, after.PeriodPaidVnd);
        Add("Còn nợ của tháng", before.PeriodDebtVnd, after.PeriodDebtVnd);
        Add("Tiền chưa phân bổ", before.UnallocatedVnd, after.UnallocatedVnd);
        return changes;
    }

    private static string Vnd(decimal value) => value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đồng";
    private static string Recipient(ReportRecipient? r) => r is null ? "Chưa thiết lập" : $"Đã thiết lập ({r.BankName})";
}
