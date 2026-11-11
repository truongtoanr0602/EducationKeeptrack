using System.Net.Mail;
using System.Security.Claims;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Identity.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
namespace EducationKeeptrack.Identity.Endpoints;

// Public registration always creates a Teacher; no input can choose a role.
public static class AccountEndpoints
{
    public static void MapAccountFlows(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (RegisterRequest request, UserManager<Account> users, EmailTokens tokens, IEmailSender email,
            IOptions<EmailOptions> options, TimeProvider clock, HttpContext http) =>
        {
            var address = Normalize(request.Email);
            if (address is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email không hợp lệ."] });
            var probe = new Account { UserName = address, Email = address };
            var errors = new List<string>();
            foreach (var validator in users.PasswordValidators)
            {
                var result = await validator.ValidateAsync(users, probe, request.Password ?? "");
                errors.AddRange(result.Errors.Select(e => e.Description));
            }
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024 || errors.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = errors.Count > 0 ? [.. errors] : ["Mật khẩu không hợp lệ."] });
            var existing = await users.FindByEmailAsync(address);
            if (existing is not null)
            {
                // Same response as a new registration so the endpoint does not reveal which emails exist.
                await email.SendAsync(new EmailMessage(address, "Email đã được đăng ký",
                    $"Email này đã có tài khoản Education Keeptrack. Đăng nhập hoặc dùng \"Quên mật khẩu\": {options.Value.PublicUrl}/"), http.RequestAborted);
                return Accepted();
            }
            probe.RegisteredAt = clock.GetUtcNow();
            var created = await users.CreateAsync(probe, request.Password);
            if (!created.Succeeded) return Accepted();
            await SendVerification(probe, tokens, email, options.Value, http.RequestAborted);
            return Accepted();
        });
        group.MapPost("/verify-email", async (TokenRequest request, EmailTokens tokens, UserManager<Account> users, HttpContext http) =>
        {
            var token = await tokens.ConsumeAsync(request.Token, EmailTokenPurpose.VerifyEmail, http.RequestAborted);
            var account = token is null ? null : await users.FindByIdAsync(token.AccountId);
            if (account is null) return InvalidLink();
            account.EmailConfirmed = true;
            await users.UpdateAsync(account);
            return Results.NoContent();
        });
        group.MapPost("/resend-verification", async (HttpContext http, UserManager<Account> users, EmailTokens tokens, IEmailSender email, IOptions<EmailOptions> options) =>
        {
            var account = await users.FindByIdAsync(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (account is not null && !account.EmailConfirmed) await SendVerification(account, tokens, email, options.Value, http.RequestAborted);
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("Teacher"));
    }

    internal static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 256) return null;
        try { return new MailAddress(trimmed).Address == trimmed ? trimmed : null; }
        catch (FormatException) { return null; }
    }

    internal static IResult InvalidLink() =>
        Results.BadRequest(new { message = "Liên kết không hợp lệ, đã dùng hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới." });

    private static IResult Accepted() =>
        Results.Json(new { message = "Nếu email hợp lệ, chúng tôi đã gửi hướng dẫn xác minh. Vui lòng kiểm tra hộp thư." }, statusCode: 202);

    private static async Task SendVerification(Account account, EmailTokens tokens, IEmailSender email, EmailOptions options, CancellationToken ct)
    {
        var token = await tokens.IssueAsync(account.Id, EmailTokenPurpose.VerifyEmail, options.VerificationLifetime, null, ct);
        await email.SendAsync(new EmailMessage(account.Email!, "Xác minh email Education Keeptrack",
            $"Mở liên kết để xác minh email (hết hạn sau {options.VerificationLifetime.TotalHours:0.#} giờ): {options.PublicUrl}/?action=verify-email&token={token}"), ct);
    }

    public record RegisterRequest(string? Email, string? Password);
    public record TokenRequest(string? Token);
}
