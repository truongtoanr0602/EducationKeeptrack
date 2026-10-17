using System.Security.Claims;
using EducationKeeptrack.Identity.Domain;

namespace EducationKeeptrack.Identity.Application;

public interface ILoginSessions
{
    Task<ClaimsPrincipal> CreateAsync(Account account);
    Task<bool> ValidateAsync(ClaimsPrincipal principal, bool userActivity, CancellationToken cancellationToken);
    Task RevokeAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
