using System.Globalization;
using System.Net;
using System.Text;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Infrastructure;

/// <summary>
/// Parent-facing report. Page 1 is an at-a-glance overview (student, amount due and QR, attendance chips, comments, roadmap);
/// page 2 holds the reconciliation detail. Output is self-contained HTML/CSS because the renderer disables JavaScript and network.
/// </summary>
public sealed class ReportHtmlTemplate : IReportHtmlTemplate
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly Lazy<string> Fonts = new(() => Font("NotoSans", "NotoSans.ttf") + Font("NotoEmoji", "NotoEmoji.ttf"));
    private const string Css = """
        @page{size:A4}
        *{box-sizing:border-box}
        body{font-family:NotoSans,sans-serif;color:#10264a;font-size:11px;line-height:1.55;margin:0}
        .sheet{background:#f3f7ff;border:1.5px solid #c9dcf8;border-radius:18px;padding:16px 18px}
        .head{text-align:center;margin-bottom:10px}
        .head h1{font-size:22px;margin:0;color:#0f3a82;letter-spacing:.2px}
        .head .sub{color:#4a5f82;font-size:11px}
        .grid{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin-bottom:12px}
        .card{background:#fff;border:1.5px solid #c9dcf8;border-radius:14px;padding:12px 14px;break-inside:avoid}
        .card h2{font-size:12px;margin:0 0 6px;color:#0f3a82;text-transform:uppercase;letter-spacing:.4px;border-bottom:1px solid #dbe7fb;padding-bottom:4px}
        dl{margin:0}
        .kv{display:flex;justify-content:space-between;gap:8px;padding:2.5px 0;border-bottom:1px dashed #d6e2f6}
        .kv dt{color:#5a6f92}.kv dd{margin:0;font-weight:600;text-align:right;overflow-wrap:anywhere}
        .chips{display:grid;grid-template-columns:repeat(4,1fr);gap:6px;margin-top:8px}
        .chip{background:#dce9ff;border-radius:999px;padding:3px 0;text-align:center;font-size:11px;font-weight:600;color:#12346b;font-variant-numeric:tabular-nums}
        .chip.wide{grid-column:1/-1}
        .two{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin:12px 0}
        .two .card h2{margin-bottom:6px}
        .tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(110px,1fr));gap:8px}
        .tile{background:#eef4ff;border-radius:12px;padding:8px 10px;text-align:center}
        .tile span{display:block;font-size:10px;color:#5a6f92}
        .tile b{display:block;font-size:14px;color:#0f3a82;font-variant-numeric:tabular-nums}
        .tile.debt b{color:#c2410c}.tile.ok b{color:#0a6b3a}
        @media screen{.detail{padding:0 6px 12px}}
        @media (max-width:640px){.grid,.two{grid-template-columns:1fr}.sheet{padding:12px}.chips{grid-template-columns:repeat(4,1fr)}}
        .chip.absent{background:#ffe2d3;color:#8a3410}
        .chip.excused{background:#fff0c2;color:#6b4d00}
        .chip.unmarked{background:#eceff4;color:#5a6f92}
        .legend{font-size:9px;color:#5a6f92;margin-top:5px}
        .pay{text-align:center;display:flex;flex-direction:column}
        .pay .body{flex:1;display:flex;flex-direction:column;justify-content:center;align-items:center}
        .total{white-space:nowrap;font-size:11px;color:#4a5f82}
        .total .amt{font-size:26px;font-weight:700;color:#0f3a82}
        .paid-ok{font-size:14px;font-weight:700;color:#0a6b3a;margin:8px 0}
        .qr{display:block;margin:8px auto;width:170px;height:auto;max-height:200px;object-fit:contain;border:1px solid #dbe7fb;border-radius:10px;padding:5px;background:#fff}
        .hint{font-size:10px;color:#5a6f92;margin-top:2px}
        .bank{font-size:11px;line-height:1.5}
        .meter{display:flex;height:9px;border-radius:5px;overflow:hidden;background:#e4ecfa;margin:6px 0 2px}
        .meter .p{background:#2a78d6}.meter .d{background:#eb6834}
        .meter-label{display:flex;justify-content:space-between;font-size:9.5px;color:#4a5f82}
        .stats{display:flex;gap:8px;margin:2px 0 6px}
        .stat{flex:1;text-align:center;background:#eef4ff;border-radius:10px;padding:4px 2px}
        .stat b{display:block;font-size:16px;color:#0f3a82}.stat span{font-size:9.5px;color:#5a6f92}
        .text{margin:0;white-space:pre-wrap;overflow-wrap:anywhere}
        .section{margin:12px 0 4px;break-inside:avoid}
        .section h2{font-size:13px;color:#0f3a82;border-bottom:1.5px solid #c9dcf8;margin:0 0 5px;padding-bottom:3px;text-transform:uppercase;letter-spacing:.4px}
        .skill{display:grid;grid-template-columns:1fr 70px 70px;gap:6px;align-items:center;padding:3px 0;border-bottom:1px dashed #d6e2f6}
        .skill .t{overflow-wrap:anywhere}
        .bar{height:7px;border-radius:4px;background:#e4ecfa;overflow:hidden}.bar i{display:block;height:100%;background:#2a78d6}
        .bar.alt i{background:#1baf7a}
        .pct{font-size:9px;color:#4a5f82;text-align:right}
        .foot{margin-top:12px;padding:8px 12px;background:#dce9ff;border-radius:999px;text-align:center;font-weight:600;color:#12346b}
        .notice{padding:8px;background:#fff3d7;border-radius:8px;margin:8px 0}
        .emoji{font-family:NotoEmoji,NotoSans}
        .detail{break-before:page}
        .detail h2{font-size:14px;margin:16px 0 6px;color:#0f3a82}
        table{width:100%;border-collapse:collapse;table-layout:fixed;font-size:11px}
        thead{display:table-header-group}
        tr{break-inside:avoid}
        th,td{padding:5px 7px;border-bottom:1px solid #d6e2f6;text-align:left;overflow-wrap:anywhere}
        th{background:#e8f0fd;color:#0f3a82}
        small{font-size:9px;color:#536476}
        """;

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
        var held = c.Sessions.Where(x => x.SessionStatus == "held").OrderBy(x => x.Date).ThenBy(x => x.StartTime).ToList();
        var present = held.Count(x => x.AttendanceStatus == "present"); var absent = held.Count(x => x.AttendanceStatus == "absent");
        var due = finance.OldDebtVnd + finance.PeriodDebtVnd;
        var html = new StringBuilder("<!doctype html><html lang='vi'><head><meta charset='utf-8'><title>Báo cáo tháng</title><style>");
        html.Append(Fonts.Value).Append(Css).Append("</style></head><body><div class='sheet'>");

        html.Append("<div class='head'><h1>"); if (draft.Emoji != "none") html.Append("<span class='emoji'>").Append(E(draft.Emoji)).Append("</span> ");
        html.Append("Báo cáo tháng ").Append(preview.Month.ToString("MM/yyyy")).Append("</h1><div class='sub'>Học tập và học phí · ").Append(E(c.ClassName)).Append("</div></div>");

        // Overview: student card + amount due / QR.
        html.Append("<div class='grid'><div class='card'><h2>Thông tin học sinh</h2><dl>")
            .Append(Kv("Họ và tên", draft.DisplayName ?? c.Profile.Name)).Append(Kv("Lớp / Môn", $"{c.ClassName} · {c.Subject}"))
            .Append(Kv("Phụ huynh", draft.ParentDisplayName ?? c.Profile.ParentName ?? "Chưa có thông tin"))
            .Append(Contact(draft.ContactDisplay ?? string.Join(" · ", new[] { c.Profile.Phone, c.Profile.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))))
            .Append(Kv("Học phí", UnitPrice(b.Fees))).Append(Kv("Buổi học", $"{held.Count} buổi · có mặt {present} · vắng {absent}")).Append("</dl>");
        html.Append("<div class='chips'>");
        foreach (var s in held)
        {
            var kind = s.AttendanceStatus == "absent" ? (s.Excused ? "excused" : "absent") : s.AttendanceStatus == "present" ? "" : "unmarked";
            html.Append("<span class='chip ").Append(kind).Append("'>").Append(s.Date.ToString("dd/MM")).Append("</span>");
        }
        if (held.Count == 0) html.Append("<span class='chip unmarked wide'>Chưa có buổi học trong tháng</span>");
        html.Append("</div><div class='legend'>Xanh: có mặt · Cam: vắng · Vàng: nghỉ có phép · Xám: chưa điểm danh</div></div>");

        html.Append("<div class='card pay'><h2>Học phí cần thanh toán</h2><div class='body'><div class='total'>Tổng cần thu: <span class='amt'>").Append(M(due)).Append("</span></div>");
        PayBlock(html, payment, due);
        html.Append("</div></div></div>");

        // Money at a glance.
        var feesTotal = Math.Max(finance.PeriodFeesVnd, 0); var paidShare = feesTotal > 0 ? Math.Clamp(finance.PeriodPaidVnd / feesTotal * 100, 0, 100) : 0;
        var debtShare = feesTotal > 0 ? Math.Clamp(finance.PeriodDebtVnd / feesTotal * 100, 0, 100 - paidShare) : 0;
        html.Append("<div class='card'><h2>Tóm tắt học phí tháng</h2><div class='tiles'>")
            .Append(Tile("Học phí sau điều chỉnh", M(finance.PeriodFeesVnd), "")).Append(Tile("Đã thanh toán kỳ", M(finance.PeriodPaidVnd), finance.PeriodPaidVnd > 0 ? "ok" : ""));
        if (finance.OldDebtVnd != 0) html.Append(Tile("Nợ trước tháng", M(finance.OldDebtVnd), "debt"));
        html.Append(Tile("Còn nợ tháng", M(finance.PeriodDebtVnd), finance.PeriodDebtVnd > 0 ? "debt" : "ok")).Append("</div>")
            .Append("<div class='meter'><span class='p' style='width:").Append(Pct(paidShare)).Append("%'></span><span class='d' style='width:").Append(Pct(debtShare)).Append("%'></span></div>")
            .Append("<div class='meter-label'><span>Xanh: đã thanh toán</span><span>Cam: còn nợ</span></div></div>");

        html.Append("<div class='two'><div class='card'><h2>Nhận xét học tập</h2><p class='text'>").Append(E(string.IsNullOrWhiteSpace(draft.Comment) ? "Chưa có nhận xét." : draft.Comment)).Append("</p></div>");
        html.Append("<div class='card'><h2>Lộ trình sắp tới</h2><p class='text'>").Append(E(string.IsNullOrWhiteSpace(draft.Roadmap) ? "Chưa có lộ trình." : draft.Roadmap)).Append("</p></div></div>");

        // An empty results list is noise for parents, so the section appears only when there is something to show.
        if (sources.Learning.Assessments.Count > 0) html.Append("<div class='section'><h2>Kết quả đã xác nhận</h2>");
        foreach (var x in sources.Learning.Assessments)
        {
            html.Append("<div class='skill'><div class='t'>").Append(x.Date.ToString("dd/MM")).Append(" · ").Append(E(x.Title)).Append("</div>");
            if (x.HasConfirmedResult) html.Append(Bar("Hoàn thành", x.Completion, false)).Append(Bar("Làm đúng", x.Correctness, true));
            else html.Append("<div class='pct' style='grid-column:span 2'>Chưa có kết quả xác nhận</div>");
            html.Append("</div>");
        }
        if (sources.Learning.Assessments.Count > 0) html.Append("</div>");

        foreach (var warning in payment.Warnings) html.Append("<p class='notice'>").Append(E(warning)).Append("</p>");
        if (b.Status == "needs-review") html.Append("<p class='notice'>Có tiền giữ để đối chiếu. Khoản giữ chưa được dùng lại hoặc hoàn trả.</p>");
        html.Append("<div class='foot'>Phụ huynh vui lòng kiểm tra thông tin học phí và lịch học. Cảm ơn quý phụ huynh.</div></div>");

        // Page 2: reconciliation detail.
        html.Append("<div class='detail'><h2>Chi tiết điểm danh</h2>");
        Table(html, ["Ngày / Giờ", "Buổi học", "Điểm danh"], c.Sessions.Select(x => new[] { $"{x.Date:yyyy-MM-dd} {x.StartTime:HH:mm}", SessionStatus(x.SessionStatus), Attendance(x.AttendanceStatus) + (x.Excused ? " (nghỉ có phép)" : "") }));
        html.Append("<h2>Học phí theo buổi</h2>"); Table(html, ["Ngày buổi học", "Đơn giá", "Phí buổi"], b.Fees.Select(x => new[] { x.Date.ToString("yyyy-MM-dd"), M(x.UnitPriceVnd), M(x.AmountVnd) }));
        if (b.Adjustments.Count > 0) { html.Append("<h2>Điều chỉnh học phí</h2>"); Table(html, ["Chiều", "Số tiền", "Lý do"], b.Adjustments.Select(x => new[] { x.Direction == "increase" ? "Tăng" : "Giảm", M(x.AmountVnd), x.Reason })); }
        html.Append("<h2>Đối chiếu số dư</h2><dl>").Append(Kv("Học phí tháng sau điều chỉnh", M(finance.PeriodFeesVnd))).Append(Kv("Đã thanh toán kỳ", M(finance.PeriodPaidVnd)))
            .Append(Kv("Nợ trước tháng", M(finance.OldDebtVnd))).Append(Kv("Còn nợ tháng", M(finance.PeriodDebtVnd))).Append(Kv("Tiền chưa phân bổ", M(finance.UnallocatedVnd)))
            .Append(Kv("Tiền giữ để đối chiếu kỳ", M(finance.PeriodHeldVnd)));
        // Reserve lines appear only when a reserve exists or moved, so reports without reserves look exactly as before.
        if (finance.ReserveOpeningVnd != 0 || finance.ReserveApprovedVnd != 0 || finance.ReserveUsedVnd != 0 || finance.ReserveCancelledVnd != 0 || finance.ReserveCarriedVnd != 0 || finance.ReserveAppliedVnd != 0)
        {
            html.Append(Kv("Bảo lưu đầu kỳ", M(finance.ReserveOpeningVnd))).Append(Kv("Bảo lưu mới duyệt trong tháng", M(finance.ReserveApprovedVnd))).Append(Kv("Bảo lưu đã dùng trong tháng", M(finance.ReserveUsedVnd)));
            if (finance.ReserveCancelledVnd != 0) html.Append(Kv("Bảo lưu đã hủy trong tháng", M(finance.ReserveCancelledVnd)));
            html.Append(Kv("Bảo lưu chuyển sang kỳ sau", M(finance.ReserveCarriedVnd))).Append(Kv("Đã khấu trừ bảo lưu vào học phí tháng này", M(finance.ReserveAppliedVnd)));
        }
        html.Append("</dl><h2>Phân bổ thanh toán</h2>"); Table(html, ["Ngày thực nhận", "Kỳ được trả", "Phân bổ"], b.Allocations.Select(x => new[] { x.ReceivedOn.ToString("yyyy-MM-dd"), x.Month, M(x.AmountVnd) }));
        html.Append("<p><small>Đối chiếu tại ").Append(E(sources.CapturedAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm:ss"))).Append(" giờ Việt Nam · Phiên bản học phí ").Append(b.Version)
            .Append(" · Bản nháp ").Append(draft.Version).Append(". Số liệu cho các kỳ tới tháng báo cáo, không gồm kỳ sau.</small></p></div></body></html>"); return html.ToString();
    }

    private static void PayBlock(StringBuilder html, ReportPayment payment, decimal due)
    {
        if (payment.AmountVnd == "0") { html.Append("<div class='paid-ok'>Không có khoản cần thanh toán</div>"); return; }
        if (payment.QrPng is { Length: > 0 }) html.Append("<img class='qr' alt='QR thanh toán' src='data:image/png;base64,").Append(Convert.ToBase64String(payment.QrPng)).Append("'>");
        if (payment.Recipient is { } r)
            html.Append("<div class='bank'>Ngân hàng: <b>").Append(E(r.BankName)).Append("</b><br>Số TK: <b>").Append(E(r.AccountNo)).Append("</b><br>Chủ TK: <b>").Append(E(r.AccountName))
                .Append("</b><br>Nội dung: ").Append(E(payment.Content)).Append("</div>");
    }

    private static string Contact(string value) => string.IsNullOrWhiteSpace(value) ? "" : Kv("Liên hệ", value);
    private static string Tile(string label, string value, string kind) => $"<div class='tile {kind}'><span>{E(label)}</span><b>{E(value)}</b></div>";
    private static string Kv(string key, string value) => $"<div class='kv'><dt>{E(key)}</dt><dd>{E(value)}</dd></div>";
    private static string Bar(string label, int? value, bool alt) =>
        value is null ? "<div class='pct'>—</div>" : $"<div><div class='bar{(alt ? " alt" : "")}'><i style='width:{Math.Clamp(value.Value, 0, 100)}%'></i></div><div class='pct'>{E(label)} {value}%</div></div>";
    private static string Pct(decimal value) => Math.Round(value, 0).ToString("0", CultureInfo.InvariantCulture);
    private static string UnitPrice(IReadOnlyList<ReportFeeLine> fees)
    {
        var prices = fees.Where(x => x.UnitPriceVnd is not null).Select(x => x.UnitPriceVnd!.Value).Distinct().ToList();
        return prices.Count == 0 ? "Chưa có đơn giá" : prices.Count == 1 ? prices[0].ToString("N0", Vi) + "đ/buổi" : "Nhiều mức giá (xem chi tiết)";
    }
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
    private static string M(decimal? value) => value is null ? "Chưa có đơn giá" : value.Value.ToString("N0", Vi) + " đồng";
    private static void Table(StringBuilder html, string[] headers, IEnumerable<string[]> rows)
    {
        html.Append("<table><thead><tr>"); foreach (var header in headers) html.Append("<th>").Append(E(header)).Append("</th>"); html.Append("</tr></thead><tbody>");
        var any = false; foreach (var row in rows) { any = true; html.Append("<tr>"); foreach (var cell in row) html.Append("<td>").Append(E(cell)).Append("</td>"); html.Append("</tr>"); }
        if (!any) html.Append("<tr><td colspan='").Append(headers.Length).Append("'>Chưa có dữ liệu.</td></tr>"); html.Append("</tbody></table>");
    }
    private static string SessionStatus(string value) => value switch { "held" => "Đã diễn ra", "cancelled" => "Đã hủy", _ => "Dự kiến" };
    private static string Attendance(string value) => value switch { "present" => "Có mặt", "absent" => "Vắng", _ => "Chưa điểm danh" };
}
