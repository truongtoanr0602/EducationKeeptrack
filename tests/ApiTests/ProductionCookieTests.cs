using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace ApiTests;

public sealed class ProductionCookieTests
{
    [Fact]
    public async Task Https_login_sets_secure_httponly_cookies_and_csrf_is_bound_to_identity()
    {
        await using var app = new TestApplication { ProductionCookies = true }; await app.InitializeAsync();
        await app.AddTeacher("secure@example.test");
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrfResponse = await client.GetAsync("/api/auth/csrf");
        Assert.Contains("secure", csrfResponse.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant());
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = csrf.GetProperty("token").GetString();
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "secure@example.test", password = "Test-only!Password123" })
        };
        login.Headers.Add("X-CSRF-TOKEN", token);
        var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=strict", cookie);
        using var staleToken = new HttpRequestMessage(HttpMethod.Post, "/api/classes") { Content = JsonContent.Create(new { name = "X", subject = "Y" }) };
        staleToken.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(staleToken)).StatusCode);
    }
}
