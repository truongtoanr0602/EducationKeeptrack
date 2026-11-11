using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class AccountDirectory(IdentityDatabase db) : IAccountDirectory
{
    public async Task<IReadOnlyList<AccountSummary>> ListTeachersAsync(CancellationToken ct) =>
        await Teachers().OrderByDescending(x => x.RegisteredAt).ThenBy(x => x.Email)
            .Select(x => new AccountSummary(x.Id, x.Email!, x.EmailConfirmed, x.IsBlocked, x.RegisteredAt)).ToListAsync(ct);

    public Task<AccountSummary?> FindTeacherAsync(string accountId, CancellationToken ct) =>
        Teachers().Where(x => x.Id == accountId)
            .Select(x => new AccountSummary(x.Id, x.Email!, x.EmailConfirmed, x.IsBlocked, x.RegisteredAt)).SingleOrDefaultAsync(ct);

    private IQueryable<Domain.Account> Teachers()
    {
        var adminIds = from role in db.Roles
                       join link in db.UserRoles on role.Id equals link.RoleId
                       where role.Name == AdminSetup.Role
                       select link.UserId;
        return db.Users.AsNoTracking().Where(x => !adminIds.Contains(x.Id));
    }
}
