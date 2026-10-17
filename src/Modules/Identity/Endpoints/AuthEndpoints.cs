using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Domain;
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
            // Dedicated Admin slice must require TOTP; never bypass it here.
            if (await users.IsInRoleAsync(user, "Admin")) return Results.Json(new { message = "Đăng nhập Admin cần xác thực hai bước." }, statusCode: 403);
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
    public record LoginRequest(string Email, string Password);
}
