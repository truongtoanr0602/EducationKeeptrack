using System.Text.Json;
using System.Text.Json.Serialization;
using EducationKeeptrack.Billing.Domain;

namespace EducationKeeptrack.Billing.Application;

public record ReceiptCorrectionInput(string Action, string? Reason, Guid IdempotencyKey, long Version,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? AmountVnd = null,
    DateOnly? ReceivedOn = null, string? Note = null)
{
    public Dictionary<string, string[]> Validate(Guid studentId)
    {
        var errors = new Dictionary<string, string[]>();
        if (Action is not ("cancel" or "replace")) errors["action"] = ["Chọn sửa hoặc hủy khoản nhận tiền."];
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Length > 1000) errors["reason"] = ["Nhập lý do sửa/hủy, tối đa 1000 ký tự."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        if (Version < 0) errors["version"] = ["Phiên bản không hợp lệ."];
        if (Action == "replace")
            foreach (var error in new ReceiptInput(studentId, AmountVnd ?? 0, ReceivedOn ?? default, Note, IdempotencyKey, Version).Validate()) errors[error.Key] = error.Value;
        if (Action == "cancel" && (AmountVnd is not null || ReceivedOn is not null || Note is not null))
            errors["action"] = ["Lệnh hủy không nhận thông tin thay thế."];
        return errors;
    }
}

public record ReceiptState(Guid Id, string AmountVnd, DateOnly ReceivedOn, string? Note, string Status,
    string UnallocatedVnd, IReadOnlyList<AllocationView> Allocations);
public record CorrectionResult(Guid ReceiptId, Guid? ReplacementId, long Version,
    IReadOnlyList<ReceiptState> Before, IReadOnlyList<ReceiptState> After);
public record ReceiptCorrectionView(string Reason, DateTimeOffset RecordedAt, string RecordedBy,
    Guid? ReplacementId, CorrectionResult Changes);

public sealed partial class PaymentService
{
    public async Task<BillingResult<CorrectionResult>> CorrectAsync(string owner, Guid classId, Guid receiptId, ReceiptCorrectionInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        return await repository.WithClassLockAsync<CorrectionResult>(classId, async () =>
        {
            var receipts = (await repository.ReceiptsAsync(owner, classId, ct)).ToList();
            var original = receipts.SingleOrDefault(x => x.Id == receiptId);
            if (original is null) return new(null, BillingFailure.NotFound);
            var errors = input.Validate(original.StudentId);
            if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
            // Expected version is deliberately excluded from replay identity.
            var request = JsonSerializer.Serialize(input with { Reason = input.Reason!.Trim(), Note = input.Note?.Trim(), Version = 0 });
            var replay = receipts.SingleOrDefault(x => x.CorrectionKey == input.IdempotencyKey);
            if (replay is not null)
                return replay.Id == receiptId && replay.CorrectionRequest == request
                    ? new(JsonSerializer.Deserialize<CorrectionResult>(replay.CorrectionResult!)!)
                    : new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            if (receipts.Any(x => x.IdempotencyKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho khoản nhận tiền.");
            var ledger = await repository.LedgerAsync(owner, classId, ct);
            if (ledger is null || ledger.Version != input.Version || original.Status != "active")
                return new(null, BillingFailure.Conflict, "Công nợ hoặc giao dịch đã thay đổi. Vui lòng tải lại.");
            var source = await classroom.ReadClassAsync(owner, classId, ct);
            if (source is null) return new(null, BillingFailure.NotFound);
            var charges = await repository.ChargesAsync(owner, classId, ct);
            var reviews = await repository.PaymentReviewsAsync(owner, classId, ct);
            if (Status(source, ledger, charges, reviews) is "pending" or "missing-rate")
                return new(null, BillingFailure.Conflict, "Phí chưa đầy đủ. Vui lòng tải lại công nợ.");
            var adjustments = await repository.AdjustmentsAsync(owner, classId, ct);
            var affected = receipts.Where(x => x.StudentId == original.StudentId).ToList();
            var before = States(affected);
            var now = clock.GetUtcNow();
            Receipt? replacement = input.Action == "replace"
                ? new Receipt(owner, classId, new(original.StudentId, input.AmountVnd!.Value, input.ReceivedOn!.Value, input.Note, input.IdempotencyKey, input.Version), now)
                : null;
            var carriedCredit = replacement is null ? 0 : Math.Min(original.UnallocatedVnd, replacement.AmountVnd);
            // Preserve period holds against surviving historical allocations. Holds have no
            // receipt provenance, so reversing a receipt must not guess which held funds to release.
            var heldLeft = reviews.Where(x => x.StudentId == original.StudentId).ToDictionary(x => x.Period, x => x.AmountVnd);
            var reserved = new Dictionary<Guid, Dictionary<DateOnly, decimal>>();
            foreach (var receipt in affected.Where(x => x.Status == "active" && (x.Id != original.Id || replacement is not null)).OrderBy(x => x.RecordedAt).ThenBy(x => x.Id))
            {
                var held = new Dictionary<DateOnly, decimal>();
                var backing = receipt == original ? replacement!.AmountVnd - carriedCredit : receipt.AmountVnd - receipt.UnallocatedVnd;
                foreach (var allocation in receipt.Allocations.OrderBy(x => x.Period))
                {
                    var amount = Math.Min(backing, Math.Min(allocation.AmountVnd, heldLeft.GetValueOrDefault(allocation.Period)));
                    if (amount > 0) { held[allocation.Period] = amount; heldLeft[allocation.Period] -= amount; backing -= amount; }
                }
                reserved[receipt == original ? replacement!.Id : receipt.Id] = held;
            }
            original.Correct(input.Action, input.IdempotencyKey, input.Reason!.Trim(), now, replacement?.Id, request);
            if (replacement is not null) { repository.Add(replacement); receipts.Add(replacement); affected.Add(replacement); }
            var active = affected.Where(x => x.Status == "active").OrderBy(x => x.ReceivedOn).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToArray();
            var remainingHolds = active.SelectMany(x => reserved.GetValueOrDefault(x.Id, []).Select(h => h))
                .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(h => h.Value));
            foreach (var review in reviews.Where(x => x.StudentId == original.StudentId))
            {
                var amount = remainingHolds.GetValueOrDefault(review.Period);
                if (amount == 0) repository.Remove(review); else review.ReduceTo(amount);
            }
            var debts = PeriodBalance.Calculate(charges, [], [], adjustments).Where(x => x.StudentId == original.StudentId).ToDictionary(x => x.Period, x => x.Fees);
            foreach (var receipt in active)
            {
                var allocations = reserved.GetValueOrDefault(receipt.Id, []);
                // Existing unallocated credit stays unallocated; a correction cannot spend it implicitly.
                var available = receipt.AmountVnd - (receipt == replacement ? carriedCredit : receipt.UnallocatedVnd) - allocations.Values.Sum();
                foreach (var period in debts.Keys.Order().ToArray())
                {
                    var amount = Math.Min(available, debts[period]);
                    if (amount <= 0) continue;
                    allocations[period] = allocations.GetValueOrDefault(period) + amount;
                    debts[period] -= amount; available -= amount;
                }
                receipt.Reallocate(allocations);
            }
            ledger.RecordProjection();
            var result = new CorrectionResult(original.Id, replacement?.Id, ledger.Version, before, States(affected));
            original.SaveCorrectionResult(JsonSerializer.Serialize(result));
            repository.Add(new BillingOutbox(ledger, "receipt.corrected", now));
            await repository.SaveAsync(ct);
            return new(result);
        }, ct);
    }

    private static ReceiptState[] States(IEnumerable<Receipt> receipts) => receipts.OrderBy(x => x.Id).Select(x =>
        new ReceiptState(x.Id, Money(x.AmountVnd), x.ReceivedOn, x.Note, x.Status, Money(x.UnallocatedVnd),
            x.Allocations.OrderBy(a => a.Period).Select(a => new AllocationView(Month(a.Period), Money(a.AmountVnd))).ToArray())).ToArray();
}
