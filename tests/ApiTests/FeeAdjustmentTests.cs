using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed partial class TuitionTests
{
    [Fact]
    public async Task Adjustment_waits_for_attendance_projection_and_expired_service_preserves_history_only()
    {
        var outage = new TemporaryOutage();
        await using var isolated = new TestApplication { DecorateBillingHandler = handler => new GatedSubscription(outage, handler) };
        await isolated.InitializeAsync();
        var (client, classId, studentId, email) = await Setup(isolated);
        using (client)
        {
            await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "500000", version = 0 });
            var sessionId = await Session(client, classId, studentId, "2026-10-02");
            var before = await Ready(client, classId);
            outage.Fail = true;
            await Put(client, $"/api/classes/{classId}/sessions/{sessionId}/attendance/{studentId}", new { status = "absent", version = 1 });
            var input = new { studentId, month = "2026-10", direction = "increase", amountVnd = "100000", reason = "Bổ sung", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() };
            var path = $"/api/billing/classes/{classId}/adjustments";
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, input)).StatusCode);
            outage.Fail = false;
            var ready = await Ready(client, classId);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, input with { version = ready.GetProperty("version").GetInt64() })).StatusCode);
            isolated.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = isolated.Client(); await TestApplication.Login(expired, email);
            Assert.Equal("100000", (await Ready(expired, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, path, input)).StatusCode);
        }
    }

    [Fact]
    public async Task Adjustment_failure_rolls_back_fees_holds_and_version_then_same_request_can_retry()
    {
        var failure = new FailAdjustmentSave();
        await using var isolated = new TestApplication { BillingInterceptor = failure };
        await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            await TestApplication.Post(client, path + "/payments", new {
                studentId, amountVnd = "500000", receivedOn = "2026-10-03", idempotencyKey = Guid.NewGuid(), version
            });
            var input = new { studentId, month = "2026-10", direction = "decrease", amountVnd = "200000", reason = "Hỗ trợ", idempotencyKey = Guid.NewGuid(), version = version + 1 };
            failure.Armed = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => TestApplication.Post(client, path + "/adjustments", input));
            var unchanged = await Payments(client, classId);
            Assert.Equal(version + 1, unchanged.GetProperty("version").GetInt64());
            Assert.Equal("0", unchanged.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            Assert.Equal("500000", (await Ready(client, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/adjustments", input)).StatusCode);
            Assert.Equal("200000", (await Payments(client, classId)).GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
        }
    }

    private sealed class FailAdjustmentSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public bool Armed;
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<EducationKeeptrack.Billing.Domain.FeeAdjustment>().Any())
            {
                Armed = false;
                throw new InvalidOperationException("Stop after adjustment save, before transaction commit");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Adjustment_event_and_history_survive_restart_and_retry_keeps_original_version()
    {
        var failing = new AdjustmentDeliveryProbe(true);
        await using var original = new TestApplication { TuitionChangeHandler = failing };
        await original.InitializeAsync();
        var (client, classId, studentId, email) = await Setup(original);
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            var input = new { studentId, month = "2026-10", direction = "increase", amountVnd = "999999999999999999", reason = "Thỏa thuận học phí", idempotencyKey = Guid.NewGuid(), version = 0 };
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/adjustments", input)).StatusCode);
            var delivered = await failing.Delivery.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(1, delivered.Version);
            await original.StopHostAsync();
            var replay = new AdjustmentDeliveryProbe(false);
            await using var restarted = original.Reopen(tuitionHandler: replay);
            await restarted.InitializeAsync();
            using var reload = restarted.Client(); await TestApplication.Login(reload, email);
            var redelivered = await replay.Delivery.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(delivered, redelivered);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(reload, path + "/adjustments", input)).StatusCode);
            Assert.Equal("999999999999999999", (await Payments(reload, classId)).GetProperty("totalDebtVnd").GetString());
            var fees = await Ready(reload, classId);
            Assert.Equal(1, fees.GetProperty("version").GetInt64());
            Assert.Equal("999999999999999999", fees.GetProperty("totalVnd").GetString());
            Assert.Equal(1, fees.GetProperty("students")[0].GetProperty("adjustments").GetArrayLength());
        }
    }

    private sealed class AdjustmentDeliveryProbe(bool fail) : EducationKeeptrack.Contracts.ITuitionChangeHandler
    {
        public TaskCompletionSource<EducationKeeptrack.Contracts.TuitionChange> Delivery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task HandleAsync(EducationKeeptrack.Contracts.TuitionChange change, CancellationToken ct)
        {
            if (change.Kind != "adjustment.recorded") return Task.CompletedTask;
            Delivery.TrySetResult(change);
            return fail ? Task.FromException(new InvalidOperationException("Stop before adjustment acknowledgment")) : Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Adjustment_retry_is_idempotent_and_competing_versions_cannot_both_commit()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/adjustments";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var key = Guid.NewGuid();
            var input = new { studentId, month = "2026-10", direction = "increase", amountVnd = "100000", reason = "Bổ sung", idempotencyKey = key, version };
            var retries = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => TestApplication.Post(client, path, input)));
            foreach (var response in retries) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal((await retries[0].Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
                (await retries[1].Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new {
                studentId, month = "2026-10", direction = "increase", amountVnd = "200000", reason = "Bổ sung", idempotencyKey = key, version
            })).StatusCode);
            var competitors = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => TestApplication.Post(client, path, new {
                studentId, month = "2026-10", direction = "increase", amountVnd = "100000", reason = "Bổ sung", idempotencyKey = Guid.NewGuid(), version = version + 1
            })));
            Assert.Single(competitors, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(competitors, x => x.StatusCode == HttpStatusCode.Conflict);
            var after = await Payments(client, classId);
            Assert.Equal(version + 2, after.GetProperty("version").GetInt64());
            Assert.Equal("700000", after.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(2, (await Ready(client, classId)).GetProperty("students")[0].GetProperty("adjustments").GetArrayLength());
        }
    }

    [Fact]
    public async Task Invalid_adjustments_foreign_students_and_stale_versions_leave_fees_unchanged()
    {
        var (client, classId, studentId) = await PaymentSetup();
        var (other, _, foreignStudent, _) = await Setup();
        using (client) using (other)
        {
            var path = $"/api/billing/classes/{classId}/adjustments";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var valid = new { studentId, month = "2026-10", direction = "decrease", amountVnd = "100000", reason = "Hỗ trợ", idempotencyKey = Guid.NewGuid(), version = (long?)version };
            foreach (var amount in new[] { "0", "-1", "0.5", "1000000000000000000", "500001" })
                Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, valid with { amountVnd = amount })).StatusCode);
            foreach (var reason in new[] { "", "   ", new string('x', 1001) })
                Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, valid with { reason = reason })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, valid with { version = null })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, valid with { month = "2026-13" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, valid with { direction = "other" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, valid with { idempotencyKey = Guid.Empty })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, valid with { version = version - 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(client, path, valid with { studentId = foreignStudent })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, path, valid)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, valid)).StatusCode);
            var after = await Payments(client, classId);
            Assert.Equal(version, after.GetProperty("version").GetInt64());
            Assert.Equal("500000", after.GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Increase_after_full_payment_creates_new_debt_and_new_receipt_pays_it()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/payments", new {
                studentId, amountVnd = "500000", receivedOn = "2026-10-03", idempotencyKey = Guid.NewGuid(), version
            })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/adjustments", new {
                studentId, month = "2026-10", direction = "increase", amountVnd = "100000", reason = "Thỏa thuận bổ sung", idempotencyKey = Guid.NewGuid(), version = version + 1
            })).StatusCode);
            var report = await Payments(client, classId);
            Assert.Equal("100000", report.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("ready", report.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/payments", new {
                studentId, amountVnd = "100000", receivedOn = "2026-10-04", idempotencyKey = Guid.NewGuid(), version = version + 2
            })).StatusCode);
            Assert.Equal("0", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Adjustment_reduces_paid_fees_and_durably_holds_excess_without_reusing_it_on_increase()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            var before = await Payments(client, classId);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/payments", new {
                studentId, amountVnd = "500000", receivedOn = "2026-10-03", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64()
            })).StatusCode);
            var paid = await Payments(client, classId);
            var input = new { studentId, month = "2026-10", direction = "decrease", amountVnd = "200000", reason = "Hỗ trợ học sinh", idempotencyKey = Guid.NewGuid(), version = paid.GetProperty("version").GetInt64() };
            var response = await TestApplication.Post(client, path + "/adjustments", input);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var saved = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrEmpty(saved.GetProperty("recordedBy").GetString()));
            Assert.NotEqual(default, saved.GetProperty("recordedAt").GetDateTimeOffset());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/adjustments", input)).StatusCode);
            var reduced = await Payments(client, classId);
            Assert.Equal(paid.GetProperty("version").GetInt64() + 1, reduced.GetProperty("version").GetInt64());
            Assert.Equal("0", reduced.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", reduced.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            Assert.Equal("300000", (await Ready(client, classId)).GetProperty("totalVnd").GetString());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/adjustments", new {
                studentId, month = "2026-10", direction = "increase", amountVnd = "100000", reason = "Bổ sung học phí", idempotencyKey = Guid.NewGuid(), version = reduced.GetProperty("version").GetInt64()
            })).StatusCode);
            var increased = await Payments(client, classId);
            Assert.Equal("100000", increased.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", increased.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            var fees = await Ready(client, classId);
            Assert.Equal("400000", fees.GetProperty("totalVnd").GetString());
            Assert.Equal(2, fees.GetProperty("students")[0].GetProperty("adjustments").GetArrayLength());
        }
    }
}
