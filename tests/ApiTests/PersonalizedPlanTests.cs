using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

public sealed class PersonalizedPlanTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Plan_snapshots_inputs_enforces_versions_and_requires_explicit_approval()
    {
        var email = $"plan-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        using var client = app.Client();
        await TestApplication.Login(client, email);
        var cls = await (await TestApplication.Post(client, "/api/classes", new { name = "Lớp kế hoạch", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var path = $"/api/learning/classes/{cls.GetProperty("id").GetString()}";
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path + "/plans", new { title = "Thiếu cấu hình", date = "2026-10-05", minutes = 30, content = "Ôn" })).StatusCode);
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var profile = new HttpRequestMessage(HttpMethod.Put, path + "/profile") { Content = JsonContent.Create(new { textbook = "Sách lớp", semester = "HK1", version = 0, skills = new[] { new { id = Guid.NewGuid(), title = "Cộng qua 10", taught = true } } }) };
        profile.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(profile)).StatusCode);
        var response = await TestApplication.Post(client, path + "/plans", new { title = "Tuần 1", date = "2026-10-05", minutes = 30, content = "5 phút chung, 20 phút riêng, 5 phút kiểm tra", priorities = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var plan = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detail = path + "/plans/" + plan.GetProperty("id").GetString();
        Assert.Equal("draft", plan.GetProperty("status").GetString());
        var snapshot = JsonDocument.Parse(plan.GetProperty("snapshotJson").GetString()!);
        Assert.Equal("Sách lớp", snapshot.RootElement.GetProperty("profile").GetProperty("Textbook").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path + "/plans", new { title = "Sai", date = "2026-10-05", minutes = 0, content = "Ôn" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, detail + "/approve", new { version = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, detail + "/approve", new { version = 1 })).StatusCode);
        var saved = await client.GetFromJsonAsync<JsonElement>(detail);
        Assert.Equal("approved", saved.GetProperty("status").GetString());
        Assert.Equal(2, saved.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await TestApplication.Post(client, path + "/plans/generate", new { })).StatusCode);

        // A new draft must not be approved after its source configuration changes.
        var draft = await (await TestApplication.Post(client, path + "/plans", new { title = "Bản cần xem lại", date = "2026-10-05", minutes = 30, content = "Ôn", priorities = Array.Empty<object>() })).Content.ReadFromJsonAsync<JsonElement>();
        var draftPath = path + "/plans/" + draft.GetProperty("id").GetString();
        var currentProfile = await client.GetFromJsonAsync<JsonElement>(path + "/profile");
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var updateProfile = new HttpRequestMessage(HttpMethod.Put, path + "/profile") { Content = JsonContent.Create(new { textbook = "Sách mới", semester = "HK1", version = 1, skills = currentProfile.GetProperty("skills") }) };
        updateProfile.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(updateProfile)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, draftPath + "/approve", new { version = 1 })).StatusCode);
        csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var edit = new HttpRequestMessage(HttpMethod.Put, draftPath) { Content = JsonContent.Create(new { title = "Đã xem lại", date = "2026-10-05", minutes = 30, content = "Phù hợp sách mới", version = 1 }) };
        edit.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(edit)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, draftPath + "/approve", new { version = 2 })).StatusCode);
        var reviewed = await client.GetFromJsonAsync<JsonElement>(draftPath);
        var history = JsonDocument.Parse(reviewed.GetProperty("historyJson").GetString()!);
        Assert.Equal(2, history.RootElement.GetArrayLength());
        Assert.Equal("draft", history.RootElement[0].GetProperty("Status").GetString());
        var oldSnapshot = JsonDocument.Parse(history.RootElement[0].GetProperty("SnapshotJson").GetString()!);
        Assert.Equal("Sách lớp", oldSnapshot.RootElement.GetProperty("profile").GetProperty("Textbook").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path + "/plans", new { title = "Vượt thời lượng", date = "2026-10-05", minutes = 10, content = "Ôn", activities = new[] { new { title = "Hoạt động quá dài", minutes = 20 } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path + "/plans", new { title = "Sai học sinh", date = "2026-10-05", minutes = 30, content = "Ôn", priorities = new[] { new { studentId = Guid.NewGuid(), skillId = Guid.NewGuid() } } })).StatusCode);

        var otherEmail = $"other-plan-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(otherEmail);
        using var other = app.Client();
        await TestApplication.Login(other, otherEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(detail)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, draftPath + "/approve", new { version = 3 })).StatusCode);
    }
}
