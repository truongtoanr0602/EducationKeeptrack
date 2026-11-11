using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed class ServicePeriodTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private string AdminKey = "";

    private async Task<(TestApplication App, HttpClient Admin)> Start()
    {
        var app = new TestApplication(); await app.InitializeAsync();
        AdminKey = await app.AddAdmin("period-admin@example.test");
        var admin = app.Client();
        await app.LoginAdmin(admin, "period-admin@example.test", AdminKey);
        return (app, admin);
    }

    private static async Task<JsonElement> Detail(HttpClient admin, string id) => await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{id}");

    private static async Task<string> Status(HttpClient teacher) =>
        (await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("status").GetString()!;

    [Fact]
    public async Task Renewal_continues_at_the_end_and_expiry_is_read_only()
    {
        var (app, admin) = await Start();
        await using var _ = app;
        var t0 = app.Clock.GetUtcNow();
        var id = await app.AddTeacher("renewing@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "renewing@example.test");
        async Task Relogin() => await app.LoginAdmin(admin, "period-admin@example.test", AdminKey);

        var first = (await Detail(admin, id)).GetProperty("periods").EnumerateArray().Single();
        var edit = new { endsAt = t0.AddHours(2), label = "Kỳ thử", reason = "Rút ngắn để kiểm thử", version = first.GetProperty("version").GetUInt32() };
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Put(admin, $"/api/admin/periods/{first.GetProperty("id").GetString()}", edit with { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Put(admin, $"/api/admin/periods/{first.GetProperty("id").GetString()}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Put(admin, $"/api/admin/periods/{first.GetProperty("id").GetString()}", edit)).StatusCode);

        var preview = await Json(await TestApplication.Post(admin, $"/api/admin/teachers/{id}/period-preview", new { days = 30 }));
        Assert.Equal(t0.AddHours(2), preview.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/periods", new { days = 30, label = "Gia hạn", expectedStartsAt = t0.AddHours(2), reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/periods", new { days = 30, label = "Gia hạn", expectedStartsAt = t0.AddHours(2), reason = "Gia hạn sớm" })).StatusCode);

        // Early renewal does not start early.
        var service = await teacher.GetFromJsonAsync<JsonElement>("/api/service");
        var periods = service.GetProperty("periods").EnumerateArray().ToList();
        Assert.True(periods[0].GetProperty("current").GetBoolean());
        Assert.True(periods[1].GetProperty("upcoming").GetBoolean());
        Assert.Equal(t0.AddHours(2), periods[1].GetProperty("startsAt").GetDateTimeOffset());

        app.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(teacher, "/api/classes", new { name = "Trước ranh giới", subject = "Toán" })).StatusCode);
        app.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal("active", await Status(teacher));
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(teacher, "/api/classes", new { name = "Đúng ranh giới", subject = "Toán" })).StatusCode);

        await Relogin();
        var second = (await Detail(admin, id)).GetProperty("periods").EnumerateArray().Last();
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Put(admin, $"/api/admin/periods/{first.GetProperty("id").GetString()}", new { endsAt = t0.AddHours(3), label = "", reason = "Sửa kỳ cũ", version = first.GetProperty("version").GetUInt32() + 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Put(admin, $"/api/admin/periods/{second.GetProperty("id").GetString()}", new { endsAt = app.Clock.GetUtcNow().AddHours(1), label = "Gia hạn", reason = "Kết thúc sớm để kiểm thử", version = second.GetProperty("version").GetUInt32() })).StatusCode);
        app.Clock.Advance(TimeSpan.FromHours(1).Subtract(TimeSpan.FromSeconds(1)));
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(teacher, "/api/classes", new { name = "Giây cuối", subject = "Toán" })).StatusCode);
        app.Clock.Advance(TimeSpan.FromSeconds(1));

        // [start, end): at the end instant the period is over.
        Assert.Equal("expired", await Status(teacher));
        var denied = await TestApplication.Post(teacher, "/api/classes", new { name = "Sau hạn", subject = "Toán" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("expired", (await Json(denied)).GetProperty("status").GetString());
        Assert.Equal(3, (await teacher.GetFromJsonAsync<JsonElement>("/api/classes")).GetArrayLength());

        app.Clock.Advance(TimeSpan.FromMinutes(10));
        await Relogin();
        var restart = await Json(await TestApplication.Post(admin, $"/api/admin/teachers/{id}/period-preview", new { days = 30 }));
        Assert.Equal(app.Clock.GetUtcNow(), restart.GetProperty("startsAt").GetDateTimeOffset());
        var history = (await Detail(admin, id)).GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()).ToList();
        Assert.Contains("period.granted", history);
        Assert.Equal(2, history.Count(x => x == "period.updated"));
    }

    [Fact]
    public async Task Lock_revokes_sessions_and_unlock_does_not_extend()
    {
        var (app, admin) = await Start();
        await using var _ = app;
        var id = await app.AddTeacher("locked@example.test");
        using var first = app.Client();
        using var second = app.Client();
        await TestApplication.Login(first, "locked@example.test");
        await TestApplication.Login(second, "locked@example.test");
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(first, "/api/classes", new { name = "Lớp bị khóa", subject = "Toán" })).StatusCode);
        var before = (await Detail(admin, id)).GetProperty("periods").EnumerateArray().Single().GetProperty("endsAt").GetDateTimeOffset();

        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/lock", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/lock", new { reason = "Vi phạm điều khoản" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(second, "/api/classes", new { name = "Sau khóa", subject = "Toán" })).StatusCode);
        using var retry = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await TestApplication.Post(retry, "/api/auth/login", new { email = "locked@example.test", password = TestApplication.Password })).StatusCode);
        Assert.True((await Detail(admin, id)).GetProperty("isBlocked").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/period-preview", new { days = 30 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/classes")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/unlock", new { reason = "Đã xác minh lại" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/classes")).StatusCode);
        await TestApplication.Login(retry, "locked@example.test");
        Assert.Equal("active", await Status(retry));
        var detail = await Detail(admin, id);
        Assert.Equal(before, detail.GetProperty("periods").EnumerateArray().Single().GetProperty("endsAt").GetDateTimeOffset());
        var actions = detail.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()).ToList();
        Assert.Contains("account.locked", actions);
        Assert.Contains("account.unlocked", actions);
    }

    [Fact]
    public async Task Active_teacher_renews_through_a_payment_request()
    {
        var (app, admin) = await Start();
        await using var _ = app;
        var id = await app.AddTeacher("payrenew@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "payrenew@example.test");
        var currentEnd = (await Detail(admin, id)).GetProperty("periods").EnumerateArray().Single().GetProperty("endsAt").GetDateTimeOffset();
        var requestId = (await Json(await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "RENEW-1", note = "", idempotencyKey = "renew" }))).GetProperty("id").GetString();
        var request = (await Detail(admin, id)).GetProperty("requests").EnumerateArray().Single();
        var approved = await Json(await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", new { days = 60, label = "Gia hạn", expectedStartsAt = currentEnd, version = request.GetProperty("version").GetUInt32() }));
        Assert.Equal(currentEnd, approved.GetProperty("startsAt").GetDateTimeOffset());
        Assert.Equal(currentEnd.AddDays(60), approved.GetProperty("endsAt").GetDateTimeOffset());
    }
}
