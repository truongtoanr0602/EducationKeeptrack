using System.Globalization;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Billing.Application;
using EducationKeeptrack.Billing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EducationKeeptrack.Billing.Infrastructure;

public sealed class BillingReportSource(IBillingRepository repository, BillingDatabase db, IClassroomBillingSource classroom, IVietQrGenerator generator) : IBillingReportSource
{
    public async Task<BillingReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(scope.OwnerId, scope.ClassId, ct);
        if (source is null || !source.Students.Any(x => x.Id == scope.StudentId)) return null;
        var snapshot = await repository.SnapshotAsync(scope.OwnerId, scope.ClassId, ct);
        var periods = PeriodBalance.Calculate(snapshot.Sessions, snapshot.Receipts, snapshot.PaymentReviews, snapshot.Adjustments).Where(x => x.StudentId == scope.StudentId).ToArray();
        var current = periods.SingleOrDefault(x => x.Period == scope.Month);
        var receipts = snapshot.Receipts.Where(x => x.Status == "active" && x.StudentId == scope.StudentId).ToArray();
        var recipient = await db.Set<PaymentRecipient>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == scope.OwnerId, ct);
        var financial = ReportStamp.Financial(source);
        var stamp = ReportStamp.Of(new { scope, Version = snapshot.Ledger?.Version ?? 0, RecipientVersion = recipient?.Version ?? 0, Financial = financial });
        var end = scope.Month.AddMonths(1);
        var fees = snapshot.Sessions.Where(x => x.Date >= scope.Month && x.Date < end).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.SessionId)
            .SelectMany(x => x.Fees.Where(f => f.StudentId == scope.StudentId).Select(f => new ReportFeeLine(x.SessionId, x.Date, x.UnitPriceVnd, f.AmountVnd))).ToArray();
        var adjustments = snapshot.Adjustments.Where(x => x.StudentId == scope.StudentId && x.Period == scope.Month).OrderBy(x => x.Version).Select(x => new ReportAdjustment(x.Id, x.Direction, x.AmountVnd, x.Reason)).ToArray();
        var allocations = receipts.OrderBy(x => x.ReceivedOn).ThenBy(x => x.Id).SelectMany(x => x.Allocations.Where(a => a.Period <= scope.Month).OrderBy(a => a.Period)
            .Select(a => new ReportAllocation(x.Id, x.ReceivedOn, a.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture), a.AmountVnd))).ToArray();
        return new(stamp, financial, PaymentService.Status(source, snapshot.Ledger, snapshot.Sessions, snapshot.PaymentReviews), snapshot.Ledger?.Version ?? 0, recipient?.Version ?? 0,
            new(periods.Where(x => x.Period < scope.Month).Sum(x => x.Debt), current?.Fees ?? 0, current?.Paid ?? 0, current?.Held ?? 0, current?.Debt ?? 0, receipts.Sum(x => x.UnallocatedVnd)), fees, adjustments, allocations,
            recipient is null ? null : new(recipient.BankBin, recipient.BankName, recipient.AccountNo, recipient.AccountName));
    }
    public async Task<ReportPayment> PaymentAsync(ReportScope scope, BillingReportSnapshot snapshot, CancellationToken ct)
    {
        var amount = (snapshot.Finance.OldDebtVnd + snapshot.Finance.PeriodDebtVnd).ToString("0", CultureInfo.InvariantCulture);
        if (amount == "0") return new("none", null, snapshot.Recipient, amount, "", []);
        var recipient = await db.Set<PaymentRecipient>().AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == scope.OwnerId, ct);
        if ((recipient?.Version ?? 0) != snapshot.RecipientVersion) throw new ReportSourceChangedException();
        if (recipient is null) return new("manual", null, null, amount, "", ["Chưa cấu hình thông tin thanh toán. Vui lòng liên hệ giáo viên."]);
        var content = $"HP{scope.Month:yyyyMM}{scope.StudentId:N}"[..24]; byte[]? qr = null;
        try { qr = await generator.GenerateAsync(new(recipient.BankBin, recipient.BankName, recipient.AccountNo, recipient.AccountName, recipient.StaticQr is not null, recipient.Version), amount, content, ct); }
        catch (HttpRequestException) { } catch (IOException) { } catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        if (QrImages.IsPng(qr)) return new("vietqr", qr, snapshot.Recipient, amount, content, []);
        if (QrImages.IsPng(recipient.StaticQr)) return new("static", recipient.StaticQr, snapshot.Recipient, amount, content, ["QR động không khả dụng; kiểm tra số tiền khi dùng QR tĩnh."]);
        return new("manual", null, snapshot.Recipient, amount, content, ["Không có QR; vui lòng dùng thông tin chuyển khoản."]);
    }
}
