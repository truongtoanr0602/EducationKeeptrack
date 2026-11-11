using System.Text.Json;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Application;

public sealed record Actor(string Id, string Email);

// Admin workflows. Every mutation records actor/time/reason in history and checks expected versions.
public sealed class ServiceAdministration(ServiceAccessDatabase db, IAccountDirectory directory, IAccountAccess access, AiQuotaLedger ledger, TimeProvider clock)
{
    public const int MaxDays = 3660;

    public async Task<IReadOnlyList<object>> TeachersAsync(CancellationToken ct)
    {
        var rows = new List<object>();
        foreach (var teacher in await directory.ListTeachersAsync(ct))
        {
            var status = await access.GetAsync(teacher.Id, ct);
            var open = await db.PaymentRequests.AnyAsync(x => x.AccountId == teacher.Id && x.Status == PaymentRequestStatus.Submitted, ct);
            var lastEnd = await db.Periods.Where(x => x.AccountId == teacher.Id).MaxAsync(x => (DateTimeOffset?)x.EndsAt, ct);
            rows.Add(new { teacher.Id, teacher.Email, teacher.EmailConfirmed, teacher.IsBlocked, teacher.RegisteredAt, status = status.Status, openRequest = open, accessEndsAt = lastEnd });
        }
        return rows;
    }

    public async Task<object?> TeacherAsync(string id, CancellationToken ct)
    {
        var teacher = await directory.FindTeacherAsync(id, ct);
        if (teacher is null) return null;
        var status = await access.GetAsync(id, ct);
        var now = clock.GetUtcNow();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == id, ct);
        var current = await db.Periods.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == id && x.StartsAt <= now && x.EndsAt > now, ct);
        return new
        {
            teacher.Id, teacher.Email, teacher.EmailConfirmed, teacher.IsBlocked, teacher.RegisteredAt, status = status.Status,
            aiEnabled = account?.AiEnabled ?? false, accountVersion = account?.Version ?? 0,
            quotas = await ledger.BalancesAsync(current, ct),
            requests = (await db.PaymentRequests.AsNoTracking().Where(x => x.AccountId == id).OrderByDescending(x => x.SubmittedAt).ToListAsync(ct))
                .Select(x => new { x.Id, x.TransactionCode, x.Note, x.SubmittedAt, status = x.Status.ToString().ToLowerInvariant(), x.VerificationNote, x.DecisionReason, x.DecidedBy, x.DecidedAt, x.PeriodId, x.Version }),
            periods = (await db.Periods.AsNoTracking().Where(x => x.AccountId == id).OrderBy(x => x.StartsAt).ToListAsync(ct))
                .Select(p => new { p.Id, p.StartsAt, p.EndsAt, p.Label, p.Reason, p.CreatedBy, p.CreatedAt, current = p.Contains(now), upcoming = p.StartsAt > now, p.Version, p.GradingQuota, p.CommentQuota }),
            history = (await db.History.AsNoTracking().Where(x => x.AccountId == id).OrderByDescending(x => x.At).Take(200).ToListAsync(ct))
                .Select(x => new { x.Action, actor = x.ActorEmail, x.Reason, x.Details, x.At })
        };
    }

    public async Task<object> InstructionsAsync(CancellationToken ct)
    {
        var item = await db.Instructions.AsNoTracking().SingleOrDefaultAsync(ct);
        return new { bankDetails = item?.BankDetails ?? "", contact = item?.Contact ?? "", version = item?.Version ?? 0, updatedAt = item?.UpdatedAt, updatedBy = item?.UpdatedBy };
    }

    public async Task<Outcome> SaveInstructionsAsync(Actor actor, InstructionsInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.BankDetails) || input.BankDetails.Trim().Length > 1000) return Outcome.Invalid("bankDetails", "Nhập hướng dẫn chuyển khoản (tối đa 1000 ký tự).");
        if (input.Contact?.Trim().Length > 500) return Outcome.Invalid("contact", "Thông tin liên hệ tối đa 500 ký tự.");
        var item = await db.Instructions.SingleOrDefaultAsync(ct);
        if ((item?.Version ?? 0) != input.Version) return Outcome.Conflict("Hướng dẫn đã được cập nhật bởi người khác. Vui lòng tải lại.");
        if (item is null) db.Instructions.Add(item = new PaymentInstructions());
        item.BankDetails = input.BankDetails.Trim();
        item.Contact = input.Contact?.Trim() ?? "";
        item.UpdatedAt = clock.GetUtcNow();
        item.UpdatedBy = actor.Email;
        Record(actor, null, "instructions.updated", null, null);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Outcome.Conflict("Hướng dẫn đã được cập nhật bởi người khác. Vui lòng tải lại."); }
        return Outcome.Ok(await InstructionsAsync(ct));
    }

    public async Task<Outcome> VerifyRequestAsync(Actor actor, Guid requestId, NoteInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Note) || input.Note.Trim().Length > 500) return Outcome.Invalid("note", "Nhập thông tin đối chiếu (tối đa 500 ký tự).");
        var request = await db.PaymentRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Outcome.NotFound;
        if (request.Status != PaymentRequestStatus.Submitted || request.Version != input.Version) return Outcome.Conflict("Yêu cầu đã thay đổi hoặc đã được xử lý. Vui lòng tải lại.");
        request.VerificationNote = input.Note.Trim();
        Record(actor, request.AccountId, "request.verified", null, new { requestId, note = request.VerificationNote });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Outcome.Conflict("Yêu cầu đã thay đổi. Vui lòng tải lại."); }
        return Outcome.Ok(new { request.Id, request.Version });
    }

    public async Task<Outcome> PreviewAsync(string accountId, int days, CancellationToken ct)
    {
        if (days is < 1 or > MaxDays) return Outcome.Invalid("days", $"Số ngày phải từ 1 đến {MaxDays}.");
        var blocked = await GrantBlockerAsync(accountId, ct);
        if (blocked is not null) return blocked;
        var start = await db.NextStartAsync(accountId, clock.GetUtcNow(), ct);
        return Outcome.Ok(new { startsAt = start, endsAt = start.AddDays(days) });
    }

    public async Task<Outcome> ApproveAsync(Actor actor, Guid requestId, ApproveInput input, CancellationToken ct)
    {
        var invalid = input.Validate();
        if (invalid is not null) return invalid;
        var accountId = await db.PaymentRequests.Where(x => x.Id == requestId).Select(x => x.AccountId).SingleOrDefaultAsync(ct);
        if (accountId is null) return Outcome.NotFound;
        var blocked = await GrantBlockerAsync(accountId, ct);
        if (blocked is not null) return blocked;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(accountId, ct);
        var request = await db.PaymentRequests.SingleAsync(x => x.Id == requestId, ct);
        if (request.Status != PaymentRequestStatus.Submitted || request.Version != input.Version)
            return Outcome.Conflict("Yêu cầu đã được xử lý hoặc đã thay đổi. Vui lòng tải lại.");
        var created = await AddPeriodAsync(actor, accountId, input.Days, input.Label, input.ExpectedStartsAt, input.Reason, requestId, new(input.Grading, input.Comment), ct);
        if (created.Status != 200) return created;
        var period = (ServicePeriod)created.Body!;
        request.Status = PaymentRequestStatus.Approved;
        request.DecidedAt = clock.GetUtcNow();
        request.DecidedBy = actor.Email;
        request.PeriodId = period.Id;
        Record(actor, accountId, "request.approved", input.Reason, new { requestId, periodId = period.Id, period.StartsAt, period.EndsAt, input.Days, input.Label, period.GradingQuota, period.CommentQuota });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Ok(new { period.Id, period.StartsAt, period.EndsAt });
    }

    public async Task<Outcome> RejectAsync(Actor actor, Guid requestId, ReasonInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 500) return Outcome.Invalid("reason", "Nhập lý do từ chối (tối đa 500 ký tự).");
        var request = await db.PaymentRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return Outcome.NotFound;
        if (request.Status != PaymentRequestStatus.Submitted || request.Version != input.Version) return Outcome.Conflict("Yêu cầu đã được xử lý hoặc đã thay đổi. Vui lòng tải lại.");
        request.Status = PaymentRequestStatus.Rejected;
        request.DecisionReason = input.Reason.Trim();
        request.DecidedAt = clock.GetUtcNow();
        request.DecidedBy = actor.Email;
        Record(actor, request.AccountId, "request.rejected", request.DecisionReason, new { requestId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Outcome.Conflict("Yêu cầu đã thay đổi. Vui lòng tải lại."); }
        return Outcome.Ok(new { request.Id, request.Version });
    }

    // Caller holds the account lock. Body is the new period on success.
    internal async Task<Outcome> AddPeriodAsync(Actor actor, string accountId, int days, string? label, DateTimeOffset expectedStart, string? reason, Guid? requestId, Quotas quotas, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var start = await db.NextStartAsync(accountId, now, ct);
        if (Moment.Of(expectedStart) != start)
            return Outcome.Conflict("Ngày bắt đầu đã thay đổi so với bản xem trước. Vui lòng xem trước lại.", new { startsAt = start, endsAt = start.AddDays(days) });
        var period = new ServicePeriod
        {
            Id = Guid.NewGuid(), AccountId = accountId, StartsAt = start, EndsAt = start.AddDays(days), Label = label?.Trim() ?? "",
            SourceRequestId = requestId, CreatedBy = actor.Email, CreatedAt = now, Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            GradingQuota = quotas.Grading, CommentQuota = quotas.Comment
        };
        db.Periods.Add(period);
        return Outcome.Ok(period);
    }

    internal async Task<Outcome?> GrantBlockerAsync(string accountId, CancellationToken ct)
    {
        var teacher = await directory.FindTeacherAsync(accountId, ct);
        if (teacher is null) return Outcome.NotFound;
        if (!teacher.EmailConfirmed) return Outcome.Conflict("Giáo viên chưa xác minh email nên chưa thể cấp quyền.");
        if (teacher.IsBlocked) return Outcome.Conflict("Tài khoản đang bị khóa. Mở khóa trước khi cấp hoặc gia hạn.");
        return null;
    }

    internal void Record(Actor actor, string? accountId, string action, string? reason, object? details) =>
        db.History.Add(new HistoryEntry
        {
            Id = Guid.NewGuid(), AccountId = accountId, ActorId = actor.Id, ActorEmail = actor.Email, Action = action,
            Reason = reason, Details = details is null ? null : JsonSerializer.Serialize(details), At = clock.GetUtcNow()
        });

    public record InstructionsInput(string? BankDetails, string? Contact, uint Version);
    public record NoteInput(string? Note, uint Version);
    public record ReasonInput(string? Reason, uint Version);
    // Snapshot of the quota per turn type for a new period; null = not granted. No defaults are assumed.
    public record Quotas(int? Grading, int? Comment)
    {
        public Outcome? Validate() =>
            Grading is < 0 or > MaxQuota ? Outcome.Invalid("grading", $"Hạn mức từ 0 đến {MaxQuota}.")
            : Comment is < 0 or > MaxQuota ? Outcome.Invalid("comment", $"Hạn mức từ 0 đến {MaxQuota}.") : null;
    }
    public const int MaxQuota = 100_000;

    public record ApproveInput(int Days, string? Label, DateTimeOffset ExpectedStartsAt, uint Version, string? Reason = null, int? Grading = null, int? Comment = null)
    {
        public Outcome? Validate()
        {
            if (new Quotas(Grading, Comment).Validate() is { } quota) return quota;
            if (Days is < 1 or > MaxDays) return Outcome.Invalid("days", $"Số ngày phải từ 1 đến {MaxDays}.");
            if (Label?.Trim().Length > 100) return Outcome.Invalid("label", "Tên gói tối đa 100 ký tự.");
            if (Reason?.Trim().Length > 500) return Outcome.Invalid("reason", "Lý do tối đa 500 ký tự.");
            return null;
        }
    }
}
