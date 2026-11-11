using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiTests;

public sealed class AccountRecoveryTests(TestApplication app) : IClassFixture<TestApplication>
{
    private const string NewPassword = "Another-Secret!456";

    private string? Token(string email, string action) =>
        app.Emails.LastOrDefault(x => x.To == email && x.Body.Contains($"action={action}&")) is { } message
            ? Regex.Match(message.Body, $@"action={action}&token=([A-Za-z0-9_-]+)").Groups[1].Value
            : null;

    private async Task<HttpClient> Session(string email, string password = TestApplication.Password)
    {
        var client = app.Client();
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, "/api/auth/login", new { email, password })).StatusCode);
        return client;
    }

    [Fact]
    public async Task Password_reset_does_not_reveal_accounts_and_revokes_every_session()
    {
        await app.AddTeacher("forgetful@example.test");
        using var first = await Session("forgetful@example.test");
        using var second = await Session("forgetful@example.test");
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(first, "/api/classes", new { name = "Lớp giữ lại", subject = "Văn" })).StatusCode);

        using var anonymous = app.Client();
        var unknown = await TestApplication.Post(anonymous, "/api/auth/forgot-password", new { email = "nobody@example.test" });
        var known = await TestApplication.Post(anonymous, "/api/auth/forgot-password", new { email = "forgetful@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
        Assert.Null(Token("nobody@example.test", "reset-password"));

        // Expired links never work.
        var expired = Token("forgetful@example.test", "reset-password")!;
        app.Clock.Advance(TimeSpan.FromMinutes(61));
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(anonymous, "/api/auth/reset-password", new { token = expired, password = NewPassword })).StatusCode);

        using var fresh = await Session("forgetful@example.test");
        Assert.Equal(HttpStatusCode.Accepted, (await TestApplication.Post(anonymous, "/api/auth/forgot-password", new { email = "forgetful@example.test" })).StatusCode);
        var token = Token("forgetful@example.test", "reset-password")!;
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(anonymous, "/api/auth/reset-password", new { token = "forged", password = NewPassword })).StatusCode);
        // A rejected password does not burn the link.
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(anonymous, "/api/auth/reset-password", new { token, password = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(anonymous, "/api/auth/reset-password", new { token, password = NewPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(anonymous, "/api/auth/reset-password", new { token, password = "Third-Secret!789" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(fresh, "/api/classes", new { name = "Sau khi đổi", subject = "Văn" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(anonymous, "/api/auth/login", new { email = "forgetful@example.test", password = TestApplication.Password })).StatusCode);

        using var after = await Session("forgetful@example.test", NewPassword);
        var me = await after.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("active", me.GetProperty("access").GetProperty("status").GetString());
        Assert.Equal("Lớp giữ lại", (await after.GetFromJsonAsync<JsonElement>("/api/classes"))[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Email_change_takes_effect_only_after_verifying_the_new_address()
    {
        await app.AddTeacher("old-address@example.test");
        await app.AddTeacher("taken-address@example.test");
        using var bystander = await Session("taken-address@example.test");
        using var first = await Session("old-address@example.test");
        using var second = await Session("old-address@example.test");
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(first, "/api/classes", new { name = "Lớp theo ID", subject = "Toán" })).StatusCode);

        using var noCsrf = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-email") { Content = JsonContent.Create(new { newEmail = "new-address@example.test", currentPassword = TestApplication.Password }) };
        Assert.Equal(HttpStatusCode.BadRequest, (await first.SendAsync(noCsrf)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(first, "/api/auth/change-email", new { newEmail = "new-address@example.test", currentPassword = "Wrong-password!1" })).StatusCode);

        Assert.Equal(HttpStatusCode.Accepted, (await TestApplication.Post(first, "/api/auth/change-email", new { newEmail = "taken-address@example.test", currentPassword = TestApplication.Password })).StatusCode);
        Assert.Null(Token("taken-address@example.test", "confirm-email"));

        Assert.Equal(HttpStatusCode.Accepted, (await TestApplication.Post(first, "/api/auth/change-email", new { newEmail = "new-address@example.test", currentPassword = TestApplication.Password })).StatusCode);
        var token = Token("new-address@example.test", "confirm-email")!;
        Assert.NotNull(token);
        // Until confirmed, the old address still signs in and the new one does not.
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(anonymous, "/api/auth/login", new { email = "new-address@example.test", password = TestApplication.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(anonymous, "/api/auth/confirm-email", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(anonymous, "/api/auth/confirm-email", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bystander.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(anonymous, "/api/auth/login", new { email = "old-address@example.test", password = TestApplication.Password })).StatusCode);

        using var renamed = await Session("new-address@example.test");
        var me = await renamed.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("new-address@example.test", me.GetProperty("email").GetString());
        Assert.Equal("active", me.GetProperty("access").GetProperty("status").GetString());
        Assert.Equal("Lớp theo ID", (await renamed.GetFromJsonAsync<JsonElement>("/api/classes"))[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Email_change_fails_if_the_address_is_claimed_before_confirmation()
    {
        await app.AddTeacher("racer@example.test");
        using var racer = await Session("racer@example.test");
        Assert.Equal(HttpStatusCode.Accepted, (await TestApplication.Post(racer, "/api/auth/change-email", new { newEmail = "contested@example.test", currentPassword = TestApplication.Password })).StatusCode);
        var token = Token("contested@example.test", "confirm-email")!;
        await app.AddTeacher("contested@example.test");
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(anonymous, "/api/auth/confirm-email", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await racer.GetAsync("/api/auth/me")).StatusCode);
    }
}
