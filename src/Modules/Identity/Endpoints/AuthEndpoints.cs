using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Identity.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Identity.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        });
        group.MapPost("/login", async (LoginRequest request, HttpContext context, UserManager<Account> users, ILoginSessions sessions) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
                return Results.BadRequest(new { message = "Vui lòng nhập email và mật khẩu hợp lệ." });
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null || user.IsBlocked || await users.IsLockedOutAsync(user)) return InvalidLogin();
            if (!await users.CheckPasswordAsync(user, request.Password))
            {
                await users.AccessFailedAsync(user);
                return InvalidLogin();
            }
            // Admin always needs password + authenticator code; there is no bypass or fallback.
            if (await users.IsInRoleAsync(user, AdminSetup.Role))
            {
                if (!user.TwoFactorEnabled || string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)))
                    return Results.Json(new { message = "Tài khoản Admin chưa thiết lập xác thực hai bước nên không thể đăng nhập." }, statusCode: 403);
                if (string.IsNullOrWhiteSpace(request.Code))
                    return Results.Json(new { message = "Nhập mã từ ứng dụng xác thực để đăng nhập Admin.", requiresTotp = true }, statusCode: 403);
                var code = request.Code.Replace(" ", "").Replace("-", "");
                if (code.Length != 6 || !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
                {
                    await users.AccessFailedAsync(user);
                    return Results.Json(new { message = "Email, mật khẩu hoặc mã xác thực không đúng.", requiresTotp = true }, statusCode: 401);
                }
            }
            await users.ResetAccessFailedCountAsync(user);
            await context.SignInAsync("session", await sessions.CreateAsync(user), new AuthenticationProperties { IsPersistent = false });
            return Results.Ok(new { email = user.Email });
        });
        group.MapGet("/me", async (HttpContext context, IAccountAccess access) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new
            {
                email = context.User.Identity!.Name,
                role = context.User.FindFirstValue(ClaimTypes.Role),
                access = await access.GetAsync(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, context.RequestAborted)
            });
        }).RequireAuthorization();
        group.MapPost("/activity", () => Results.NoContent()).RequireAuthorization();
        group.MapPost("/logout", async (HttpContext context, ILoginSessions sessions) =>
        {
            await sessions.RevokeAsync(context.User, context.RequestAborted);
            await context.SignOutAsync("session");
            return Results.NoContent();
        }).RequireAuthorization();
    }
    private static IResult InvalidLogin() => Results.Json(new { message = "Email hoặc mật khẩu không đúng, hoặc tài khoản chưa thể đăng nhập." }, statusCode: 401);
    public record LoginRequest(string Email, string Password, string? Code = null);
}
