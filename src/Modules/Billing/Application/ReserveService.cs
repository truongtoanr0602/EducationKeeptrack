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
    IReadOnlyList<ReserveSourceView> Sources, IReadOnlyList<ReserveEventView> History);
public record StudentReserveView(Guid StudentId, string Name, string CollectedVnd, string UsedVnd, string UnusedVnd, string ReservedVnd,
    string AvailableVnd, IReadOnlyList<MoneySourceView> Sources, IReadOnlyList<ReserveRequestView> Requests);
/// <param name="Status">none: no policy has been set, so every case is decided by the teacher with a reason.</param>
public record ReservePolicyView(string Status, string Message);
public record ReserveView(long Version, ReservePolicyView Policy, IReadOnlyList<StudentReserveView> Students);

public sealed class ReserveService(IBillingRepository repository, IClassroomBillingSource classroom, TimeProvider clock)
{
    public static readonly ReservePolicyView NoPolicy = new("none",
        "Chưa có chính sách bảo lưu: số ngày nghỉ tối thiểu, minh chứng, hạn dùng, chuyển hoặc dừng lớp và hoàn tiền chưa được quy định. Hệ thống chỉ kiểm tra tiền có thật, chưa dùng và chưa thuộc bảo lưu khác; mỗi trường hợp do giáo viên quyết định kèm lý do.");

    public async Task<BillingResult<ReserveView>> ReportAsync(string owner, Guid classId, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return new(null, BillingFailure.NotFound);
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        var requests = await repository.ReserveRequestsAsync(owner, classId, ct);
        var students = source.Students.Select(student =>
        {
            var active = snapshot.Receipts.Where(x => x.StudentId == student.Id && x.Status == "active").OrderBy(x => x.ReceivedOn).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToArray();
            var mine = requests.Where(x => x.StudentId == student.Id).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
            return new StudentReserveView(student.Id, student.Name, Money(active.Sum(x => x.AmountVnd)), Money(active.Sum(x => x.AmountVnd - x.UnallocatedVnd)),
                Money(active.Sum(x => x.UnallocatedVnd)), Money(active.Sum(x => x.ReservedVnd)), Money(active.Sum(x => x.AvailableForReserveVnd)),
                active.Select(x => new MoneySourceView(x.Id, x.ReceivedOn, x.Note, Money(x.AmountVnd), Money(x.AmountVnd - x.UnallocatedVnd), Money(x.UnallocatedVnd),
                    Money(x.ReservedVnd), Money(x.AvailableForReserveVnd))).ToArray(),
                mine.Select(x => View(x, snapshot.Receipts)).ToArray());
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
            if (existing is not null) return existing.Matches(input) ? new(View(existing, [])) : new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho đề nghị khác.");
            if (requests.Any(x => x.DecisionKey == input.IdempotencyKey)) return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho quyết định.");
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
            return new(View(request, receipts));
        }, ct);
    }

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
            // The same command replays its original answer even when the version is stale; any other command on a decided request conflicts.
            if (request.Status != "pending")
                return request.DecisionMatches(input) ? new(View(request, receipts))
                    : new(null, BillingFailure.Conflict, "Đề nghị này đã được quyết định. Vui lòng tải lại.");
            if (requests.Any(x => x.Id != requestId && (x.CreateKey == input.IdempotencyKey || x.DecisionKey == input.IdempotencyKey)))
                return new(null, BillingFailure.Conflict, "Khóa chống lặp đã dùng cho thao tác khác.");
            if (request.Version != input.Version) return new(null, BillingFailure.Conflict, "Đề nghị đã thay đổi. Vui lòng tải lại trước khi quyết định.");
            var now = clock.GetUtcNow();
            if (input.Decision == "reject") { request.Reject(input, owner, now); await repository.SaveAsync(ct); return new(View(request, receipts)); }

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
            return new(View(request, receipts));
        }, ct);
    }

    private static ReserveRequestView View(ReserveRequest x, IReadOnlyList<Receipt> receipts) => new(x.Id, x.AbsentFrom, x.AbsentTo, x.Reason, x.EvidenceNote, Money(x.RequestedVnd),
        x.Status, x.Version, x.DecisionReason, x.CreatedAt, x.DecidedAt,
        x.Sources.Select(s => new ReserveSourceView(s.ReceiptId, receipts.FirstOrDefault(r => r.Id == s.ReceiptId)?.ReceivedOn ?? default, Money(s.AmountVnd))).OrderBy(s => s.ReceivedOn).ThenBy(s => s.ReceiptId).ToArray(),
        x.Events.OrderBy(e => e.Version).Select(e => new ReserveEventView(e.Version, e.Action, e.Reason, e.ActorId, Money(e.AmountVnd), e.At)).ToArray());
    private static string Money(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);
}
