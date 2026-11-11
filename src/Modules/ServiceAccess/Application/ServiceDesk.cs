using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.ServiceAccess.Application;

// Teacher-facing service profile: status, instructions and payment reconciliation requests.
public sealed class ServiceDesk(ServiceAccessDatabase db, IAccountAccess access, AiQuotaLedger ledger, TimeProvider clock)
{
    public async Task<object> ProfileAsync(string accountId, CancellationToken ct)
    {
        var status = await access.GetAsync(accountId, ct);
        var instructions = await db.Instructions.AsNoTracking().SingleOrDefaultAsync(ct);
        var requests = await db.PaymentRequests.AsNoTracking().Where(x => x.AccountId == accountId).OrderByDescending(x => x.SubmittedAt).ToListAsync(ct);
        var periods = await db.Periods.AsNoTracking().Where(x => x.AccountId == accountId).OrderBy(x => x.StartsAt).ToListAsync(ct);
        var now = clock.GetUtcNow();
        var aiEnabled = await db.Accounts.AsNoTracking().Where(x => x.AccountId == accountId).Select(x => x.AiEnabled).SingleOrDefaultAsync(ct);
        var current = periods.FirstOrDefault(p => p.Contains(now));
        return new
        {
            status = status.Status,
            access = status,
            instructions = new { bankDetails = instructions?.BankDetails ?? "", contact = instructions?.Contact ?? "" },
            requests = requests.Select(TeacherRequest),
            periods = periods.Select(p => new { p.Id, p.StartsAt, p.EndsAt, p.Label, current = p.Contains(now), upcoming = p.StartsAt > now }),
            // Balances of the current period only; a new period never inherits unused turns.
            ai = new { enabled = aiEnabled, canUseAi = status.CanUseAi, quotas = await ledger.BalancesAsync(current, ct) }
        };
    }

    public async Task<Outcome> SubmitAsync(string accountId, RequestInput input, CancellationToken ct)
    {
        var errors = input.Validate();
        if (errors is not null) return errors;
        var status = await access.GetAsync(accountId, ct);
        if (status.Status is "blocked" or "unverified")
            return new(403, new { message = status.Status == "unverified" ? "Xác minh email trước khi gửi đối chiếu thanh toán." : "Tài khoản hiện không được truy cập.", status = status.Status });
        var key = input.IdempotencyKey!.Trim();
        var existing = await db.PaymentRequests.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId && x.IdempotencyKey == key, ct);
        if (existing is not null) return Same(existing, input) ? Outcome.Ok(TeacherRequest(existing)) : Outcome.Conflict("Mã gửi lặp đã dùng cho nội dung khác.");
        if (await db.PaymentRequests.AnyAsync(x => x.AccountId == accountId && x.Status == PaymentRequestStatus.Submitted, ct))
            return Outcome.Conflict("Bạn đã có một yêu cầu đối chiếu đang chờ Admin xử lý.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(accountId, ct);
        var request = new PaymentRequest
        {
            Id = Guid.NewGuid(), AccountId = accountId, IdempotencyKey = key, TransactionCode = input.TransactionCode!.Trim(),
            Note = input.Note?.Trim() ?? "", SubmittedAt = Moment.Of(clock.GetUtcNow())
        };
        db.PaymentRequests.Add(request);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Outcome.Conflict("Yêu cầu đã được gửi trước đó. Vui lòng tải lại.");
        }
        return new(201, TeacherRequest(request));
    }

    private static bool Same(PaymentRequest existing, RequestInput input) =>
        existing.TransactionCode == input.TransactionCode!.Trim() && existing.Note == (input.Note?.Trim() ?? "");

    internal static object TeacherRequest(PaymentRequest x) => new
    {
        x.Id, x.TransactionCode, x.Note, x.SubmittedAt, status = x.Status.ToString().ToLowerInvariant(), x.DecisionReason, x.DecidedAt
    };

    public record RequestInput(string? TransactionCode, string? Note, string? IdempotencyKey)
    {
        public Outcome? Validate()
        {
            if (string.IsNullOrWhiteSpace(TransactionCode) || TransactionCode.Trim().Length > 100) return Outcome.Invalid("transactionCode", "Nhập mã giao dịch (tối đa 100 ký tự).");
            if (Note?.Trim().Length > 500) return Outcome.Invalid("note", "Ghi chú tối đa 500 ký tự.");
            if (string.IsNullOrWhiteSpace(IdempotencyKey) || IdempotencyKey.Trim().Length > 100) return Outcome.Invalid("idempotencyKey", "Thiếu mã chống gửi lặp.");
            return null;
        }
    }
}
