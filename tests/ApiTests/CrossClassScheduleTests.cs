using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

// GET /api/schedules (UX8-7): the weekly schedules of the teacher's other classes, so a new slot can be checked for
// overlap. Read-only, owner-scoped, current revisions only. Saving an overlapping schedule is never refused.
public sealed class CrossClassScheduleTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string Day(TestApplication app, int offset) =>
        DateOnly.FromDateTime(app.Clock.GetUtcNow().AddHours(7).AddDays(offset).DateTime).ToString("yyyy-MM-dd");

    private static async Task<string> NewClass(HttpClient client, string name)
    {
        var created = await TestApplication.Post(client, "/api/classes", new { name, subject = "Toán" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await Json(created)).GetProperty("id").GetString()!;
    }

    private static async Task<HttpResponseMessage> Save(HttpClient client, string classId, int version, string from, params (int Day, string Start, string End)[] slots) =>
        await TestApplication.Put(client, $"/api/classes/{classId}/schedule", new
        {
            version,
            effectiveFrom = from,
            slots = slots.Select(x => new { dayOfWeek = x.Day, startTime = x.Start, endTime = x.End }).ToArray()
        });

    [Fact]
    public async Task Returns_the_other_classes_current_schedules_and_nothing_else()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("planner@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "planner@example.test");
        var a = await NewClass(teacher, "Lớp A");
        var b = await NewClass(teacher, "Lớp B");
        var empty = await NewClass(teacher, "Lớp trống");
        var future = await NewClass(teacher, "Lớp chưa bắt đầu");

        Assert.Equal(HttpStatusCode.OK, (await Save(teacher, a, 0, Day(app, -7), (1, "17:00", "18:00"))).StatusCode);
        // Version 2 replaces version 1: only the revision in effect is returned.
        Assert.Equal(HttpStatusCode.OK, (await Save(teacher, a, 1, Day(app, -1), (1, "18:00", "19:30"), (3, "18:00", "19:30"))).StatusCode);
        // A revision that has not started yet is not "in effect": the earlier one still is.
        Assert.Equal(HttpStatusCode.OK, (await Save(teacher, a, 2, Day(app, 30), (5, "08:00", "09:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Save(teacher, b, 0, Day(app, 0), (2, "09:00", "10:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Save(teacher, future, 0, Day(app, 10), (4, "09:00", "10:00"))).StatusCode);

        var all = await teacher.GetFromJsonAsync<JsonElement>("/api/schedules");
        Assert.Equal(["Lớp A", "Lớp B"], all.EnumerateArray().Select(x => x.GetProperty("className").GetString()).ToArray());
        var first = all[0];
        Assert.Equal(a, first.GetProperty("classId").GetString());
        Assert.Equal(Day(app, -1), first.GetProperty("effectiveFrom").GetString());
        Assert.Equal([1, 3], first.GetProperty("slots").EnumerateArray().Select(x => x.GetProperty("dayOfWeek").GetInt32()).ToArray());
        Assert.Equal("18:00:00", first.GetProperty("slots")[0].GetProperty("startTime").GetString());
        Assert.Equal("19:30:00", first.GetProperty("slots")[0].GetProperty("endTime").GetString());

        var others = await teacher.GetFromJsonAsync<JsonElement>($"/api/schedules?exceptClassId={b}");
        Assert.Equal(a, Assert.Single(others.EnumerateArray()).GetProperty("classId").GetString());
        Assert.DoesNotContain(all.EnumerateArray(), x => x.GetProperty("classId").GetString() == empty || x.GetProperty("classId").GetString() == future);
    }

    [Fact]
    public async Task A_teacher_never_sees_another_teachers_schedules()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("first@example.test");
        await app.AddTeacher("second@example.test");
        using var one = app.Client();
        using var two = app.Client();
        await TestApplication.Login(one, "first@example.test");
        await TestApplication.Login(two, "second@example.test");
        var secret = await NewClass(one, "Lớp riêng của giáo viên một");
        Assert.Equal(HttpStatusCode.OK, (await Save(one, secret, 0, Day(app, -1), (1, "18:00", "19:30"))).StatusCode);
        var mine = await NewClass(two, "Lớp của giáo viên hai");
        Assert.Equal(HttpStatusCode.OK, (await Save(two, mine, 0, Day(app, -1), (1, "18:00", "19:30"))).StatusCode);

        var seen = await two.GetFromJsonAsync<JsonElement>("/api/schedules");
        Assert.Equal(mine, Assert.Single(seen.EnumerateArray()).GetProperty("classId").GetString());
        // Naming the other teacher's class in the filter changes nothing and reveals nothing.
        var guessed = await two.GetFromJsonAsync<JsonElement>($"/api/schedules?exceptClassId={secret}");
        Assert.Equal(mine, Assert.Single(guessed.EnumerateArray()).GetProperty("classId").GetString());
        Assert.DoesNotContain("giáo viên một", (await two.GetStringAsync("/api/schedules")));
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/schedules")).StatusCode);
    }

    [Fact]
    public async Task An_overlapping_schedule_is_still_saved_and_the_read_works_after_expiry()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        await app.AddTeacher("coteach@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "coteach@example.test");
        var a = await NewClass(teacher, "Lớp A");
        var b = await NewClass(teacher, "Lớp B");
        Assert.Equal(HttpStatusCode.OK, (await Save(teacher, a, 0, Day(app, -1), (1, "18:00", "19:30"))).StatusCode);
        // Same day, overlapping hours, other class: allowed (team teaching), same status code as a clean save.
        var overlapping = await Save(teacher, b, 0, Day(app, 0), (1, "19:00", "20:00"));
        Assert.Equal(HttpStatusCode.OK, overlapping.StatusCode);
        Assert.Equal(1, (await Json(overlapping)).GetProperty("version").GetInt32());

        app.Clock.Advance(TimeSpan.FromDays(31));
        using var later = app.Client();
        await TestApplication.Login(later, "coteach@example.test");
        Assert.Equal("expired", (await later.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("status").GetString());
        var afterExpiry = await later.GetAsync("/api/schedules");
        Assert.Equal(HttpStatusCode.OK, afterExpiry.StatusCode);
        Assert.Equal(2, (await Json(afterExpiry)).GetArrayLength());
    }
}
