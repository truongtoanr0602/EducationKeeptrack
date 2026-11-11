namespace EducationKeeptrack.Identity.Infrastructure;

// Lifetimes are deployment configuration (Identity:Email:*); the defaults are development values, not policy.
public sealed class EmailOptions
{
    public string PublicUrl { get; set; } = "http://127.0.0.1:5173";
    public TimeSpan VerificationLifetime { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan ResetLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan EmailChangeLifetime { get; set; } = TimeSpan.FromHours(24);
}
