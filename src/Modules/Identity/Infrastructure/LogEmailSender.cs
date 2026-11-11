using EducationKeeptrack.Identity.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace EducationKeeptrack.Identity.Infrastructure;

// Placeholder until a production email provider is chosen. Links are only logged in Development.
public sealed class LogEmailSender(ILogger<LogEmailSender> logger, IHostEnvironment environment) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (environment.IsDevelopment())
            logger.LogInformation("Development email to {To}: {Subject}\n{Body}", message.To, message.Subject, message.Body);
        else
            logger.LogWarning("No email provider is configured; message '{Subject}' was not delivered.", message.Subject);
        return Task.CompletedTask;
    }
}
