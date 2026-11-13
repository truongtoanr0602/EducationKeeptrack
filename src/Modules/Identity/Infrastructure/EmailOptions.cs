namespace EducationKeeptrack.Identity.Infrastructure;

// Lifetimes are deployment configuration (Identity:Email:*); the defaults are development values, not policy.
public sealed class EmailOptions
{
    /// <summary>Origin used in every emailed link, e.g. https://keeptrack.example.com (no trailing slash).</summary>
    public string PublicUrl { get; set; } = "http://127.0.0.1:5173";
    public TimeSpan VerificationLifetime { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan ResetLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan EmailChangeLifetime { get; set; } = TimeSpan.FromHours(24);
    public SmtpOptions Smtp { get; set; } = new();
}

/// <summary>
/// Identity:Email:Smtp:*. Leaving Host empty keeps the logging placeholder sender. The sender uses STARTTLS (typically port 587);
/// implicit TLS on port 465 is not supported by System.Net.Mail.
/// </summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    /// <summary>Sender address. Defaults to <see cref="User"/>; providers such as Gmail only accept their own address or an alias.</summary>
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Education Keeptrack";
    public bool EnableSsl { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 20;
    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}
