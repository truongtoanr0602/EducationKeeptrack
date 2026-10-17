using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiTests;

public class LoginTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Anonymous_requests_are_unauthorized_and_login_requires_csrf()
    {
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = "teacher@example.test", password = "Invalid" })).StatusCode);
    }
}
