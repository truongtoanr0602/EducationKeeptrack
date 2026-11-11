using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Application;

// Single policy every module consults: read/write/AI are decided here, never from client flags.
public sealed class AccessPolicy(ServiceAccessDatabase db, IAccountState accounts, TimeProvider clock) : IAccountAccess
{
    public async Task<AccountAccess> GetAsync(string accountId, CancellationToken ct)
    {
        var account = await accounts.GetAsync(accountId, ct);
        if (!account.Exists || account.IsBlocked) return new(false, false, false, "blocked");
        if (!account.EmailConfirmed) return new(false, false, false, "unverified");
        var now = clock.GetUtcNow();
        var periods = await db.Periods.AsNoTracking().Where(x => x.AccountId == accountId && x.StartsAt <= now).ToListAsync(ct);
        if (periods.Any(x => x.Contains(now)))
        {
            var ai = await db.Accounts.AsNoTracking().Where(x => x.AccountId == accountId).Select(x => x.AiEnabled).SingleOrDefaultAsync(ct);
            return new(true, true, ai, "active");
        }
        if (periods.Count > 0) return new(true, false, false, "expired");
        // Rejected only while no newer request is waiting for review.
        var statuses = await db.PaymentRequests.AsNoTracking().Where(x => x.AccountId == accountId).Select(x => x.Status).Distinct().ToListAsync(ct);
        var rejected = statuses.Contains(PaymentRequestStatus.Rejected) && !statuses.Contains(PaymentRequestStatus.Submitted);
        return new(false, false, false, rejected ? "rejected" : "pending");
    }
}
