using System.Globalization;
using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Billing.Application;

public record AdjustmentView(Guid Id, Guid StudentId, string Month, string Direction, string AmountVnd,
    string Reason, string RecordedBy, DateTimeOffset RecordedAt, long Version);

public sealed class AdjustmentService(IBillingRepository repository, IClassroomBillingSource classroom, TimeProvider clock)
{
    public async Task<BillingResult<AdjustmentView>> AddAsync(string owner, Guid classId, AdjustmentInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<AdjustmentView>(classId, async () =>
        {
            var adjustments = (await repository.AdjustmentsAsync(owner, classId, ct)).ToList();
            var existing = adjustments.SingleOrDefault(x => x.IdempotencyKey == input.IdempotencyKey);
            if (existing is not null) return existing.Matches(input) ? new(View(existing))
                : new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho điều chỉnh khác.");
            var source = await classroom.ReadClassAsync(owner, classId, ct);
            if (source is null || !source.Students.Any(x => x.Id == input.StudentId)) return new(null, BillingFailure.NotFound);
            var ledger = await repository.LedgerAsync(owner, classId, ct);
            if ((ledger?.Version ?? 0) != input.Version) return new(null, BillingFailure.Conflict, "Bảng phí đã thay đổi. Vui lòng tải lại trước khi điều chỉnh.");
            var charges = await repository.ChargesAsync(owner, classId, ct);
            var reviews = await repository.PaymentReviewsAsync(owner, classId, ct);
            if (PaymentService.Status(source, ledger, charges, reviews) is "pending" or "missing-rate")
                return new(null, BillingFailure.Conflict, "Phí chưa đầy đủ. Vui lòng chờ cập nhật bảng phí.");
            var receipts = await repository.ReceiptsAsync(owner, classId, ct);
            var period = DateOnly.ParseExact(input.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var fees = PeriodBalance.Calculate(charges, receipts, reviews, adjustments)
                .SingleOrDefault(x => x.StudentId == input.StudentId && x.Period == period)?.Fees ?? 0;
            if (input.Direction == "decrease" && input.AmountVnd > fees)
                return new(null, BillingFailure.Invalid, Errors: new() { ["amountVnd"] = ["Khoản giảm không được vượt nghĩa vụ học phí của kỳ."] });
            if (ledger is null) { ledger = new TuitionLedger(owner, classId); repository.Add(ledger); }
            ledger.RecordProjection();
            var adjustment = new FeeAdjustment(owner, classId, input, ledger.Version, clock.GetUtcNow());
            repository.Add(adjustment); adjustments.Add(adjustment);
            var balances = PeriodBalance.Calculate(charges, receipts, reviews, adjustments);
            foreach (var review in PaymentReview.HoldExcess(ledger, balances, reviews, clock.GetUtcNow())) repository.Add(review);
            repository.Add(new BillingOutbox(ledger, "adjustment.recorded", clock.GetUtcNow()));
            await repository.SaveAsync(ct);
            return new(View(adjustment));
        }, ct);
    }

    public static AdjustmentView View(FeeAdjustment adjustment) => new(adjustment.Id, adjustment.StudentId,
        adjustment.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture), adjustment.Direction,
        adjustment.AmountVnd.ToString("0", CultureInfo.InvariantCulture), adjustment.Reason,
        adjustment.RecordedBy, adjustment.RecordedAt, adjustment.Version);
}
