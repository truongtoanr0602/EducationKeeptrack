using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace ApiTests;

public sealed class AttendanceTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private async Task<(HttpClient Client, string ClassId, string StudentId)> Setup(TestApplication? application = null)
    {
        application ??= app;
        var email = $"attendance-{Guid.NewGuid():N}@example.test";
        await application.AddTeacher(email);
        var client = application.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, classId, studentId);
    }

    [Fact]
    public async Task Cancelled_session_blocks_attendance_and_makeup_does_not_duplicate_time_or_source()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            var path = $"/api/classes/{classId}";
            var created = await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var session = await created.Content.ReadFromJsonAsync<JsonElement>();
            var id = session.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}", new { status = "cancelled", reason = "Giáo viên bận", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/sessions/{id}", new { status = "held", version = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:30:00", endTime = "19:30:00", kind = "makeup", replacesSessionId = id })).StatusCode);
            var makeup = await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-06", startTime = "18:00:00", endTime = "19:00:00", kind = "makeup", replacesSessionId = id });
            Assert.Equal(HttpStatusCode.Created, makeup.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-07", startTime = "18:00:00", endTime = "19:00:00", kind = "makeup", replacesSessionId = id })).StatusCode);
            var sessions = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions?from=2026-10-01&to=2026-10-31");
            Assert.Equal(2, sessions.GetArrayLength());
            Assert.Equal("cancelled", sessions[0].GetProperty("status").GetString());
            Assert.Equal("Giáo viên bận", sessions[0].GetProperty("reason").GetString());
            Assert.Equal(id, sessions[1].GetProperty("replacesSessionId").GetString());
        }
    }

    [Fact]
    public async Task Attendance_is_unique_rejects_stale_tabs_and_missing_list_excludes_cancelled_sessions()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            var path = $"/api/classes/{classId}";
            var session = await (await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = session.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            var missing = await client.GetFromJsonAsync<JsonElement>($"{path}/attendance-missing?from=2026-10-01&to=2026-10-31");
            Assert.Equal(studentId, missing[0].GetProperty("studentId").GetString());
            var changes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "absent", reason = "Bị ốm", excused = true, version = 0 })));
            Assert.Single(changes, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(changes, x => x.StatusCode == HttpStatusCode.Conflict);
            var attendance = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions/{id}/attendance");
            Assert.Equal(1, attendance.GetArrayLength());
            Assert.Equal("absent", attendance[0].GetProperty("status").GetString());
            Assert.True(attendance[0].GetProperty("excused").GetBoolean());
            Assert.Equal("Bị ốm", attendance[0].GetProperty("reason").GetString());
            Assert.Equal(1, attendance[0].GetProperty("version").GetInt32());
            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"{path}/attendance-missing?from=2026-10-01&to=2026-10-31")).GetArrayLength());
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "unmarked", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "unmarked", version = 2 })).StatusCode);
            // Session version also changes with attendance, so stale cancellation cannot discard newer work.
            var latest = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions?from=2026-10-01&to=2026-10-31");
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}", new { status = "cancelled", reason = "Hủy buổi", version = latest[0].GetProperty("version").GetInt32() })).StatusCode);
            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"{path}/attendance-missing?from=2026-10-01&to=2026-10-31")).GetArrayLength());
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 3 })).StatusCode);
            var history = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions/{id}/history");
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, history.EnumerateArray().Select(x => x.GetProperty("sessionVersion").GetInt32()));
            Assert.Equal(new[] { "absent", "present", "unmarked" }, history.EnumerateArray().Where(x => x.GetProperty("kind").GetString() == "attendance.changed").Select(x => x.GetProperty("attendanceStatus").GetString()));
        }
    }

    [Fact]
    public async Task Invalid_schedules_stale_schedule_versions_and_foreign_references_are_rejected()
    {
        var (client, classId, studentId) = await Setup();
        var (other, otherClassId, otherStudentId) = await Setup();
        using (client) using (other)
        {
            var path = $"/api/classes/{classId}";
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"{path}/schedule", new { version = 0, effectiveFrom = "2026-10-01", slots = new[] { new { dayOfWeek = 1, startTime = "18:00:00", endTime = "19:00:00" }, new { dayOfWeek = 1, startTime = "18:30:00", endTime = "20:00:00" } } })).StatusCode);
            Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>($"{path}/schedule")).GetProperty("version").GetInt32());
            var schedule = new { version = 0, effectiveFrom = "2026-10-01", slots = new[] { new { dayOfWeek = 1, startTime = "18:00:00", endTime = "19:00:00" } } };
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/schedule", schedule)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{path}/schedule", schedule)).StatusCode);
            var session = await (await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = session.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/schedule")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, $"{path}/schedule", schedule)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/sessions?from=2026-10-01&to=2026-10-31")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/sessions/{id}/attendance")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/sessions/{id}/history")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/attendance-missing?from=2026-10-01&to=2026-10-31")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, $"{path}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, $"/api/classes/{otherClassId}/sessions", new { date = "2026-10-06", startTime = "18:00:00", endTime = "19:00:00", kind = "makeup", replacesSessionId = id })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(client, $"{path}/sessions/{id}/attendance/{otherStudentId}", new { status = "present", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", excused = true, version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 0 })).StatusCode);
        }
    }

    [Fact]
    public async Task Expired_service_can_read_sessions_but_cannot_change_schedule_session_or_attendance()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            var email = me.GetProperty("email").GetString()!;
            var path = $"/api/classes/{classId}";
            var session = await (await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = session.GetProperty("id").GetString();
            app.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = app.Client();
            await TestApplication.Login(expired, email);
            foreach (var route in new[] { "/schedule", "/sessions?from=2026-10-01&to=2026-10-31", $"/sessions/{id}/attendance", $"/sessions/{id}/history", "/attendance-missing?from=2026-10-01&to=2026-10-31" })
                Assert.Equal(HttpStatusCode.OK, (await expired.GetAsync(path + route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(expired, $"{path}/schedule", new { version = 0, effectiveFrom = "2026-10-01", slots = Array.Empty<object>() })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, $"{path}/sessions/generate", new { from = "2026-10-01", to = "2026-10-31", scheduleVersion = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, $"{path}/sessions", new { date = "2026-10-06", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(expired, $"{path}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(expired, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 0 })).StatusCode);
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var account = (await users.FindByEmailAsync(email))!;
            account.IsBlocked = true;
            Assert.True((await users.UpdateAsync(account)).Succeeded);
            Assert.Equal(HttpStatusCode.Unauthorized, (await expired.GetAsync($"{path}/sessions/{id}/attendance")).StatusCode);
        }
    }

    [Fact]
    public async Task Persisted_attendance_change_reaches_public_handler_after_a_failed_delivery()
    {
        var handler = new RetryingHandler();
        await using var deliveryApp = new TestApplication { ClassroomChangeHandler = handler };
        await deliveryApp.InitializeAsync();
        var (client, classId, studentId) = await Setup(deliveryApp);
        using (client)
        {
            var path = $"/api/classes/{classId}";
            var session = await (await TestApplication.Post(client, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = session.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/sessions/{id}", new { status = "cancelled", reason = "Hủy sau đối chiếu", version = 3 })).StatusCode);
            var delivered = await handler.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(12));
            await handler.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(12));
            Assert.Equal(new[] { 1, 2, 3, 4 }, handler.Versions.ToArray());
            Assert.Equal(studentId, delivered.StudentId.ToString());
            Assert.Equal("present", delivered.AttendanceStatus);
            Assert.Equal(3, delivered.SessionVersion);
            var history = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions/{id}/history");
            Assert.Equal(delivered.EventId, history[2].GetProperty("eventId").GetGuid());
            Assert.Equal("present", (await client.GetFromJsonAsync<JsonElement>($"{path}/sessions/{id}/attendance"))[0].GetProperty("status").GetString());
        }
    }
    private sealed class RetryingHandler : IClassroomChangeHandler
    {
        private int failuresRemaining = 2;
        public TaskCompletionSource<ClassroomChange> Delivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ClassroomChange> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public System.Collections.Concurrent.ConcurrentQueue<int> Versions { get; } = new();
        public Task HandleAsync(ClassroomChange change, CancellationToken cancellationToken)
        {
            if (change.Kind == "attendance.changed")
            {
                if (Interlocked.Decrement(ref failuresRemaining) >= 0) throw new InvalidOperationException("Simulated subscriber outage");
            }
            Versions.Enqueue(change.SessionVersion);
            if (change.Kind == "attendance.changed") Delivered.TrySetResult(change);
            if (change.SessionStatus == "cancelled") Cancelled.TrySetResult(change);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Failed_session_with_large_backlog_does_not_starve_another_session()
    {
        var handler = new IsolatedFailureHandler();
        await using var deliveryApp = new TestApplication { ClassroomChangeHandler = handler };
        await deliveryApp.InitializeAsync();
        var (failedClient, failedClassId, studentId) = await Setup(deliveryApp);
        var (healthyClient, healthyClassId, _) = await Setup(deliveryApp);
        using (failedClient) using (healthyClient)
        {
            handler.FailedClassId = failedClassId;
            handler.HealthyClassId = healthyClassId;
            var path = $"/api/classes/{failedClassId}";
            var session = await (await TestApplication.Post(failedClient, $"{path}/sessions", new { date = "2026-10-05", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = session.GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.OK, (await Put(failedClient, $"{path}/sessions/{id}", new { status = "held", version = 1 })).StatusCode);
            for (var version = 0; version < 101; version++)
                Assert.Equal(HttpStatusCode.OK, (await Put(failedClient, $"{path}/sessions/{id}/attendance/{studentId}", new { status = "present", version })).StatusCode);
            Assert.Equal(103, (await failedClient.GetFromJsonAsync<JsonElement>($"{path}/sessions/{id}/history")).GetArrayLength());
            // Ensure the healthy event is later than the failed backlog, independent of version sorting.
            deliveryApp.Clock.Advance(TimeSpan.FromMinutes(1));
            var healthy = await TestApplication.Post(healthyClient, $"/api/classes/{healthyClassId}/sessions", new { date = "2026-10-06", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" });
            Assert.Equal(HttpStatusCode.Created, healthy.StatusCode);
            var delivered = await handler.Healthy.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(healthyClassId, delivered.ClassId.ToString());
        }
    }
    private sealed class IsolatedFailureHandler : IClassroomChangeHandler
    {
        public bool FailAllChanges { get; init; }
        public string? FailedClassId { get; set; }
        public string? HealthyClassId { get; set; }
        public TaskCompletionSource<ClassroomChange> Healthy { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task HandleAsync(ClassroomChange change, CancellationToken cancellationToken)
        {
            if (change.ClassId.ToString() == FailedClassId && (FailAllChanges || change.Kind == "attendance.changed")) throw new InvalidOperationException("This session's subscriber remains unavailable");
            if (change.ClassId.ToString() == HealthyClassId) Healthy.TrySetResult(change);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task A_full_batch_of_failed_sessions_does_not_starve_a_later_healthy_session()
    {
        var handler = new IsolatedFailureHandler { FailAllChanges = true };
        await using var deliveryApp = new TestApplication { ClassroomChangeHandler = handler };
        await deliveryApp.InitializeAsync();
        var (failedClient, failedClassId, _) = await Setup(deliveryApp);
        var (healthyClient, healthyClassId, _) = await Setup(deliveryApp);
        using (failedClient) using (healthyClient)
        {
            handler.FailedClassId = failedClassId;
            handler.HealthyClassId = healthyClassId;
            for (var offset = 0; offset < 100; offset++)
                Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(failedClient, $"/api/classes/{failedClassId}/sessions", new { date = new DateOnly(2026, 10, 1).AddDays(offset).ToString("yyyy-MM-dd"), startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).StatusCode);
            deliveryApp.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(healthyClient, $"/api/classes/{healthyClassId}/sessions", new { date = "2026-10-06", startTime = "18:00:00", endTime = "19:00:00", kind = "regular" })).StatusCode);
            var delivered = await handler.Healthy.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(healthyClassId, delivered.ClassId.ToString());
        }
    }

    [Fact]
    public async Task Weekly_schedule_generates_multiple_days_and_keeps_existing_sessions_when_changed()
    {
        var (client, classId, studentId) = await Setup();
        using (client)
        {
            var path = $"/api/classes/{classId}";
            var saved = await Put(client, $"{path}/schedule", new { version = 0, effectiveFrom = "2026-10-01", slots = new[] { new { dayOfWeek = 1, startTime = "18:00:00", endTime = "19:30:00" }, new { dayOfWeek = 3, startTime = "19:00:00", endTime = "20:00:00" } } });
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var generate = await TestApplication.Post(client, $"{path}/sessions/generate", new { from = "2026-10-05", to = "2026-10-11", scheduleVersion = 1 });
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
            var sessions = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions?from=2026-10-01&to=2026-10-31");
            Assert.Equal(2, sessions.GetArrayLength());
            Assert.Equal("2026-10-05", sessions[0].GetProperty("date").GetString());
            Assert.Equal(DateTimeOffset.Parse("2026-10-05T11:00:00Z"), sessions[0].GetProperty("startsAtUtc").GetDateTimeOffset());
            Assert.Equal("planned", sessions[0].GetProperty("status").GetString());
            var attendance = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions/{sessions[0].GetProperty("id").GetString()}/attendance");
            Assert.Equal(studentId, attendance[0].GetProperty("studentId").GetString());
            Assert.Equal("unmarked", attendance[0].GetProperty("status").GetString());
            Assert.Equal(0, attendance[0].GetProperty("version").GetInt32());
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{path}/schedule", new { version = 1, effectiveFrom = "2026-10-12", slots = new[] { new { dayOfWeek = 2, startTime = "17:00:00", endTime = "18:00:00" } } })).StatusCode);
            var unchanged = await client.GetFromJsonAsync<JsonElement>($"{path}/sessions?from=2026-10-01&to=2026-10-31");
            Assert.Equal(sessions.ToString(), unchanged.ToString());
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"{path}/sessions/generate", new { from = "2026-10-05", to = "2026-10-11", scheduleVersion = 2 })).StatusCode);
            Assert.Equal(2, (await client.GetFromJsonAsync<JsonElement>($"{path}/sessions?from=2026-10-01&to=2026-10-31")).GetArrayLength());
        }
    }
}
