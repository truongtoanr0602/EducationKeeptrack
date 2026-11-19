using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Application;
using Microsoft.Extensions.Options;
namespace EducationKeeptrack.Identity.Infrastructure;

// The "you can start" email sent when an Admin approves a request or grants a period.
public sealed class AccessNotifier(IAccountDirectory directory, IEmailSender email, IOptions<EmailOptions> options) : IAccessNotifier
{
    public async Task AccessGrantedAsync(string accountId, CancellationToken ct)
    {
        var teacher = await directory.FindTeacherAsync(accountId, ct);
        if (teacher is null) return;
        await email.SendAsync(new EmailMessage(teacher.Email, "Tài khoản Education Keeptrack đã được cấp quyền",
            $"Tài khoản của bạn đã được cấp quyền sử dụng. Mở liên kết để bắt đầu thiết lập lớp học đầu tiên: {options.Value.PublicUrl}/#/setup/1"), ct);
    }
}
