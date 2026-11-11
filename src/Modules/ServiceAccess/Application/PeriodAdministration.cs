using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Application;

// Direct grants/renewals, period corrections and account lock decisions, all with reason + history.
public sealed class PeriodAdministration(ServiceAccessDatabase db, ServiceAdministration admin, AiQuotaLedger ledger, IAccountControl control, IAccountDirectory directory, TimeProvider clock)
{
    public async Task<Outcome> GrantAsync(Actor actor, string accountId, GrantInput input, CancellationToken ct)
    {
        if (input.Days is < 1 or > ServiceAdministration.MaxDays) return Outcome.Invalid("days", $"Số ngày phải từ 1 đến {ServiceAdministration.MaxDays}.");
        if (input.Label?.Trim().Length > 100) return Outcome.Invalid("label", "Tên gói tối đa 100 ký tự.");
        if (!ValidReason(input.Reason)) return Outcome.Invalid("reason", "Nhập lý do cấp/gia hạn (tối đa 500 ký tự).");
        var quotas = new ServiceAdministration.Quotas(input.Grading, input.Comment);
        if (quotas.Validate() is { } invalidQuota) return invalidQuota;
        var blocked = await admin.GrantBlockerAsync(accountId, ct);
        if (blocked is not null) return blocked;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(accountId, ct);
        var created = await admin.AddPeriodAsync(actor, accountId, input.Days, input.Label, input.ExpectedStartsAt, input.Reason, null, quotas, ct);
        if (created.Status != 200) return created;
        var period = (Domain.ServicePeriod)created.Body!;
        admin.Record(actor, accountId, "period.granted", period.Reason, new { periodId = period.Id, period.StartsAt, period.EndsAt, input.Days, input.Label, period.GradingQuota, period.CommentQuota });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Ok(new { period.Id, period.StartsAt, period.EndsAt });
    }

    // Correcting a granted period needs a reason; only the latest period may move so periods never overlap.
    public async Task<Outcome> UpdateAsync(Actor actor, Guid periodId, PeriodEdit input, CancellationToken ct)
    {
        if (!ValidReason(input.Reason)) return Outcome.Invalid("reason", "Nhập lý do sửa kỳ (tối đa 500 ký tự).");
        if (input.Label?.Trim().Length > 100) return Outcome.Invalid("label", "Tên gói tối đa 100 ký tự.");
        var accountId = await db.Periods.Where(x => x.Id == periodId).Select(x => x.AccountId).SingleOrDefaultAsync(ct);
        if (accountId is null) return Outcome.NotFound;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(accountId, ct);
        var period = await db.Periods.SingleAsync(x => x.Id == periodId, ct);
        if (period.Version != input.Version) return Outcome.Conflict("Kỳ đã được thay đổi. Vui lòng tải lại.");
        if (await db.Periods.AnyAsync(x => x.AccountId == accountId && x.StartsAt > period.StartsAt, ct))
            return Outcome.Conflict("Chỉ sửa được kỳ cuối cùng; kỳ này đã có kỳ gia hạn nối sau.");
        var endsAt = Domain.Moment.Of(input.EndsAt);
        if (endsAt <= period.StartsAt || endsAt > period.StartsAt.AddDays(ServiceAdministration.MaxDays))
            return Outcome.Invalid("endsAt", "Ngày kết thúc phải sau ngày bắt đầu của kỳ.");
        var before = new { period.EndsAt, period.Label };
        period.EndsAt = endsAt;
        period.Label = input.Label?.Trim() ?? period.Label;
        admin.Record(actor, accountId, "period.updated", input.Reason!.Trim(), new { periodId, before, after = new { period.EndsAt, period.Label } });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Ok(new { period.Id, period.StartsAt, period.EndsAt });
    }

    // Lock first in Identity (sessions die), then under the account row lock record history
    // so that any AI reservation taken concurrently is seen and cancelled by quota handling.
    public async Task<Outcome> SetLockedAsync(Actor actor, string accountId, bool locked, string? reason, CancellationToken ct)
    {
        if (!ValidReason(reason)) return Outcome.Invalid("reason", locked ? "Nhập lý do khóa (tối đa 500 ký tự)." : "Nhập lý do mở khóa (tối đa 500 ký tự).");
        if (!await control.SetBlockedAsync(accountId, locked, ct)) return Outcome.NotFound;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(accountId, ct);
        if (locked) await OnLockedAsync(accountId, ct);
        admin.Record(actor, accountId, locked ? "account.locked" : "account.unlocked", reason!.Trim(), null);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Ok(new { locked, at = clock.GetUtcNow() });
    }

    // Extended by AI quota handling: cancels held reservations so late results cannot be applied.
    private Task OnLockedAsync(string accountId, CancellationToken ct) => ledger.CancelHeldAsync(accountId, ct);

    // Switching AI never touches quota counters; accepted work may still finish.
    public async Task<Outcome> SetAiAsync(Actor actor, string accountId, AiInput input, CancellationToken ct)
    {
        if (!ValidReason(input.Reason)) return Outcome.Invalid("reason", "Nhập lý do bật/tắt AI (tối đa 500 ký tự).");
        if (await directory.FindTeacherAsync(accountId, ct) is null) return Outcome.NotFound;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await db.LockAsync(accountId, ct);
        account.AiEnabled = input.Enabled;
        admin.Record(actor, accountId, input.Enabled ? "ai.enabled" : "ai.disabled", input.Reason!.Trim(), null);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Ok(new { aiEnabled = account.AiEnabled });
    }

    // Adjusts a period's quota snapshot; never below what is already held or used in that period.
    public async Task<Outcome> SetQuotasAsync(Actor actor, Guid periodId, QuotaEdit input, CancellationToken ct)
    {
        if (!ValidReason(input.Reason)) return Outcome.Invalid("reason", "Nhập lý do điều chỉnh hạn mức (tối đa 500 ký tự).");
        var quotas = new ServiceAdministration.Quotas(input.Grading, input.Comment);
        if (quotas.Validate() is { } invalidQuota) return invalidQuota;
        var accountId = await db.Periods.Where(x => x.Id == periodId).Select(x => x.AccountId).SingleOrDefaultAsync(ct);
        if (accountId is null) return Outcome.NotFound;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.LockAsync(accountId, ct);
        var period = await db.Periods.SingleAsync(x => x.Id == periodId, ct);
        if (period.Version != input.Version) return Outcome.Conflict("Kỳ đã được thay đổi. Vui lòng tải lại.");
        var before = new { period.GradingQuota, period.CommentQuota };
        period.GradingQuota = quotas.Grading;
        period.CommentQuota = quotas.Comment;
        foreach (var kind in AiQuotaLedger.Kinds)
        {
            var spent = await ledger.Spent(periodId, kind, ct);
            if (spent > 0 && (period.QuotaFor(kind) ?? 0) < spent)
                return Outcome.Conflict($"Hạn mức {AiQuotaLedger.KindName(kind)} không thể thấp hơn số lượt đã dùng hoặc đang giữ ({spent}).");
        }
        admin.Record(actor, accountId, "quota.updated", input.Reason!.Trim(), new { periodId, before, after = new { period.GradingQuota, period.CommentQuota } });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Outcome.Ok(new { period.Id, period.GradingQuota, period.CommentQuota });
    }

    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500;

    public record GrantInput(int Days, string? Label, DateTimeOffset ExpectedStartsAt, string? Reason, int? Grading = null, int? Comment = null);
    public record AiInput(bool Enabled, string? Reason);
    public record QuotaEdit(int? Grading, int? Comment, string? Reason, uint Version);
    public record PeriodEdit(DateTimeOffset EndsAt, string? Label, string? Reason, uint Version);
    public record ReasonOnly(string? Reason);
}
