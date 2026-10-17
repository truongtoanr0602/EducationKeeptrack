using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
namespace ApiTests;

// Each test gets independent real storage and a clock; parallel tests cannot
// interfere with time or accounts.
public sealed class SessionTests
{
    [Fact]
    public async Task Idle_limit_is_two_hours_and_polling_does_not_extend_it()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("idle@example.test");
        using var client = app.Client(); await TestApplication.Login(client, "idle@example.test");
        app.Clock.Advance(TimeSpan.FromMinutes(119));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        app.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Explicit_activity_renews_idle_but_never_absolute_limit()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("absolute@example.test");
        using var client = app.Client(); await TestApplication.Login(client, "absolute@example.test");
        for (var hour = 1; hour < 24; hour++)
        {
            app.Clock.Advance(TimeSpan.FromHours(1));
            Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(client, "/api/auth/activity", new { })).StatusCode);
        }
        app.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_copied_cookie_and_invalid_cookie_is_rejected()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("logout@example.test");
        using var client = app.Client();
        var login = await TestApplication.Post(client, "/api/auth/login", new { email = "logout@example.test", password = "Test-only!Password123" });
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("ekt.session=")).Split(';')[0];
        Assert.Contains("httponly", login.Headers.GetValues("Set-Cookie").First().ToLowerInvariant());
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(client, "/api/auth/logout", new { })).StatusCode);
        using var replay = app.Client(); replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/auth/me")).StatusCode);
        using var invalid = app.Client(); invalid.DefaultRequestHeaders.Add("Cookie", "ekt.session=forged");
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalid.GetAsync("/api/classes")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Security_stamp_change_or_block_invalidates_session_at_next_request(bool block)
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        var id = await app.AddTeacher("revoke@example.test");
        using var client = app.Client(); await TestApplication.Login(client, "revoke@example.test");
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var account = (await users.FindByIdAsync(id))!;
            if (block) { account.IsBlocked = true; Assert.True((await users.UpdateAsync(account)).Succeeded); }
            else Assert.True((await users.UpdateSecurityStampAsync(account)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/classes")).StatusCode);
    }

    [Theory]
    [InlineData(false, true, "pending")]
    [InlineData(true, false, "unverified")]
    public async Task Authentication_does_not_grant_service_access(bool approved, bool confirmed, string status)
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("pending@example.test", approved, confirmed);
        using var client = app.Client(); await TestApplication.Login(client, "pending@example.test");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal(status, me.GetProperty("access").GetProperty("status").GetString());
        Assert.False(me.GetProperty("access").GetProperty("canUseAi").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(client, "/api/classes", new { name = "X", subject = "Y" })).StatusCode);
    }

    [Fact]
    public async Task Expired_service_allows_reading_old_classes_but_denies_writes()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        var id = await app.AddTeacher("expired@example.test");
        using var client = app.Client(); await TestApplication.Login(client, "expired@example.test");
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, "/api/classes", new { name = "Lớp cũ", subject = "Toán" })).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EducationKeeptrack.ServiceAccess.Infrastructure.ServiceAccessDatabase>();
            await db.DevelopmentGrants.Where(x => x.AccountId == id).ExecuteUpdateAsync(x => x.SetProperty(u => u.Until, app.Clock.GetUtcNow()));
        }
        var classes = await client.GetFromJsonAsync<JsonElement>("/api/classes");
        Assert.Equal("Lớp cũ", classes[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(client, "/api/classes", new { name = "Lớp mới", subject = "Toán" })).StatusCode);
    }
}
