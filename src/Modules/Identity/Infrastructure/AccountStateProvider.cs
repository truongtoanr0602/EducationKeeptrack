using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class AccountStateProvider(IdentityDatabase db) : IAccountState
{
    public async Task<AccountState> GetAsync(string accountId, CancellationToken ct)
    {
        var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId, ct);
        return account is null ? new(false, false, true) : new(true, account.EmailConfirmed, account.IsBlocked);
    }
}
