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
            // A concurrent duplicate keeps the non-disclosing answer; any other failure is a real validation error.
            if (!created.Succeeded && created.Errors.All(e => e.Code is "DuplicateEmail" or "DuplicateUserName")) return Accepted();
            if (!created.Succeeded)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email này chưa được hỗ trợ. Vui lòng dùng email khác."] });
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

    public static void MapRecoveryFlows(this RouteGroupBuilder group)
    {
        group.MapPost("/forgot-password", async (EmailRequest request, UserManager<Account> users, EmailTokens tokens, IEmailSender email,
            IOptions<EmailOptions> options, HttpContext http) =>
        {
            var address = Normalize(request.Email);
            var account = address is null ? null : await users.FindByEmailAsync(address);
            if (account is not null && account.EmailConfirmed && !account.IsBlocked)
            {
                var token = await tokens.IssueAsync(account.Id, EmailTokenPurpose.ResetPassword, options.Value.ResetLifetime, null, http.RequestAborted);
                await email.SendAsync(new EmailMessage(account.Email!, "Đặt lại mật khẩu Education Keeptrack",
                    $"Mở liên kết để đặt mật khẩu mới (dùng một lần, hết hạn sau {options.Value.ResetLifetime.TotalMinutes:0} phút): {options.Value.PublicUrl}/?action=reset-password&token={token}"), http.RequestAborted);
            }
            // Identical response whether or not the account exists.
            return Results.Json(new { message = "Nếu email có tài khoản, chúng tôi đã gửi liên kết đặt lại mật khẩu." }, statusCode: 202);
        });
        group.MapPost("/reset-password", async (ResetRequest request, UserManager<Account> users, EmailTokens tokens, ILoginSessions sessions, HttpContext http) =>
        {
            var pending = await tokens.PeekAsync(request.Token, EmailTokenPurpose.ResetPassword, http.RequestAborted);
            var account = pending is null ? null : await users.FindByIdAsync(pending.AccountId);
            if (account is null) return InvalidLink();
            var errors = await PasswordErrors(users, account, request.Password);
            if (errors.Length > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = errors });
            if (await tokens.ConsumeAsync(request.Token, EmailTokenPurpose.ResetPassword, http.RequestAborted) is null) return InvalidLink();
            account.PasswordHash = users.PasswordHasher.HashPassword(account, request.Password!);
            account.AccessFailedCount = 0;
            account.LockoutEnd = null;
            // New security stamp + explicit revocation: every existing session fails on its next request.
            await users.UpdateSecurityStampAsync(account);
            await sessions.RevokeAllAsync(account.Id, http.RequestAborted);
            return Results.NoContent();
        });
        group.MapPost("/change-email", async (ChangeEmailRequest request, HttpContext http, UserManager<Account> users, EmailTokens tokens,
            IEmailSender email, IOptions<EmailOptions> options) =>
        {
            var account = await users.FindByIdAsync(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (account is null) return Results.Unauthorized();
            if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 1024 || !await users.CheckPasswordAsync(account, request.CurrentPassword))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["Mật khẩu hiện tại không đúng."] });
            var address = Normalize(request.NewEmail);
            if (address is null || string.Equals(address, account.Email, StringComparison.OrdinalIgnoreCase))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newEmail"] = ["Nhập email mới hợp lệ, khác email hiện tại."] });
            if (await users.FindByEmailAsync(address) is not null)
                await email.SendAsync(new EmailMessage(address, "Yêu cầu đổi email",
                    "Có yêu cầu dùng email này cho một tài khoản Education Keeptrack khác, nhưng email đã có tài khoản nên không được đổi."), http.RequestAborted);
            else
            {
                var token = await tokens.IssueAsync(account.Id, EmailTokenPurpose.ChangeEmail, options.Value.EmailChangeLifetime, address, http.RequestAborted);
                await email.SendAsync(new EmailMessage(address, "Xác minh email mới Education Keeptrack",
                    $"Mở liên kết để xác nhận dùng email này cho tài khoản (dùng một lần): {options.Value.PublicUrl}/?action=confirm-email&token={token}"), http.RequestAborted);
                await email.SendAsync(new EmailMessage(account.Email!, "Có yêu cầu đổi email",
                    "Email đăng nhập chỉ đổi sau khi địa chỉ mới được xác minh. Nếu không phải bạn, hãy đổi mật khẩu ngay."), http.RequestAborted);
            }
            // Same response whether or not the new address already has an account.
            return Results.Json(new { message = "Kiểm tra hộp thư email mới để xác nhận. Email đăng nhập chưa thay đổi cho đến khi xác nhận." }, statusCode: 202);
        }).RequireAuthorization(p => p.RequireRole("Teacher"));
        group.MapPost("/confirm-email", async (TokenRequest request, UserManager<Account> users, EmailTokens tokens, ILoginSessions sessions, HttpContext http) =>
        {
            var token = await tokens.ConsumeAsync(request.Token, EmailTokenPurpose.ChangeEmail, http.RequestAborted);
            var account = token?.NewEmail is null ? null : await users.FindByIdAsync(token.AccountId);
            if (account is null) return InvalidLink();
            if (await users.FindByEmailAsync(token!.NewEmail!) is not null)
                return Results.Conflict(new { message = "Email mới đã được dùng cho tài khoản khác. Email đăng nhập không thay đổi." });
            // Same account ID: periods, quota and data stay attached; only the login address changes.
            account.Email = token.NewEmail;
            account.UserName = token.NewEmail;
            account.EmailConfirmed = true;
            var result = await users.UpdateAsync(account);
            if (!result.Succeeded) return Results.Conflict(new { message = "Không thể đổi email. Vui lòng thử lại." });
            await users.UpdateSecurityStampAsync(account);
            await sessions.RevokeAllAsync(account.Id, http.RequestAborted);
            return Results.NoContent();
        });
    }

    private static async Task<string[]> PasswordErrors(UserManager<Account> users, Account account, string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024) return ["Mật khẩu không hợp lệ."];
        var errors = new List<string>();
        foreach (var validator in users.PasswordValidators)
            errors.AddRange((await validator.ValidateAsync(users, account, password)).Errors.Select(e => e.Description));
        return [.. errors];
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
    public record EmailRequest(string? Email);
    public record ResetRequest(string? Token, string? Password);
    public record ChangeEmailRequest(string? NewEmail, string? CurrentPassword);
}
