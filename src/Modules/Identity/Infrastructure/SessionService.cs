using System.Security.Claims;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Identity.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class SessionService(IdentityDatabase db, UserManager<Account> users, TimeProvider clock) : ILoginSessions
{
    public async Task<ClaimsPrincipal> CreateAsync(Account account)
    {
        var admin = await users.IsInRoleAsync(account, "Admin");
        var now = clock.GetUtcNow();
        var session = new LoginSession
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            SecurityStamp = account.SecurityStamp!,
            CreatedAt = now,
            LastActiveAt = now,
            IsAdmin = admin
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return new(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, account.Id), new(ClaimTypes.Name, account.Email!),
            new(ClaimTypes.Role, admin ? "Admin" : "Teacher"), new("sid", session.Id.ToString())
        ], "session"));
    }

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, bool userActivity, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sid"), out var id)) return false;
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (session is null || session.Revoked || session.AccountId != principal.FindFirstValue(ClaimTypes.NameIdentifier)) return false;
        var now = clock.GetUtcNow();
        var idle = TimeSpan.FromMinutes(session.IsAdmin ? 30 : 120);
        var absolute = TimeSpan.FromHours(session.IsAdmin ? 8 : 24);
        if (now >= session.CreatedAt + absolute || now >= session.LastActiveAt + idle) return false;
        var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.AccountId, ct);
        if (account is null || account.IsBlocked || account.SecurityStamp != session.SecurityStamp) return false;
        if (userActivity)
            await db.Sessions.Where(x => x.Id == id && x.LastActiveAt < now)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.LastActiveAt, now), ct);
        return true;
    }

    public Task RevokeAsync(ClaimsPrincipal principal, CancellationToken ct) =>
        db.Sessions.Where(x => x.Id == Guid.Parse(principal.FindFirstValue("sid")!))
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Revoked, true), ct);
}
