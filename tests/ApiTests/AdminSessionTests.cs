using System.Net;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace ApiTests;

public sealed class AdminSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_session_has_shorter_limits_and_cannot_access_classes(bool keepActive)
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        var id = await app.AddTeacher("admin@example.test");
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
        var account = (await users.FindByIdAsync(id))!;
        Assert.True((await users.AddToRoleAsync(account, "Admin")).Succeeded);
        // Trusted fixture provisions an already second-factor-authenticated session;
        // there is no public test or Admin-login bypass endpoint.
        var principal = await scope.ServiceProvider.GetRequiredService<ILoginSessions>().CreateAsync(account);
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("session");
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
        }, "session");
        using var client = app.Client();
        client.DefaultRequestHeaders.Add("Cookie", "ekt.session=" + options.TicketDataFormat.Protect(ticket));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/classes")).StatusCode);
        if (keepActive)
        {
            for (var i = 0; i < 47; i++)
            {
                app.Clock.Advance(TimeSpan.FromMinutes(10));
                Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(client, "/api/auth/activity", new { })).StatusCode);
            }
            app.Clock.Advance(TimeSpan.FromMinutes(10));
        }
        else app.Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var passwordOnly = app.Client();
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(passwordOnly, "/api/auth/login", new { email = account.Email, password = "Test-only!Password123" })).StatusCode);
    }
}
