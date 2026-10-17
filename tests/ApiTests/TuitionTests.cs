using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace ApiTests;

public sealed class TuitionTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, string ClassId, string StudentId, string Email)> Setup(TestApplication? application = null)
    {
        application ??= app;
        var email = $"tuition-{Guid.NewGuid():N}@example.test";
        await application.AddTeacher(email);
        var client = application.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, classId, studentId, email);
    }
    private static async Task<string> Session(HttpClient client, string classId, string studentId, string date, string mark = "present", string status = "held", string kind = "regular", string? replacesSessionId = null)
    {
        var path = $"/api/classes/{classId}/sessions";
        var created = await TestApplication.Post(client, path, new { date, startTime = "18:00:00", endTime = "19:00:00", kind, replacesSessionId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        if (status != "planned") Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/{id}", new { status = "held", version = 1 })).StatusCode);
        if (mark != "unmarked" && status != "planned") Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/{id}/attendance/{studentId}", new { status = mark, version = 0 })).StatusCode);
        if (status == "cancelled") Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/{id}", new { status, reason = "Hủy buổi", version = mark == "unmarked" ? 2 : 3 })).StatusCode);
        return id;
    }
    private static async Task<JsonElement> Ready(HttpClient client, string classId, string month = "2026-10")
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var response = await client.GetAsync($"/api/billing/classes/{classId}/tuition?month={month}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.GetProperty("status").GetString() != "pending") return result;
            Assert.True(DateTime.UtcNow < deadline, "Tuition projection did not catch up");
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task Effective_prices_exclusions_makeups_and_attendance_corrections_keep_old_session_price()
    {
        var (client, classId, studentId, _) = await Setup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            var old = await Session(client, classId, studentId, "2026-10-02");
            await Ready(client, classId);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-16", unitPriceVnd = "200000", version = 1 })).StatusCode);
            await Session(client, classId, studentId, "2026-10-20");
            await Session(client, classId, studentId, "2026-10-21", "absent");
            await Session(client, classId, studentId, "2026-10-22", "unmarked");
            await Session(client, classId, studentId, "2026-10-23", "unmarked", "planned");
            var cancelled = await Session(client, classId, studentId, "2026-10-24", "present", "cancelled");
            await Session(client, classId, studentId, "2026-10-25", "present", "held", "makeup", cancelled);
            var fees = await Ready(client, classId);
            Assert.Equal("550000", fees.GetProperty("totalVnd").GetString());
            var lines = fees.GetProperty("students")[0].GetProperty("lines").EnumerateArray().ToArray();
            Assert.Equal("150000", lines.Single(x => x.GetProperty("sessionId").GetString() == old).GetProperty("unitPriceVnd").GetString());
            Assert.Equal(4, lines.Count(x => x.GetProperty("amountVnd").GetString() == "0"));
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{old}/attendance/{studentId}", new { status = "absent", version = 1 })).StatusCode);
            var changed = await Ready(client, classId);
            Assert.Equal("400000", changed.GetProperty("totalVnd").GetString());
            Assert.True(changed.GetProperty("version").GetInt64() > fees.GetProperty("version").GetInt64());
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{old}/attendance/{studentId}", new { status = "present", version = 2 })).StatusCode);
            Assert.Equal("550000", (await Ready(client, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-02", unitPriceVnd = "999999", version = 2 })).StatusCode);
        }
    }

    [Fact]
    public async Task Missing_price_is_explicit_and_first_price_backfills_existing_actual_sessions()
    {
        var (client, classId, studentId, _) = await Setup();
        using (client)
        {
            await Session(client, classId, studentId, "2026-10-02");
            var unpriced = await Ready(client, classId);
            Assert.Equal("missing-rate", unpriced.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, unpriced.GetProperty("students")[0].GetProperty("lines")[0].GetProperty("amountVnd").ValueKind);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            Assert.Equal("150000", (await Ready(client, classId)).GetProperty("totalVnd").GetString());
        }
    }

    [Fact]
    public async Task Restart_replays_unacknowledged_events_without_duplicating_fees_or_versions()
    {
        var failing = new DeliveryProbe(true);
        await using var original = new TestApplication { ClassroomChangeHandler = failing, TuitionChangeHandler = failing };
        await original.InitializeAsync();
        var (client, classId, studentId, email) = await Setup(original);
        using (client)
        {
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            await Session(client, classId, studentId, "2026-10-02");
            var before = await Ready(client, classId);
            var initialClassroomEvent = await failing.ClassroomDelivery.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var initialRateEvent = await failing.RateDelivery.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await original.StopHostAsync();
            var replay = new DeliveryProbe(false);
            await using var restarted = original.Reopen(replay, replay);
            await restarted.InitializeAsync();
            using var reload = restarted.Client();
            await TestApplication.Login(reload, email);
            var replayedClassroomEvent = await replay.ClassroomDelivery.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var replayedRateEvent = await replay.RateDelivery.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var after = await Ready(reload, classId);
            Assert.Equal("150000", after.GetProperty("totalVnd").GetString());
            Assert.Equal(before.GetProperty("version").GetInt64(), after.GetProperty("version").GetInt64());
            Assert.Equal(1, after.GetProperty("students")[0].GetProperty("lines").GetArrayLength());
            Assert.Equal(initialRateEvent.EventId, replayedRateEvent.EventId);
            Assert.Equal(initialClassroomEvent.EventId, replayedClassroomEvent.EventId);
        }
    }
    private sealed class DeliveryProbe(bool fail) : IClassroomChangeHandler, ITuitionChangeHandler
    {
        public TaskCompletionSource<ClassroomChange> ClassroomDelivery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TuitionChange> RateDelivery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task HandleAsync(ClassroomChange change, CancellationToken ct)
        {
            ClassroomDelivery.TrySetResult(change);
            return fail ? Task.FromException(new InvalidOperationException("Stop before Classroom acknowledgment")) : Task.CompletedTask;
        }
        public Task HandleAsync(TuitionChange change, CancellationToken ct)
        {
            if (change.Kind == "rate.changed") RateDelivery.TrySetResult(change);
            return fail ? Task.FromException(new InvalidOperationException("Stop before Billing acknowledgment")) : Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Delayed_delivery_marks_cached_total_pending_then_updates_after_recovery()
    {
        var outage = new TemporaryOutage();
        await using var delayedApp = new TestApplication { DecorateBillingHandler = handler => new GatedSubscription(outage, handler) };
        await delayedApp.InitializeAsync();
        var (client, classId, studentId, _) = await Setup(delayedApp);
        using (client)
        {
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            var id = await Session(client, classId, studentId, "2026-10-02");
            var before = await Ready(client, classId);
            outage.Fail = true;
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{id}/attendance/{studentId}", new { status = "absent", version = 1 })).StatusCode);
            var pending = await client.GetFromJsonAsync<JsonElement>($"/api/billing/classes/{classId}/tuition?month=2026-10");
            Assert.Equal("pending", pending.GetProperty("status").GetString());
            Assert.Equal("150000", pending.GetProperty("totalVnd").GetString());
            outage.Fail = false;
            var after = await Ready(client, classId);
            Assert.Equal("0", after.GetProperty("totalVnd").GetString());
            Assert.True(after.GetProperty("version").GetInt64() > before.GetProperty("version").GetInt64());
        }
    }
    private sealed class TemporaryOutage
    {
        public volatile bool Fail;
    }
    private sealed class GatedSubscription(TemporaryOutage outage, IClassroomChangeHandler subscriber) : IClassroomChangeHandler
    {
        public Task HandleAsync(ClassroomChange change, CancellationToken ct) => outage.Fail
            ? Task.FromException(new InvalidOperationException("Billing subscription unavailable"))
            : subscriber.HandleAsync(change, ct);
    }

    [Fact]
    public async Task Rate_validation_versions_and_cross_teacher_access_are_enforced()
    {
        var (client, classId, _, _) = await Setup();
        var (other, _, _, _) = await Setup();
        using (client) using (other)
        {
            var path = $"/api/billing/classes/{classId}";
            foreach (var value in new[] { "-1", "1.5", "1000000000000000000" })
                Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = value, version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-16", unitPriceVnd = "200000", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{path}/tuition?month=2026-99")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/rates")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/tuition?month=2026-10")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "999", version = 0 })).StatusCode);
        }
    }

    [Fact]
    public async Task Expired_teacher_can_read_old_fees_but_cannot_change_prices_and_blocking_revokes_reads()
    {
        var (client, classId, studentId, email) = await Setup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            await Session(client, classId, studentId, "2026-10-02"); await Ready(client, classId);
            app.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = app.Client(); await TestApplication.Login(expired, email);
            Assert.Equal("150000", (await Ready(expired, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal(HttpStatusCode.OK, (await expired.GetAsync($"{path}/rates")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, $"{path}/rates", new { effectiveFrom = "2026-10-16", unitPriceVnd = "200000", version = 1 })).StatusCode);
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>(); var account = (await users.FindByEmailAsync(email))!;
            account.IsBlocked = true; Assert.True((await users.UpdateAsync(account)).Succeeded);
            Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync($"{path}/tuition?month=2026-10")).StatusCode);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Unapproved_or_unverified_accounts_cannot_access_billing(bool approved, bool confirmed)
    {
        var email = $"denied-tuition-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email, approved, confirmed);
        using var client = app.Client();
        var login = await TestApplication.Post(client, "/api/auth/login", new { email, password = "Test-only!Password123" });
        var expected = login.IsSuccessStatusCode ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized;
        var path = $"/api/billing/classes/{Guid.NewGuid()}";
        Assert.Equal(expected, (await client.GetAsync($"{path}/rates")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"{path}/tuition?month=2026-10")).StatusCode);
        Assert.Equal(expected, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
    }

    [Fact]
    public async Task Whole_dong_amounts_above_javascript_safe_integer_and_zero_rates_remain_exact()
    {
        var (client, classId, studentId, _) = await Setup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/rates";
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, new { effectiveFrom = "2026-10-01", unitPriceVnd = "999999999999999999", version = 0 })).StatusCode);
            await Session(client, classId, studentId, "2026-10-02"); await Session(client, classId, studentId, "2026-10-03");
            Assert.Equal("1999999999999999998", (await Ready(client, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, new { effectiveFrom = "2026-10-16", unitPriceVnd = "0", version = 1 })).StatusCode);
            var free = await Session(client, classId, studentId, "2026-10-20");
            var fees = await Ready(client, classId);
            Assert.Equal("ready", fees.GetProperty("status").GetString());
            Assert.Equal("1999999999999999998", fees.GetProperty("totalVnd").GetString());
            Assert.Equal("0", fees.GetProperty("students")[0].GetProperty("lines").EnumerateArray().Single(x => x.GetProperty("sessionId").GetString() == free).GetProperty("unitPriceVnd").GetString());
        }
    }

    [Fact]
    public async Task Early_morning_session_belongs_to_vietnam_month_rather_than_utc_month()
    {
        var (client, classId, studentId, _) = await Setup();
        using (client)
        {
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            var created = await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date = "2026-10-01", startTime = "00:30:00", endTime = "01:30:00", kind = "regular" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var session = await created.Content.ReadFromJsonAsync<JsonElement>(); var id = session.GetProperty("id").GetString();
            Assert.Equal(DateTimeOffset.Parse("2026-09-30T17:30:00Z"), session.GetProperty("startsAtUtc").GetDateTimeOffset());
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 0 })).StatusCode);
            Assert.Equal("150000", (await Ready(client, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal("0", (await Ready(client, classId, "2026-09")).GetProperty("totalVnd").GetString());
        }
    }

    [Fact]
    public async Task Eight_present_sessions_at_150000_are_persisted_as_1200000()
    {
        var (client, classId, studentId, email) = await Setup();
        using (client)
        {
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "150000", version = 0 })).StatusCode);
            for (var day = 2; day <= 9; day++) await Session(client, classId, studentId, $"2026-10-{day:00}");
            var fees = await Ready(client, classId);
            Assert.Equal("ready", fees.GetProperty("status").GetString());
            Assert.Equal("1200000", fees.GetProperty("totalVnd").GetString());
            Assert.Equal(8, fees.GetProperty("students")[0].GetProperty("lines").GetArrayLength());
            Assert.All(fees.GetProperty("students")[0].GetProperty("lines").EnumerateArray(), line => Assert.Equal("150000", line.GetProperty("amountVnd").GetString()));
            using var reload = app.Client();
            await TestApplication.Login(reload, email);
            Assert.Equal("1200000", (await Ready(reload, classId)).GetProperty("totalVnd").GetString());
        }
    }
}
