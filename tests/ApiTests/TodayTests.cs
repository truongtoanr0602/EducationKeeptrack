using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

public sealed class TodayTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private async Task<HttpClient> Teacher()
    {
        var email = $"today-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        return client;
    }
    private static async Task<string> Json(HttpResponseMessage response, string property)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(property).GetString()!;
    }
    private static async Task<(string ClassId, string SessionId)> ClassWithSession(HttpClient client, string name, string date, string start, string end, string[] students)
    {
        var classId = await Json(await TestApplication.Post(client, "/api/classes", new { name, subject = "Toán" }), "id");
        foreach (var student in students)
        {
            var studentId = await Json(await TestApplication.Post(client, "/api/students", new { name = student }), "id");
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        }
        var sessionId = await Json(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date, startTime = start, endTime = end, kind = "regular" }), "id");
        return (classId, sessionId);
    }

    [Fact]
    public async Task Today_lists_owner_sessions_across_classes_in_time_order_with_attendance_counts()
    {
        using var client = await Teacher();
        using var other = await Teacher();
        var (classA, sessionA) = await ClassWithSession(client, "Lớp chiều", "2026-10-05", "18:00:00", "19:00:00", ["An", "Bình"]);
        var (classB, _) = await ClassWithSession(client, "Lớp sáng", "2026-10-05", "08:00:00", "09:00:00", ["Chi"]);
        await ClassWithSession(client, "Lớp ngày khác", "2026-10-06", "08:00:00", "09:00:00", ["Dũng"]);
        await ClassWithSession(other, "Lớp người khác", "2026-10-05", "07:00:00", "08:00:00", ["Em"]);
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classA}/sessions/{sessionA}", new { status = "held", version = 1 })).StatusCode);
        var roster = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{classA}/sessions/{sessionA}/attendance");
        var first = roster[0].GetProperty("studentId").GetString();
        var second = roster[1].GetProperty("studentId").GetString();
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classA}/sessions/{sessionA}/attendance/{first}", new { status = "present", version = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classA}/sessions/{sessionA}/attendance/{second}", new { status = "absent", excused = true, version = 0 })).StatusCode);

        var today = await client.GetFromJsonAsync<JsonElement>("/api/today?date=2026-10-05");

        Assert.Equal("2026-10-05", today.GetProperty("date").GetString());
        var sessions = today.GetProperty("sessions");
        Assert.Equal(2, sessions.GetArrayLength());
        Assert.Equal(new[] { "Lớp sáng", "Lớp chiều" }, sessions.EnumerateArray().Select(x => x.GetProperty("className").GetString()));
        Assert.Equal(classB, sessions[0].GetProperty("classId").GetString());
        Assert.Equal("planned", sessions[0].GetProperty("session").GetProperty("status").GetString());
        Assert.Equal(1, sessions[0].GetProperty("students").GetInt32());
        Assert.Equal(0, sessions[0].GetProperty("present").GetInt32());
        Assert.Equal("held", sessions[1].GetProperty("session").GetProperty("status").GetString());
        Assert.Equal(2, sessions[1].GetProperty("students").GetInt32());
        Assert.Equal(1, sessions[1].GetProperty("present").GetInt32());
        Assert.Equal(1, sessions[1].GetProperty("absent").GetInt32());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/today?date=2026-10-07")).GetProperty("sessions").GetArrayLength());
    }

    [Fact]
    public async Task Today_requires_a_signed_in_teacher()
    {
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/today?date=2026-10-05")).StatusCode);
    }
}
