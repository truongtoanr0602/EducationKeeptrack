using System.Globalization;
using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Billing.Application;

public record AllocationView(string Month, string AmountVnd);
public record ReceiptView(Guid Id, Guid StudentId, string AmountVnd, DateOnly ReceivedOn, string? Note,
    DateTimeOffset RecordedAt, string UnallocatedVnd, IReadOnlyList<AllocationView> Allocations, string Status, ReceiptCorrectionView? Correction);
public record PeriodDebtView(string Month, string FeesVnd, string PaidVnd, string DebtVnd, string Status, string AllocationExcessVnd);
public record StudentPaymentsView(Guid StudentId, string Name, string OldDebtVnd, string CurrentDebtVnd,
    string TotalDebtVnd, string UnallocatedVnd, string AllocationExcessVnd, IReadOnlyList<PeriodDebtView> Periods, IReadOnlyList<ReceiptView> Receipts);
public record PaymentsView(string Month, long Version, string Status, string TotalDebtVnd, IReadOnlyList<StudentPaymentsView> Students);

public sealed partial class PaymentService(IBillingRepository repository, IClassroomBillingSource classroom, TimeProvider clock)
{
    public async Task<BillingResult<PaymentsView>> ReportAsync(string owner, Guid classId, string month, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return new(null, BillingFailure.NotFound);
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return new(null, BillingFailure.Invalid, Errors: new() { ["month"] = ["Tháng không hợp lệ."] });
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        var balances = PeriodBalance.Calculate(snapshot.Sessions, snapshot.Receipts, snapshot.PaymentReviews, snapshot.Adjustments);
        var students = source.Students.Select(student =>
        {
            var receipts = snapshot.Receipts.Where(x => x.StudentId == student.Id).ToArray();
            var periods = balances.Where(x => x.StudentId == student.Id).ToArray();
            return new StudentPaymentsView(student.Id, student.Name,
                Money(periods.Where(x => x.Period < first).Sum(x => x.Debt)),
                Money(periods.Where(x => x.Period == first).Sum(x => x.Debt)),
                Money(periods.Sum(x => x.Debt)), Money(receipts.Where(x => x.Status == "active").Sum(x => x.UnallocatedVnd)), Money(periods.Sum(x => x.Held)),
                periods.Select(x => new PeriodDebtView(Month(x.Period), Money(x.Fees), Money(x.Paid), Money(x.Debt),
                    x.Debt == 0 ? "paid" : x.Paid > 0 ? "partial" : "unpaid", Money(x.Held))).ToArray(),
                receipts.OrderByDescending(x => x.ReceivedOn).ThenByDescending(x => x.RecordedAt).ThenBy(x => x.Id).Select(View).ToArray());
        }).ToArray();
        return new(new PaymentsView(month, snapshot.Ledger?.Version ?? 0, Status(source, snapshot.Ledger, snapshot.Sessions, snapshot.PaymentReviews),
            Money(students.Sum(x => decimal.Parse(x.TotalDebtVnd, CultureInfo.InvariantCulture))), students));
    }

    public async Task<BillingResult<ReceiptView>> RecordAsync(string owner, Guid classId, ReceiptInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<ReceiptView>(classId, async () =>
        {
            var receipts = await repository.ReceiptsAsync(owner, classId, ct);
            if (receipts.Any(x => x.CorrectionKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho sửa/hủy khoản nhận tiền.");
            var existing = receipts.SingleOrDefault(x => x.IdempotencyKey == input.IdempotencyKey);
            // A retry after a lost response returns the original receipt, even with a stale expected version.
            if (existing is not null) return existing.Matches(input) ? new(View(existing))
                : new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho khoản nhận tiền khác.");
            var source = await classroom.ReadClassAsync(owner, classId, ct);
            if (source is null || !source.Students.Any(x => x.Id == input.StudentId)) return new(null, BillingFailure.NotFound);
            var ledger = await repository.LedgerAsync(owner, classId, ct);
            if ((ledger?.Version ?? 0) != input.Version) return new(null, BillingFailure.Conflict, "Công nợ đã thay đổi. Vui lòng tải lại trước khi nhận tiền.");
            var charges = await repository.ChargesAsync(owner, classId, ct);
            var reviews = await repository.PaymentReviewsAsync(owner, classId, ct);
            if (Status(source, ledger, charges, reviews) != "ready")
                return new(null, BillingFailure.Conflict, "Phí chưa đầy đủ hoặc có tiền cần đối chiếu sau thay đổi phí. Vui lòng tải lại công nợ.");
            if (ledger is null) { ledger = new TuitionLedger(owner, classId); repository.Add(ledger); }
            var receipt = new Receipt(owner, classId, input, clock.GetUtcNow());
            foreach (var period in PeriodBalance.Calculate(charges, receipts, reviews, await repository.AdjustmentsAsync(owner, classId, ct)).Where(x => x.StudentId == input.StudentId))
                receipt.Allocate(period.Period, period.Debt);
            repository.Add(receipt); ledger.RecordProjection();
            repository.Add(new BillingOutbox(ledger, "receipt.recorded", clock.GetUtcNow()));
            await repository.SaveAsync(ct);
            return new(View(receipt));
        }, ct);
    }

    internal static string Status(BillingClassSource source, TuitionLedger? ledger, IReadOnlyList<SessionCharge> charges, IReadOnlyList<PaymentReview> reviews)
    {
        if ((ledger?.NeedsRefresh == true && source.Sessions.Count > 0)
            || source.Sessions.Any(s => charges.SingleOrDefault(x => x.SessionId == s.Id)?.SourceVersion != s.Version)) return "pending";
        if (charges.Any(x => x.Fees.Any(f => f.AmountVnd is null))) return "missing-rate";
        return reviews.Count > 0 ? "needs-review" : "ready";
    }
    internal static ReceiptView View(Receipt receipt) => new(receipt.Id, receipt.StudentId, Money(receipt.AmountVnd), receipt.ReceivedOn,
        receipt.Note, receipt.RecordedAt, Money(receipt.UnallocatedVnd), receipt.Allocations.OrderBy(x => x.Period).Select(x => new AllocationView(Month(x.Period), Money(x.AmountVnd))).ToArray(), receipt.Status,
        receipt.CorrectionKey is null ? null : new(receipt.CorrectionReason!, receipt.CorrectedAt!.Value, receipt.OwnerId, receipt.ReplacementId,
            System.Text.Json.JsonSerializer.Deserialize<CorrectionResult>(receipt.CorrectionResult!)!));
    private static string Month(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static string Money(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);
}
