using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

public sealed class TodayRangeTests(TestApplication app) : IClassFixture<TestApplication>
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
        var email = $"range-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        return client;
    }
    private static async Task<string> Id(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
    private static async Task<string> ClassWithSchedule(HttpClient client, string name, object[] slots, string effectiveFrom = "2026-10-01")
    {
        var classId = await Id(await TestApplication.Post(client, "/api/classes", new { name, subject = "Toán" }));
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/schedule", new { version = 0, effectiveFrom, slots })).StatusCode);
        return classId;
    }
    private static object Slot(int day, string start, string end) => new { dayOfWeek = day, startTime = start, endTime = end };

    [Fact]
    public async Task Range_returns_sessions_and_unplanned_slots_following_the_schedule_revision_of_each_day()
    {
        using var client = await Teacher();
        using var other = await Teacher();
        var classId = await ClassWithSchedule(client, "Lớp tối", [Slot(1, "18:00:00", "19:00:00")]);
        await ClassWithSchedule(other, "Lớp người khác", [Slot(1, "07:00:00", "08:00:00")]);
        await Id(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" }));
        // From 12/10 the class moves from Monday to Wednesday.
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/schedule", new { version = 1, effectiveFrom = "2026-10-12", slots = new[] { Slot(3, "18:00:00", "19:00:00") } })).StatusCode);

        var range = await client.GetFromJsonAsync<JsonElement>("/api/today/range?from=2026-10-05&to=2026-10-18");

        Assert.Equal(1, range.GetProperty("sessions").GetArrayLength());
        Assert.Equal("2026-10-05", range.GetProperty("sessions")[0].GetProperty("session").GetProperty("date").GetString());
        var unplanned = range.GetProperty("unplanned").EnumerateArray().ToArray();
        Assert.Equal(["2026-10-14"], unplanned.Select(x => x.GetProperty("date").GetString()));
        Assert.Equal("Lớp tối", unplanned[0].GetProperty("className").GetString());
        Assert.Equal(classId, unplanned[0].GetProperty("classId").GetString());
    }

    [Fact]
    public async Task Range_does_not_mark_a_cancelled_session_as_unplanned_and_ignores_days_before_the_schedule_starts()
    {
        using var client = await Teacher();
        var classId = await ClassWithSchedule(client, "Lớp thứ Hai", [Slot(1, "18:00:00", "19:00:00")], "2026-10-12");
        var sessionId = await Id(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date = "2026-10-12", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" }));
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{sessionId}", new { status = "cancelled", reason = "Nghỉ lễ", version = 1 })).StatusCode);

        var range = await client.GetFromJsonAsync<JsonElement>("/api/today/range?from=2026-10-05&to=2026-10-19");

        Assert.Equal(["2026-10-19"], range.GetProperty("unplanned").EnumerateArray().Select(x => x.GetProperty("date").GetString()));
        Assert.Equal("cancelled", range.GetProperty("sessions")[0].GetProperty("session").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("2026-10-01", "2026-11-12")] // 43 days
    [InlineData("2026-10-10", "2026-10-09")] // reversed
    public async Task Range_rejects_a_reversed_or_too_long_range(string from, string to)
    {
        using var client = await Teacher();
        var response = await client.GetAsync($"/api/today/range?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Range_accepts_exactly_42_days()
    {
        using var client = await Teacher();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/today/range?from=2026-10-01&to=2026-11-11")).StatusCode);
    }
}
