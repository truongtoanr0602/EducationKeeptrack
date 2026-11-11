using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTests;

// The future AI modules (grading, comments) reach quota only through IAiQuota; these tests play that
// simulated AI adapter and check balances through the HTTP API.
public sealed class AiQuotaTests
{
    private string adminKey = "";

    private async Task<(TestApplication App, HttpClient Admin)> Start()
    {
        var app = new TestApplication(); await app.InitializeAsync();
        adminKey = await app.AddAdmin("ai-admin@example.test");
        var admin = app.Client();
        await app.LoginAdmin(admin, "ai-admin@example.test", adminKey);
        return (app, admin);
    }

    private static async Task<T> Quota<T>(TestApplication app, Func<IAiQuota, Task<T>> action)
    {
        using var scope = app.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IAiQuota>());
    }

    private static Task<AiReservation> Reserve(TestApplication app, string id, AiTaskKind kind, string key, int units = 1) =>
        Quota(app, q => q.ReserveAsync(id, kind, key, units, CancellationToken.None));

    private static async Task<JsonElement> Detail(HttpClient admin, string id) => await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{id}");

    private static async Task<JsonElement> Balance(HttpClient teacher, string kind) =>
        (await teacher.GetFromJsonAsync<JsonElement>("/api/service")).GetProperty("ai").GetProperty("quotas").EnumerateArray().Single(x => x.GetProperty("kind").GetString() == kind);

    private static void AssertBalance(JsonElement balance, int? granted, int held, int used)
    {
        if (granted is null) Assert.Equal(JsonValueKind.Null, balance.GetProperty("granted").ValueKind);
        else Assert.Equal(granted, balance.GetProperty("granted").GetInt32());
        Assert.Equal(held, balance.GetProperty("held").GetInt32());
        Assert.Equal(used, balance.GetProperty("used").GetInt32());
        Assert.Equal(granted is null ? 0 : Math.Max(0, granted.Value - held - used), balance.GetProperty("remaining").GetInt32());
    }

    private static async Task SetAi(HttpClient admin, string id, bool enabled)
    {
        var version = (await Detail(admin, id)).GetProperty("accountVersion").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/ai", new { enabled, reason = enabled ? "Gói có AI" : "Tạm tắt", version })).StatusCode);
    }

    private static async Task SetQuotas(HttpClient admin, string id, int? grading, int? comment, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var period = (await Detail(admin, id)).GetProperty("periods").EnumerateArray().Single(x => x.GetProperty("current").GetBoolean());
        Assert.Equal(expected, (await TestApplication.Put(admin, $"/api/admin/periods/{period.GetProperty("id").GetString()}/quotas",
            new { grading, comment, reason = "Cấp hạn mức", version = period.GetProperty("version").GetUInt32() })).StatusCode);
    }

    [Fact]
    public async Task Ai_needs_the_flag_and_an_explicit_quota_but_manual_work_continues()
    {
        var (app, admin) = await Start();
        await using var _ = app;
        var id = await app.AddTeacher("ai-teacher@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "ai-teacher@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(teacher, $"/api/admin/teachers/{id}/ai", new { enabled = true, reason = "tự bật", version = 0 })).StatusCode);

        Assert.Equal("ai_disabled", (await Reserve(app, id, AiTaskKind.Grading, "g-0")).DeniedReason);
        AssertBalance(await Balance(teacher, "grading"), null, 0, 0);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(teacher, "/api/classes", new { name = "Không AI", subject = "Toán" })).StatusCode);

        await SetAi(admin, id, true);
        Assert.True((await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("canUseAi").GetBoolean());
        // Enabled but no quota configured for this period: nothing is granted implicitly.
        Assert.Equal("no_quota", (await Reserve(app, id, AiTaskKind.Grading, "g-1")).DeniedReason);

        await SetQuotas(admin, id, grading: 2, comment: null);
        var first = await Reserve(app, id, AiTaskKind.Grading, "g-2");
        Assert.Equal(AiReservationOutcome.Reserved, first.Outcome);
        Assert.Equal("no_quota", (await Reserve(app, id, AiTaskKind.Comment, "c-1")).DeniedReason);
        AssertBalance(await Balance(teacher, "grading"), 2, 1, 0);
        AssertBalance(await Balance(teacher, "comment"), null, 0, 0);

        // Turning AI off blocks new work, lets accepted work finish, and never resets counters.
        await SetAi(admin, id, false);
        Assert.Equal("ai_disabled", (await Reserve(app, id, AiTaskKind.Grading, "g-3")).DeniedReason);
        Assert.True(await Quota(app, q => q.CommitAsync(first.ReservationId!.Value, CancellationToken.None)));
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(teacher, "/api/classes", new { name = "Vẫn thủ công", subject = "Toán" })).StatusCode);
        await SetAi(admin, id, true);
        AssertBalance(await Balance(teacher, "grading"), 2, 0, 1);

        await SetQuotas(admin, id, grading: 0, comment: 1, expected: HttpStatusCode.Conflict);
        var history = (await Detail(admin, id)).GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()).ToList();
        Assert.Equal(3, history.Count(x => x == "ai.enabled" || x == "ai.disabled"));
        Assert.Contains("quota.updated", history);
    }

    [Fact]
    public async Task Concurrent_duplicate_failed_and_restarted_requests_keep_the_ledger_exact()
    {
        var (app, admin) = await Start();
        await using var _ = app;
        var id = await app.AddTeacher("ledger@example.test");
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "ledger@example.test");
        await SetAi(admin, id, true);
        await SetQuotas(admin, id, grading: 3, comment: 5);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Reserve(app, id, AiTaskKind.Grading, $"bai-{i}")));
        var reserved = attempts.Where(x => x.Outcome == AiReservationOutcome.Reserved).ToList();
        Assert.Equal(3, reserved.Count);
        Assert.All(attempts.Where(x => x.Outcome == AiReservationOutcome.Denied), x => Assert.Equal("exhausted", x.DeniedReason));
        AssertBalance(await Balance(teacher, "grading"), 3, 3, 0);

        var key = attempts.Select((x, i) => (x, i)).First(p => p.x.Outcome == AiReservationOutcome.Reserved).i;
        var again = await Reserve(app, id, AiTaskKind.Grading, $"bai-{key}");
        Assert.Equal(AiReservationOutcome.Duplicate, again.Outcome);
        Assert.Equal(reserved[0].ReservationId, again.ReservationId);

        // Valid result: deducted once even if the callback repeats. Error: returned. Invalid output: returned.
        Assert.True(await Quota(app, q => q.CommitAsync(reserved[0].ReservationId!.Value, CancellationToken.None)));
        Assert.True(await Quota(app, q => q.CommitAsync(reserved[0].ReservationId!.Value, CancellationToken.None)));
        await Quota(app, async q => { await q.ReleaseAsync(reserved[1].ReservationId!.Value, CancellationToken.None); return 0; });
        await Quota(app, async q => { await q.ReleaseAsync(reserved[0].ReservationId!.Value, CancellationToken.None); return 0; });
        Assert.False(await Quota(app, q => q.CommitAsync(reserved[1].ReservationId!.Value, CancellationToken.None)));
        AssertBalance(await Balance(teacher, "grading"), 3, 1, 1);

        // A host restart keeps the hold; the result arriving afterwards is still settled once.
        await app.StopHostAsync();
        await using var restarted = app.Reopen(); await restarted.InitializeAsync();
        Assert.True(await Quota(restarted, q => q.CommitAsync(reserved[2].ReservationId!.Value, CancellationToken.None)));
        using var after = restarted.Client();
        await TestApplication.Login(after, "ledger@example.test");
        AssertBalance(await Balance(after, "grading"), 3, 0, 2);
        AssertBalance(await Balance(after, "comment"), 5, 0, 0);

        // A regrade is a new request key and therefore a new turn.
        Assert.Equal(AiReservationOutcome.Reserved, (await Reserve(restarted, id, AiTaskKind.Grading, "bai-regrade")).Outcome);
        Assert.Equal("exhausted", (await Reserve(restarted, id, AiTaskKind.Grading, "bai-regrade-2")).DeniedReason);
    }

    [Fact]
    public async Task Turns_stay_with_their_period_and_lock_voids_late_results()
    {
        var (app, admin) = await Start();
        await using var _ = app;
        var id = await app.AddTeacher("boundary@example.test");
        await SetAi(admin, id, true);
        await SetQuotas(admin, id, grading: 2, comment: 2);
        var period = (await Detail(admin, id)).GetProperty("periods").EnumerateArray().Single();
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Put(admin, $"/api/admin/periods/{period.GetProperty("id").GetString()}",
            new { endsAt = app.Clock.GetUtcNow().AddMinutes(20), label = "", reason = "Kết thúc sớm", version = period.GetProperty("version").GetUInt32() })).StatusCode);
        var lateGrading = await Reserve(app, id, AiTaskKind.Grading, "truoc-ranh-gioi");
        var used = await Reserve(app, id, AiTaskKind.Grading, "da-dung");
        Assert.True(await Quota(app, q => q.CommitAsync(used.ReservationId!.Value, CancellationToken.None)));

        // Early renewal grants the new quota only when the new period starts; unused turns do not carry over.
        var preview = await (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/period-preview", new { days = 30 })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/periods",
            new { days = 30, label = "Kỳ 2", reason = "Gia hạn", expectedStartsAt = preview.GetProperty("startsAt").GetDateTimeOffset(), grading = 1, comment = 1 })).StatusCode);
        Assert.Equal("exhausted", (await Reserve(app, id, AiTaskKind.Grading, "chua-den-ky-moi")).DeniedReason);

        app.Clock.Advance(TimeSpan.FromMinutes(20));
        // The result arriving after the boundary still counts in the period that accepted it.
        Assert.True(await Quota(app, q => q.CommitAsync(lateGrading.ReservationId!.Value, CancellationToken.None)));
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "boundary@example.test");
        AssertBalance(await Balance(teacher, "grading"), 1, 0, 0);

        var running = await Reserve(app, id, AiTaskKind.Comment, "nhan-xet-dang-chay");
        Assert.Equal(AiReservationOutcome.Reserved, running.Outcome);
        await app.LoginAdmin(admin, "ai-admin@example.test", adminKey);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/lock", new { reason = "Khóa khi AI đang chạy" })).StatusCode);
        Assert.False(await Quota(app, q => q.CommitAsync(running.ReservationId!.Value, CancellationToken.None)));
        Assert.Equal("blocked", (await Reserve(app, id, AiTaskKind.Comment, "sau-khoa")).DeniedReason);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/teachers/{id}/unlock", new { reason = "Mở lại" })).StatusCode);
        Assert.False(await Quota(app, q => q.CommitAsync(running.ReservationId!.Value, CancellationToken.None)));
        using var again = app.Client();
        await TestApplication.Login(again, "boundary@example.test");
        AssertBalance(await Balance(again, "comment"), 1, 0, 0);
    }
}
