namespace EducationKeeptrack.Identity.Application;

public sealed record EmailMessage(string To, string Subject, string Body);

// Adapter boundary: a real provider must be configured per environment before public launch.
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
