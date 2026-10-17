using Microsoft.AspNetCore.Identity;
namespace EducationKeeptrack.Identity.Domain;

public sealed class Account : IdentityUser
{
    public bool IsBlocked { get; set; }
}

public sealed class LoginSession
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public required string SecurityStamp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public bool IsAdmin { get; set; }
    public bool Revoked { get; set; }
}
