using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Infrastructure;

public sealed class ProvisionedAccountAccess(ServiceAccessDatabase db, IAccountState accounts, TimeProvider clock) : IAccountAccess
{
    public async Task<AccountAccess> GetAsync(string accountId, CancellationToken ct)
    {
        var account = await accounts.GetAsync(accountId, ct);
        if (!account.Exists || account.IsBlocked) return new(false, false, false, "blocked");
        if (!account.EmailConfirmed) return new(false, false, false, "unverified");
        var grant = await db.DevelopmentGrants.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId, ct);
        if (grant is null || !grant.Approved) return new(false, false, false, "pending");
        var active = grant.Until > clock.GetUtcNow();
        return new(true, active, false, active ? "active" : "expired");
    }
}
