using System.Net;
using System.Net.Mail;
using System.Text;
using EducationKeeptrack.Identity.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EducationKeeptrack.Identity.Infrastructure;

/// <summary>
/// Delivers account emails over SMTP with STARTTLS. A failed delivery is logged (exception type and subject only, never credentials,
/// recipient or body) and then swallowed: the endpoints answer the same way whether or not the address exists, so an outage must not
/// change that answer. The user can retry through "resend verification" or "forgot password".
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var smtp = options.Value.Smtp;
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(smtp.From) ? smtp.User : smtp.From, smtp.FromName, Encoding.UTF8),
                Subject = message.Subject, SubjectEncoding = Encoding.UTF8,
                Body = message.Body, BodyEncoding = Encoding.UTF8, IsBodyHtml = false
            };
            mail.To.Add(new MailAddress(message.To));
            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network, Timeout = smtp.TimeoutSeconds * 1000,
                Credentials = string.IsNullOrEmpty(smtp.User) ? null : new NetworkCredential(smtp.User, smtp.Password)
            };
            await client.SendMailAsync(mail, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError("Email '{Subject}' was not delivered ({Error}).", message.Subject, e.GetType().Name);
        }
    }
}

/// <summary>Rejects a half-configured SMTP sender or an unusable public link origin at startup instead of at the first password reset.</summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var problems = EmailOptionsValidation.Problems(options).ToArray();
        return problems.Length == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problems);
    }
}

public static class EmailOptionsValidation
{
    public static IEnumerable<string> Problems(EmailOptions options)
    {
        var smtp = options.Smtp;
        if (!smtp.Enabled) yield break;
        if (smtp.Port is < 1 or > 65535) yield return "Identity:Email:Smtp:Port must be 1-65535.";
        if (string.IsNullOrWhiteSpace(smtp.From) && string.IsNullOrWhiteSpace(smtp.User)) yield return "Identity:Email:Smtp:From or User is required.";
        if (string.IsNullOrEmpty(smtp.User) != string.IsNullOrEmpty(smtp.Password)) yield return "Identity:Email:Smtp:User and Password must be set together.";
        if (smtp.TimeoutSeconds is < 1 or > 120) yield return "Identity:Email:Smtp:TimeoutSeconds must be 1-120.";
        if (!Uri.TryCreate(options.PublicUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || url.AbsolutePath != "/"
            || url.Query.Length > 0 || url.Fragment.Length > 0 || options.PublicUrl.EndsWith('/'))
            yield return "Identity:Email:PublicUrl must be an https origin without a path or trailing slash (links in emails are built from it).";
    }
}
