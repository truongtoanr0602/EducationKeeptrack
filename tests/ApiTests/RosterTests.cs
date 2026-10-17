using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace ApiTests;

public sealed class RosterTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private async Task<(HttpClient Client, string ClassId, string StudentId)> Setup()
    {
        var email = $"roster-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        return (client, classroom.GetProperty("id").GetString()!, student.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Duplicate_enrollments_are_rejected_including_concurrent_requests()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            var path = $"/api/classes/{classId}/enrollments";
            var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => TestApplication.Post(client, path, new { studentId, startDate = "2026-10-01" })));
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>(path)).GetArrayLength());
        }
    }

    [Fact]
    public async Task Status_changes_preserve_history_and_stopped_enrollment_is_not_reopened()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            var path = $"/api/classes/{classId}/enrollments";
            var enrollment = await (await TestApplication.Post(client, path, new { studentId, startDate = "2026-10-01" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = enrollment.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/{id}", new { status = "leave", effectiveDate = "2026-10-05", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/{id}", new { status = "stopped", effectiveDate = "2026-10-06", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/{id}", new { status = "active", effectiveDate = "2026-10-07", version = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/{id}", new { status = "stopped", effectiveDate = "2026-10-10", version = 3 })).StatusCode);
            var roster = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal("2026-10-10", roster[0].GetProperty("endDate").GetString());
            Assert.Equal(new[] { "active", "leave", "active", "stopped" }, roster[0].GetProperty("history").EnumerateArray().Select(x => x.GetProperty("status").GetString()));
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/{id}", new { status = "active", effectiveDate = "2026-10-11", version = 4 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new { studentId, startDate = "2026-10-09" })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, new { studentId, startDate = "2026-10-11" })).StatusCode);
        }
    }

    [Fact]
    public async Task Profile_edits_and_tenant_filters_apply_to_every_roster_route()
    {
        var (client, classId, studentId) = await Setup();
        var (other, otherClassId, otherStudentId) = await Setup();
        using (client) using (other)
        {
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/students/{studentId}", new { name = "An đã sửa", parentName = "Bình", phone = "0901234567", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"/api/students/{studentId}", new { name = "Stale tab", version = 1 })).StatusCode);
            var profile = await client.GetFromJsonAsync<JsonElement>($"/api/students/{studentId}");
            Assert.Equal("An đã sửa", profile.GetProperty("name").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/students/{studentId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, $"/api/students/{studentId}", new { name = "Hacked" })).StatusCode);
            var students = await other.GetFromJsonAsync<JsonElement>("/api/students");
            Assert.DoesNotContain(students.EnumerateArray(), x => x.GetProperty("id").GetString() == studentId);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId = otherStudentId, startDate = "2026-10-01" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(client, $"/api/classes/{otherClassId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
            var path = $"/api/classes/{classId}/enrollments";
            var enrollment = await (await TestApplication.Post(client, path, new { studentId, startDate = "2026-10-01" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = enrollment.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, $"{path}/{id}", new { status = "leave", effectiveDate = "2026-10-02", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(client, $"/api/classes/{otherClassId}/enrollments/{id}", new { status = "leave", effectiveDate = "2026-10-02", version = 1 })).StatusCode);
        }
    }

    [Fact]
    public async Task Invalid_profile_and_status_do_not_change_saved_data()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, "/api/students", new { name = " ", email = "invalid", phone = "abc" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"/api/students/{studentId}", new { name = "An", email = "invalid" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/students", new { name = "No CSRF" })).StatusCode);
            var path = $"/api/classes/{classId}/enrollments";
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { studentId })).StatusCode);
            var enrollment = await (await TestApplication.Post(client, path, new { studentId, startDate = "2026-10-01" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = enrollment.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"{path}/{id}", new { status = "unknown", effectiveDate = "2026-10-02", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"{path}/{id}", new { status = "leave", effectiveDate = "2026-09-30", version = 1 })).StatusCode);
            var roster = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal("active", roster[0].GetProperty("status").GetString());
            Assert.Equal(1, roster[0].GetProperty("history").GetArrayLength());
        }
    }

    [Theory]
    [InlineData(false, true, "pending")]
    [InlineData(true, false, "unverified")]
    public async Task Accounts_without_service_access_cannot_read_or_write_roster(bool approved, bool confirmed, string status)
    {
        var email = $"denied-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email, approved, confirmed);
        using var client = app.Client();
        // Login denies unverified accounts; pending teachers can authenticate but cannot read data.
        var login = await TestApplication.Post(client, "/api/auth/login", new { email, password = "Test-only!Password123" });
        var expected = login.IsSuccessStatusCode ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        Assert.Equal(expected, (await client.GetAsync("/api/students")).StatusCode);
        Assert.Equal(expected, (await TestApplication.Post(client, "/api/students", new { name = "An" })).StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            var result = await (await client.GetAsync("/api/students")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(status, result.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Expiry_allows_reading_but_blocks_all_roster_writes_and_blocking_revokes_access()
    {
        var email = $"expiry-{Guid.NewGuid():N}@example.test";
        var ownerId = await app.AddTeacher(email);
        using var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var studentId = student.GetProperty("id").GetString();
        var path = $"/api/classes/{classroom.GetProperty("id").GetString()}/enrollments";
        var enrollment = await (await TestApplication.Post(client, path, new { studentId, startDate = "2026-10-01" })).Content.ReadFromJsonAsync<JsonElement>();
        app.Clock.Advance(TimeSpan.FromDays(31));
        using var expired = app.Client();
        await TestApplication.Login(expired, email);
        Assert.Equal(HttpStatusCode.OK, (await expired.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await expired.GetAsync($"/api/students/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await expired.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, "/api/students", new { name = "New" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(expired, $"/api/students/{studentId}", new { name = "Changed" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, path, new { studentId, startDate = "2026-11-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(expired, $"{path}/{enrollment.GetProperty("id").GetString()}", new { status = "leave", effectiveDate = "2026-10-02", version = 1 })).StatusCode);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var account = (await users.FindByIdAsync(ownerId))!;
        account.IsBlocked = true;
        Assert.True((await users.UpdateAsync(account)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Student_and_parent_are_retrievable_in_class_after_new_login()
    {
        var email = $"roster-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        using var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var response = await TestApplication.Post(client, "/api/students", new { name = "Nguyễn An", parentName = "Nguyễn Bình", phone = "0901234567", email = "binh@example.test", notes = "Liên hệ buổi tối" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var student = await response.Content.ReadFromJsonAsync<JsonElement>();
        var path = $"/api/classes/{classroom.GetProperty("id").GetString()}/enrollments";
        var enrollment = await TestApplication.Post(client, path, new { studentId = student.GetProperty("id").GetString(), startDate = "2026-10-01" });
        Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);
        using var reload = app.Client();
        await TestApplication.Login(reload, email);
        var roster = await reload.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal("Nguyễn An", roster[0].GetProperty("student").GetProperty("name").GetString());
        Assert.Equal("Nguyễn Bình", roster[0].GetProperty("student").GetProperty("parentName").GetString());
        Assert.Equal("0901234567", roster[0].GetProperty("student").GetProperty("phone").GetString());
        Assert.Equal("active", roster[0].GetProperty("status").GetString());
    }
}
