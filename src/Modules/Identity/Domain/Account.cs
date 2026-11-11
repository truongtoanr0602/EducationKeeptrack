using Microsoft.AspNetCore.Identity;
namespace EducationKeeptrack.Identity.Domain;

public sealed class Account : IdentityUser
{
    public bool IsBlocked { get; set; }
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
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

public enum EmailTokenPurpose { VerifyEmail = 1, ResetPassword = 2, ChangeEmail = 3 }

// One-time, expiring token sent by email. Only a SHA-256 hash is stored.
public sealed class EmailToken
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public EmailTokenPurpose Purpose { get; set; }
    public required string TokenHash { get; set; }
    public string? NewEmail { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
