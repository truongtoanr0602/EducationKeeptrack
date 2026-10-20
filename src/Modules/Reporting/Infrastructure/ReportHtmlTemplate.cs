using System.Globalization;
using System.Net;
using System.Text;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class ReportHtmlTemplate : IReportHtmlTemplate
{
    private static readonly Lazy<string> Fonts = new(() => Font("NotoSans", "NotoSans.ttf") + Font("NotoEmoji", "NotoEmoji.ttf"));
    private static string Font(string family, string name)
    {
        var assembly = typeof(ReportHtmlTemplate).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(x => x.EndsWith("." + name, StringComparison.Ordinal)))!;
        using var memory = new MemoryStream(); stream.CopyTo(memory);
        return $"@font-face{{font-family:'{family}';src:url(data:font/ttf;base64,{Convert.ToBase64String(memory.ToArray())}) format('truetype');font-weight:100 900;}}";
    }
    public string Render(ReportPreview preview, ReportPayment payment)
    {
        var content = preview.Content(); var draft = content.Draft; var sources = content.Sources; var c = sources.Classroom; var b = sources.Billing; var finance = b.Finance;
        var html = new StringBuilder("<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>Báo cáo tháng</title><style>");
        html.Append(Fonts.Value).Append("body{font-family:NotoSans,sans-serif;color:#182b45;font-size:11px;line-height:1.6;margin:0}h1{font-size:23px}h2{font-size:16px;margin-top:22px}p{white-space:pre-wrap;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse;table-layout:fixed}thead{display:table-header-group}tr{break-inside:avoid}th,td{padding:7px;border-bottom:1px solid #ccd5df;text-align:left;overflow-wrap:anywhere}th{background:#eef3f8}.notice{padding:8px;background:#fff3d7}.emoji{font-family:NotoEmoji,NotoSans}img{width:140px;height:140px}section{break-inside:auto}.total{font-size:16px;font-weight:700}small{font-size:9px;color:#536476}</style></head><body>");
        html.Append("<h1>"); if (draft.Emoji != "none") html.Append("<span class='emoji'>").Append(E(draft.Emoji)).Append("</span> "); html.Append("Báo cáo tháng ").Append(preview.Month.ToString("MM/yyyy")).Append("</h1>");
        html.Append("<p>Học sinh: ").Append(E(draft.DisplayName ?? c.Profile.Name)).Append("\nLớp: ").Append(E(c.ClassName)).Append(" · Môn: ").Append(E(c.Subject))
            .Append("\nPhụ huynh: ").Append(E(draft.ParentDisplayName ?? c.Profile.ParentName ?? "Chưa có thông tin")).Append("\nLiên hệ: ")
            .Append(E(draft.ContactDisplay ?? string.Join(" · ", new[] { c.Profile.Phone, c.Profile.Email }.Where(x => !string.IsNullOrWhiteSpace(x))))).Append("</p>");
        html.Append("<h2>Chuyên cần</h2><p>Buổi đã diễn ra: ").Append(c.Sessions.Count(x => x.SessionStatus == "held"))
            .Append(" · Có mặt: ").Append(c.Sessions.Count(x => x.SessionStatus == "held" && x.AttendanceStatus == "present"))
            .Append(" · Vắng: ").Append(c.Sessions.Count(x => x.SessionStatus == "held" && x.AttendanceStatus == "absent")).Append("</p>");
        Table(html, ["Ngày / Giờ", "Buổi học", "Điểm danh"], c.Sessions.Select(x => new[] { $"{x.Date:yyyy-MM-dd} {x.StartTime:HH:mm}", SessionStatus(x.SessionStatus), Attendance(x.AttendanceStatus) + (x.Excused ? " (nghỉ có phép)" : "") }));
        html.Append("<h2>Kết quả đã xác nhận</h2>");
        Table(html, ["Ngày / Bài đánh giá", "Hoàn thành", "Làm đúng", "Trạng thái"], sources.Learning.Assessments.Select(x => new[] { $"{x.Date:yyyy-MM-dd} · {x.Title}", x.Completion is null ? "—" : x.Completion + "%", x.Correctness is null ? "—" : x.Correctness + "%", x.HasConfirmedResult ? LearningStatus(x.Status) : "Chưa có kết quả xác nhận" }));
        html.Append("<h2>Nhận xét</h2><p>").Append(E(string.IsNullOrWhiteSpace(draft.Comment) ? "Chưa có nhận xét." : draft.Comment)).Append("</p><h2>Lộ trình</h2><p>").Append(E(string.IsNullOrWhiteSpace(draft.Roadmap) ? "Chưa có lộ trình." : draft.Roadmap)).Append("</p>");
        html.Append("<h2>Học phí theo buổi</h2>"); Table(html, ["Ngày buổi học", "Đơn giá", "Phí buổi"], b.Fees.Select(x => new[] { x.Date.ToString("yyyy-MM-dd"), M(x.UnitPriceVnd), M(x.AmountVnd) }));
        if (b.Adjustments.Count > 0) { html.Append("<h2>Điều chỉnh học phí</h2>"); Table(html, ["Chiều", "Số tiền", "Lý do"], b.Adjustments.Select(x => new[] { x.Direction == "increase" ? "Tăng" : "Giảm", M(x.AmountVnd), x.Reason })); }
        html.Append("<p>Học phí tháng sau điều chỉnh: ").Append(M(finance.PeriodFeesVnd)).Append("\nĐã thanh toán kỳ: ").Append(M(finance.PeriodPaidVnd))
            .Append("\nNợ trước tháng: ").Append(M(finance.OldDebtVnd)).Append("\nCòn nợ tháng: ").Append(M(finance.PeriodDebtVnd)).Append("\nTiền chưa phân bổ: ").Append(M(finance.UnallocatedVnd))
            .Append("\nTiền giữ để đối chiếu kỳ: ").Append(M(finance.PeriodHeldVnd)).Append("</p><p class='total'>Tổng cần thu: ").Append(M(finance.OldDebtVnd + finance.PeriodDebtVnd)).Append("</p>");
        html.Append("<h2>Phân bổ thanh toán</h2>"); Table(html, ["Ngày thực nhận", "Kỳ được trả", "Phân bổ"], b.Allocations.Select(x => new[] { x.ReceivedOn.ToString("yyyy-MM-dd"), x.Month, M(x.AmountVnd) }));
        if (payment.AmountVnd != "0")
        {
            html.Append("<h2>Thông tin thanh toán</h2>");
            if (payment.Recipient is { } r) html.Append("<p>Ngân hàng: ").Append(E(r.BankName)).Append("\nSố tài khoản: ").Append(E(r.AccountNo)).Append("\nNgười nhận: ").Append(E(r.AccountName)).Append("\nSố tiền: ").Append(E(payment.AmountVnd)).Append(" đồng\nNội dung: ").Append(E(payment.Content)).Append("</p>");
            if (payment.QrPng is { Length: > 0 }) html.Append("<img alt='QR thanh toán' src='data:image/png;base64,").Append(Convert.ToBase64String(payment.QrPng)).Append("'>");
        }
        foreach (var warning in payment.Warnings) html.Append("<p class='notice'>").Append(E(warning)).Append("</p>");
        if (b.Status == "needs-review") html.Append("<p class='notice'>Có tiền giữ để đối chiếu. Khoản giữ chưa được dùng lại hoặc hoàn trả.</p>");
        html.Append("<p><small>Đối chiếu tại ").Append(E(sources.CapturedAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm:ss"))).Append(" giờ Việt Nam · Phiên bản học phí ").Append(b.Version)
            .Append(" · Bản nháp ").Append(draft.Version).Append(". Số liệu cho các kỳ tới tháng báo cáo, không gồm kỳ sau.</small></p></body></html>"); return html.ToString();
    }
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string M(decimal? value) => value is null ? "Chưa có đơn giá" : value.Value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " đồng";
    private static void Table(StringBuilder html, string[] headers, IEnumerable<string[]> rows)
    {
        html.Append("<table><thead><tr>"); foreach (var header in headers) html.Append("<th>").Append(E(header)).Append("</th>"); html.Append("</tr></thead><tbody>");
        var any = false; foreach (var row in rows) { any = true; html.Append("<tr>"); foreach (var cell in row) html.Append("<td>").Append(E(cell)).Append("</td>"); html.Append("</tr>"); }
        if (!any) html.Append("<tr><td colspan='").Append(headers.Length).Append("'>Chưa có dữ liệu.</td></tr>"); html.Append("</tbody></table>");
    }
    private static string SessionStatus(string value) => value switch { "held" => "Đã diễn ra", "cancelled" => "Đã hủy", _ => "Dự kiến" };
    private static string Attendance(string value) => value switch { "present" => "Có mặt", "absent" => "Vắng", _ => "Chưa điểm danh" };
    private static string LearningStatus(string value) => value switch { "assessed" => "Đã đánh giá", "not_done" => "Không làm bài", "no_correct" => "Không có câu đúng", "unassigned" => "Không giao bài", _ => "Chưa đánh giá" };
}
