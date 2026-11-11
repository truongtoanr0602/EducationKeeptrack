using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class AccountControl(UserManager<Account> users, ILoginSessions sessions, IAccountDirectory directory) : IAccountControl
{
    public async Task<bool> SetBlockedAsync(string accountId, bool blocked, CancellationToken ct)
    {
        if (await directory.FindTeacherAsync(accountId, ct) is null) return false;
        var account = (await users.FindByIdAsync(accountId))!;
        account.IsBlocked = blocked;
        var result = await users.UpdateAsync(account);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        if (blocked)
        {
            await users.UpdateSecurityStampAsync(account);
            await sessions.RevokeAllAsync(accountId, ct);
        }
        return true;
    }
}
