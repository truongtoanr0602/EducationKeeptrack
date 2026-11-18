using System.Globalization;
using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Billing.Application;

/// <param name="UsedVnd">Allocated to fees. Money held by reserves is shown apart in <see cref="ReservedVnd"/>.</param>
public record MoneySourceView(Guid ReceiptId, DateOnly ReceivedOn, string? Note, string CollectedVnd, string UsedVnd, string UnusedVnd,
    string ReservedVnd, string AvailableVnd);
public record ReserveSourceView(Guid ReceiptId, DateOnly ReceivedOn, string AmountVnd);
public record ReserveEventView(int Version, string Action, string Reason, string RecordedBy, string AmountVnd, DateTimeOffset At);
public record ReserveRequestView(Guid Id, DateOnly AbsentFrom, DateOnly AbsentTo, string Reason, string? EvidenceNote, string RequestedVnd,
    string Status, int Version, string? DecisionReason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    IReadOnlyList<ReserveSourceView> Sources, IReadOnlyList<ReserveEventView> History, string UsedVnd = "0", string RemainingVnd = "0", bool CanCancel = false);
/// <param name="ApprovedVnd">Approved and not cancelled. Remaining = approved - used.</param>
public record ReserveBalanceView(string ApprovedVnd, string UsedVnd, string RemainingVnd);
public record ReserveUseView(Guid Id, string Month, DateOnly UsedOn, string AmountVnd, string Status, string? ReversalReason, DateTimeOffset CreatedAt, DateTimeOffset? ReversedAt);
/// <param name="ReserveExcessVnd">Used reserve beyond the obligation (fees were reduced later). The teacher reverses it; it is never reused automatically.</param>
public record ReservePeriodView(string Month, string DebtVnd, string ReserveAppliedVnd, string ReserveExcessVnd);
public record ReserveMovementsView(string Month, string OpeningVnd, string ApprovedVnd, string UsedVnd, string CancelledVnd, string CarriedVnd);
public record StudentReserveView(Guid StudentId, string Name, string CollectedVnd, string UsedVnd, string UnusedVnd, string ReservedVnd,
    string AvailableVnd, IReadOnlyList<MoneySourceView> Sources, IReadOnlyList<ReserveRequestView> Requests,
    ReserveBalanceView Reserve, IReadOnlyList<ReserveUseView> Uses, IReadOnlyList<ReservePeriodView> Periods, ReserveMovementsView Movements);
/// <param name="Status">none: no policy has been set, so every case is decided by the teacher with a reason.</param>
public record ReservePolicyView(string Status, string Message);
public record ReserveView(long Version, ReservePolicyView Policy, IReadOnlyList<StudentReserveView> Students);

public sealed class ReserveService(IBillingRepository repository, IClassroomBillingSource classroom, TimeProvider clock)
{
    public static readonly ReservePolicyView NoPolicy = new("none",
        "Chưa có chính sách bảo lưu: số ngày nghỉ tối thiểu, minh chứng, hạn dùng, chuyển hoặc dừng lớp và hoàn tiền chưa được quy định. Hệ thống chỉ kiểm tra tiền có thật, chưa dùng và chưa thuộc bảo lưu khác; mỗi trường hợp do giáo viên quyết định kèm lý do. Bảo lưu không tự hết hạn và không tự trừ vào học phí.");

    private DateOnly TodayInVietnam() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    /// <summary>What a request still holds: approved money minus the active uses that drew from it.</summary>
    private static decimal Remaining(ReserveRequest request, IEnumerable<ReserveUse> uses) =>
        request.Status != "approved" ? 0 : request.RequestedVnd - uses.Where(u => u.Status == "active").SelectMany(u => u.Sources).Where(s => s.RequestId == request.Id).Sum(s => s.AmountVnd);

    public async Task<BillingResult<ReserveView>> ReportAsync(string owner, Guid classId, string? month, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return new(null, BillingFailure.NotFound);
        var today = TodayInVietnam();
        if (!ReserveUseInput.TryPeriod(month ?? today.ToString("yyyy-MM", CultureInfo.InvariantCulture), out var first))
            return new(null, BillingFailure.Invalid, Errors: new() { ["month"] = ["Tháng không hợp lệ (yyyy-MM)."] });
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
        var balances = PeriodBalance.Calculate(snapshot.Sessions, snapshot.Receipts, snapshot.PaymentReviews, snapshot.Adjustments, snapshot.ReserveUses);
        var students = source.Students.Select(student =>
        {
            var active = snapshot.Receipts.Where(x => x.StudentId == student.Id && x.Status == "active").OrderBy(x => x.ReceivedOn).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToArray();
            var mine = requests.Where(x => x.StudentId == student.Id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
            var uses = snapshot.ReserveUses.Where(x => x.StudentId == student.Id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
            var approved = mine.Where(x => x.Status == "approved").ToArray();
            var remaining = approved.Sum(x => Remaining(x, snapshot.ReserveUses));
            var used = uses.Where(x => x.Status == "active").Sum(x => x.AmountVnd);
            var movements = ReserveLedger.For(student.Id, first, mine, uses);
            return new StudentReserveView(student.Id, student.Name, Money(active.Sum(x => x.AmountVnd)), Money(active.Sum(x => x.AmountVnd - x.UnallocatedVnd)),
                Money(active.Sum(x => x.UnallocatedVnd)), Money(active.Sum(x => x.ReservedVnd)), Money(active.Sum(x => x.AvailableForReserveVnd)),
                active.Select(x => new MoneySourceView(x.Id, x.ReceivedOn, x.Note, Money(x.AmountVnd), Money(x.AmountVnd - x.UnallocatedVnd), Money(x.UnallocatedVnd),
                    Money(x.ReservedVnd), Money(x.AvailableForReserveVnd))).ToArray(),
                mine.Select(x => View(x, snapshot.Receipts, snapshot.ReserveUses)).ToArray(),
                new ReserveBalanceView(Money(approved.Sum(x => x.RequestedVnd)), Money(used), Money(remaining)),
                uses.Select(u => new ReserveUseView(u.Id, u.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture), u.UsedOn, Money(u.AmountVnd), u.Status, u.ReversalReason, u.CreatedAt, u.ReversedAt)).ToArray(),
                balances.Where(b => b.StudentId == student.Id && (b.Debt > 0 || b.ReserveApplied > 0 || b.ReserveExcess > 0))
                    .Select(b => new ReservePeriodView(b.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture), Money(b.Debt), Money(b.ReserveApplied), Money(b.ReserveExcess))).ToArray(),
                new ReserveMovementsView(first.ToString("yyyy-MM", CultureInfo.InvariantCulture), Money(movements.Opening), Money(movements.Approved), Money(movements.Used), Money(movements.Cancelled), Money(movements.Carried)));
        }).ToArray();
        return new(new ReserveView(snapshot.Ledger?.Version ?? 0, NoPolicy, students));
    }

    public async Task<BillingResult<ReserveRequestView>> RequestAsync(string owner, Guid classId, ReserveRequestInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<ReserveRequestView>(classId, async () =>
        {
            var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
            var existing = requests.SingleOrDefault(x => x.CreateKey == input.IdempotencyKey);
            // A retry after a lost response returns the original request.
            if (existing is not null) return existing.Matches(input) ? new(View(existing, [], [])) : new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho đề nghị khác.");
            if (UsesKey(requests, input.IdempotencyKey)) return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            if ((await repository.ReserveUsesAsync(owner, classId, ct)).Any(x => x.UseKey == input.IdempotencyKey || x.ReversalKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            var source = await classroom.ReadClassAsync(owner, classId, ct);
            if (source is null || !source.Students.Any(x => x.Id == input.StudentId)) return new(null, BillingFailure.NotFound);
            var receipts = await repository.ReceiptsAsync(owner, classId, ct);
            var available = receipts.Where(x => x.StudentId == input.StudentId).Sum(x => x.AvailableForReserveVnd);
            // Absence alone creates nothing: there has to be money the family actually paid and the student has not used.
            var pending = requests.Where(x => x.StudentId == input.StudentId && x.Status == "pending").Sum(x => x.RequestedVnd);
            if (input.AmountVnd > available - pending)
                return new(null, BillingFailure.Invalid, Errors: new() { ["amountVnd"] = [available == 0
                    ? "Học sinh chưa có tiền đã nhận mà chưa dùng nên chưa thể đề nghị bảo lưu."
                    : $"Chỉ có tối đa {Money(Math.Max(0, available - pending))} đồng chưa dùng và chưa nằm trong bảo lưu hoặc đề nghị khác."] });
            var request = new ReserveRequest(owner, classId, input, clock.GetUtcNow());
            repository.Add(request);
            await repository.SaveAsync(ct);
            return new(View(request, receipts, []));
        }, ct);
    }

    private static bool UsesKey(IEnumerable<ReserveRequest> requests, Guid key) => requests.Any(x => x.DecisionKey == key || x.CancelKey == key);

    public async Task<BillingResult<ReserveRequestView>> DecideAsync(string owner, Guid classId, Guid requestId, ReserveDecisionInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<ReserveRequestView>(classId, async () =>
        {
            var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
            var request = requests.SingleOrDefault(x => x.Id == requestId);
            if (request is null) return new(null, BillingFailure.NotFound);
            var receipts = await repository.ReceiptsAsync(owner, classId, ct);
            var uses = await repository.ReserveUsesAsync(owner, classId, ct);
            // The same command replays its original answer even when the version is stale; any other command on a decided request conflicts.
            if (request.Status != "pending")
                return request.DecisionMatches(input) ? new(View(request, receipts, uses))
                    : new(null, BillingFailure.Conflict, "Đề nghị này đã được quyết định. Vui lòng tải lại.");
            if (requests.Any(x => x.Id != requestId && (x.CreateKey == input.IdempotencyKey || x.DecisionKey == input.IdempotencyKey || x.CancelKey == input.IdempotencyKey))
                || uses.Any(x => x.UseKey == input.IdempotencyKey || x.ReversalKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            if (request.Version != input.Version) return new(null, BillingFailure.Conflict, "Đề nghị đã thay đổi. Vui lòng tải lại trước khi quyết định.");
            var now = clock.GetUtcNow();
            if (input.Decision == "reject") { request.Reject(input, owner, now); await repository.SaveAsync(ct); return new(View(request, receipts, uses)); }

            // Only unused money of this student's active receipts, oldest first, never money another reserve already holds.
            var pool = receipts.Where(x => x.StudentId == request.StudentId && x.AvailableForReserveVnd > 0)
                .OrderBy(x => x.ReceivedOn).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToArray();
            var left = request.RequestedVnd; var taken = new List<(Receipt, decimal)>();
            foreach (var receipt in pool)
            {
                if (left <= 0) break;
                var amount = Math.Min(left, receipt.AvailableForReserveVnd);
                taken.Add((receipt, amount)); left -= amount;
            }
            if (left > 0)
                return new(null, BillingFailure.Conflict, "Tiền chưa dùng không còn đủ để bảo lưu (có thể đã được dùng hoặc đã nằm trong bảo lưu khác). Vui lòng tải lại.");
            request.Approve(taken, input, owner, now);
            var ledger = await repository.LedgerAsync(owner, classId, ct) ?? throw new InvalidOperationException("A class with receipts always has a ledger.");
            ledger.RecordProjection();
            repository.Add(new BillingOutbox(ledger, "reserve.approved", now));
            await repository.SaveAsync(ct);
            return new(View(request, receipts, uses));
        }, ct);
    }

    /// <summary>Takes an approved reserve out of play. Refused while any active use still draws on it, so what was used is reconciled first.</summary>
    public async Task<BillingResult<ReserveRequestView>> CancelAsync(string owner, Guid classId, Guid requestId, ReserveCorrectionInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate(needsVersion: true);
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<ReserveRequestView>(classId, async () =>
        {
            var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
            var request = requests.SingleOrDefault(x => x.Id == requestId);
            if (request is null) return new(null, BillingFailure.NotFound);
            var receipts = await repository.ReceiptsAsync(owner, classId, ct);
            var uses = await repository.ReserveUsesAsync(owner, classId, ct);
            if (request.Status == "cancelled")
                return request.CancelMatches(input) ? new(View(request, receipts, uses)) : new(null, BillingFailure.Conflict, "Bảo lưu này đã được hủy. Vui lòng tải lại.");
            if (request.Status != "approved") return new(null, BillingFailure.Conflict, "Chỉ hủy được bảo lưu đã duyệt.");
            if (requests.Any(x => x.Id != requestId && (x.CreateKey == input.IdempotencyKey || x.DecisionKey == input.IdempotencyKey || x.CancelKey == input.IdempotencyKey))
                || uses.Any(x => x.UseKey == input.IdempotencyKey || x.ReversalKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            if (request.Version != input.Version) return new(null, BillingFailure.Conflict, "Bảo lưu đã thay đổi. Vui lòng tải lại trước khi hủy.");
            var used = request.RequestedVnd - Remaining(request, uses);
            if (used > 0)
                return new(null, BillingFailure.Conflict, $"Bảo lưu này đã được khấu trừ {Money(used)} đồng vào học phí. Hãy hủy các lần khấu trừ đó (kèm lý do) trước khi hủy bảo lưu.");
            var now = clock.GetUtcNow();
            request.Cancel(receipts, input, owner, now);
            var ledger = await repository.LedgerAsync(owner, classId, ct) ?? throw new InvalidOperationException("A class with receipts always has a ledger.");
            ledger.RecordProjection();
            repository.Add(new BillingOutbox(ledger, "reserve.cancelled", now));
            await repository.SaveAsync(ct);
            return new(View(request, receipts, uses));
        }, ct);
    }

    /// <summary>Applies reserve money to one period's remaining obligation: at most what is still payable and at most what the reserve still holds.</summary>
    public async Task<BillingResult<ReserveUseView>> UseAsync(string owner, Guid classId, ReserveUseInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        ReserveUseInput.TryPeriod(input.Month, out var period);
        return await repository.WithClassLockAsync<ReserveUseView>(classId, async () =>
        {
            var uses = await repository.ReserveUsesAsync(owner, classId, ct);
            var existing = uses.SingleOrDefault(x => x.UseKey == input.IdempotencyKey);
            // A retry after a lost response returns the original use, even with a stale ledger version.
            if (existing is not null) return existing.Matches(input) ? new(UseView(existing)) : new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho lần khấu trừ khác.");
            var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
            if (UsesKey(requests, input.IdempotencyKey) || requests.Any(x => x.CreateKey == input.IdempotencyKey) || uses.Any(x => x.ReversalKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            var source = await classroom.ReadClassAsync(owner, classId, ct);
            if (source is null || !source.Students.Any(x => x.Id == input.StudentId)) return new(null, BillingFailure.NotFound);
            var ledger = await repository.LedgerAsync(owner, classId, ct);
            if ((ledger?.Version ?? 0) != input.Version) return new(null, BillingFailure.Conflict, "Công nợ đã thay đổi. Vui lòng tải lại trước khi khấu trừ bảo lưu.");
            var charges = await repository.ChargesAsync(owner, classId, ct);
            var reviews = await repository.PaymentReviewsAsync(owner, classId, ct);
            if (PaymentService.Status(source, ledger, charges, reviews) != "ready")
                return new(null, BillingFailure.Conflict, "Phí chưa đầy đủ hoặc có tiền cần đối chiếu sau thay đổi phí. Vui lòng tải lại công nợ.");
            var receipts = await repository.ReceiptsAsync(owner, classId, ct);
            var adjustments = await repository.AdjustmentsAsync(owner, classId, ct);
            var debt = PeriodBalance.Calculate(charges, receipts, reviews, adjustments, uses).SingleOrDefault(x => x.StudentId == input.StudentId && x.Period == period)?.Debt ?? 0;
            if (input.AmountVnd > debt)
                return new(null, BillingFailure.Invalid, Errors: new() { ["amountVnd"] = [debt == 0
                    ? "Kỳ này không còn nghĩa vụ phải trả nên không khấu trừ bảo lưu."
                    : $"Chỉ khấu trừ tối đa {Money(debt)} đồng, là phần học phí của kỳ này còn phải trả."] });
            var approved = requests.Where(x => x.StudentId == input.StudentId && x.Status == "approved").OrderBy(x => x.DecidedAt).ThenBy(x => x.Id).ToArray();
            var left = input.AmountVnd; var taken = new List<(Guid, decimal)>();
            foreach (var request in approved)
            {
                if (left <= 0) break;
                var amount = Math.Min(left, Remaining(request, uses));
                if (amount <= 0) continue;
                taken.Add((request.Id, amount)); left -= amount;
            }
            if (left > 0)
                return new(null, BillingFailure.Invalid, Errors: new() { ["amountVnd"] = [$"Bảo lưu còn lại chỉ có {Money(input.AmountVnd - left)} đồng."] });
            var now = clock.GetUtcNow();
            var use = new ReserveUse(owner, classId, input.StudentId, period, input.UsedOn ?? TodayInVietnam(), input.IdempotencyKey, taken, now);
            repository.Add(use);
            ledger!.RecordProjection();
            repository.Add(new BillingOutbox(ledger, "reserve.used", now));
            await repository.SaveAsync(ct);
            return new(UseView(use));
        }, ct);
    }

    /// <summary>Undoes one use: the debt of that period comes back and the reserve holds the money again.</summary>
    public async Task<BillingResult<ReserveUseView>> ReverseUseAsync(string owner, Guid classId, Guid useId, ReserveCorrectionInput input, CancellationToken ct)
    {
        if (await classroom.ReadClassAsync(owner, classId, ct) is null) return new(null, BillingFailure.NotFound);
        var errors = input.Validate(needsVersion: false);
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        return await repository.WithClassLockAsync<ReserveUseView>(classId, async () =>
        {
            var uses = await repository.ReserveUsesAsync(owner, classId, ct);
            var use = uses.SingleOrDefault(x => x.Id == useId);
            if (use is null) return new(null, BillingFailure.NotFound);
            if (use.Status == "reversed")
                return use.ReversalMatches(input) ? new(UseView(use)) : new(null, BillingFailure.Conflict, "Lần khấu trừ này đã được hủy. Vui lòng tải lại.");
            var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
            if (uses.Any(x => x.Id != useId && (x.UseKey == input.IdempotencyKey || x.ReversalKey == input.IdempotencyKey))
                || requests.Any(x => x.CreateKey == input.IdempotencyKey || x.DecisionKey == input.IdempotencyKey || x.CancelKey == input.IdempotencyKey))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            var now = clock.GetUtcNow();
            use.Reverse(input, now);
            var ledger = await repository.LedgerAsync(owner, classId, ct) ?? throw new InvalidOperationException("A class with receipts always has a ledger.");
            ledger.RecordProjection();
            repository.Add(new BillingOutbox(ledger, "reserve.use-reversed", now));
            await repository.SaveAsync(ct);
            return new(UseView(use));
        }, ct);
    }

    private static ReserveUseView UseView(ReserveUse u) => new(u.Id, u.Period.ToString("yyyy-MM", CultureInfo.InvariantCulture), u.UsedOn, Money(u.AmountVnd), u.Status, u.ReversalReason, u.CreatedAt, u.ReversedAt);

    private static ReserveRequestView View(ReserveRequest x, IReadOnlyList<Receipt> receipts, IReadOnlyList<ReserveUse> uses)
    {
        var remaining = Remaining(x, uses);
        return new(x.Id, x.AbsentFrom, x.AbsentTo, x.Reason, x.EvidenceNote, Money(x.RequestedVnd),
            x.Status, x.Version, x.DecisionReason ?? x.CancelReason, x.CreatedAt, x.DecidedAt,
            x.Sources.Select(s => new ReserveSourceView(s.ReceiptId, receipts.FirstOrDefault(r => r.Id == s.ReceiptId)?.ReceivedOn ?? default, Money(s.AmountVnd))).OrderBy(s => s.ReceivedOn).ThenBy(s => s.ReceiptId).ToArray(),
            x.Events.OrderBy(e => e.Version).Select(e => new ReserveEventView(e.Version, e.Action, e.Reason, e.ActorId, Money(e.AmountVnd), e.At)).ToArray(),
            Money(x.Status == "approved" ? x.RequestedVnd - remaining : 0), Money(remaining), x.Status == "approved" && remaining == x.RequestedVnd);
    }
    private static string Money(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);
}
