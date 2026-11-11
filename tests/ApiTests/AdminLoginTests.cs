using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed class AdminLoginTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Admin_login_requires_password_and_authenticator_code()
    {
        var key = await app.AddAdmin("totp-admin@example.test");
        using var client = app.Client();

        var missing = await TestApplication.Post(client, "/api/auth/login", new { email = "totp-admin@example.test", password = TestApplication.Password });
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.True((await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requiresTotp").GetBoolean());

        var wrongCode = Totp.Code(key) == "000000" ? "111111" : "000000";
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(client, "/api/auth/login", new { email = "totp-admin@example.test", password = TestApplication.Password, code = wrongCode })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(client, "/api/auth/login", new { email = "totp-admin@example.test", password = "Wrong-password!123", code = Totp.Code(key) })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, "/api/auth/login", new { email = "totp-admin@example.test", password = TestApplication.Password, code = Totp.Code(key) })).StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Admin", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Admin_role_without_enrolled_authenticator_cannot_sign_in()
    {
        await app.AddAdmin("no-2fa-admin@example.test", enrollTotp: false);
        using var client = app.Client();
        var response = await TestApplication.Post(client, "/api/auth/login", new { email = "no-2fa-admin@example.test", password = TestApplication.Password, code = "123456" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/teachers")).StatusCode);
    }

    [Fact]
    public async Task Admin_sees_teacher_profiles_but_not_teacher_data()
    {
        var teacherId = await app.AddTeacher("profile-teacher@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "profile-teacher@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/admin/teachers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync($"/api/admin/teachers/{teacherId}")).StatusCode);
        var created = await TestApplication.Post(teacher, "/api/classes", new { name = "Lớp riêng", subject = "Toán", description = "" });
        var classId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var key = await app.AddAdmin("profiles-admin@example.test");
        using var admin = app.Client();
        await app.LoginAdmin(admin, "profiles-admin@example.test", key);
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/admin/teachers");
        var row = list.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "profile-teacher@example.test");
        Assert.Equal(teacherId, row.GetProperty("id").GetString());
        Assert.True(row.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal("active", row.GetProperty("status").GetString());
        Assert.DoesNotContain(list.EnumerateArray(), x => x.GetProperty("email").GetString() == "profiles-admin@example.test");

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}");
        Assert.Equal("profile-teacher@example.test", detail.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/teachers/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/classes/{classId}/enrollments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/students")).StatusCode);
    }

    [Fact]
    public async Task Admin_session_expires_after_idle_limit()
    {
        var key = await app.AddAdmin("idle-admin@example.test");
        using var admin = app.Client();
        await app.LoginAdmin(admin, "idle-admin@example.test", key);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/teachers")).StatusCode);
        app.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/admin/teachers")).StatusCode);
    }
}
