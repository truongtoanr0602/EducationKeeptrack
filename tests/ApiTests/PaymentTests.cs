using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed partial class TuitionTests
{
    [Fact]
    public async Task Refresh_includes_new_charges_before_deciding_whether_existing_allocations_exceed_fees()
    {
        var outage = new TemporaryOutage();
        await using var isolated = new TestApplication { DecorateBillingHandler = handler => new GatedSubscription(outage, handler) };
        await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var before = await Payments(client, classId);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", new { studentId, amountVnd = "500000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() })).StatusCode);
            var sessions = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{classId}/sessions?from=2026-10-01&to=2026-10-31");
            outage.Fail = true;
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{sessions[0].GetProperty("id").GetString()}/attendance/{studentId}", new { status = "absent", version = 1 })).StatusCode);
            await Session(client, classId, studentId, "2026-10-03");
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-11-01", unitPriceVnd = "900000", version = 1 })).StatusCode);
            var after = await Payments(client, classId);
            Assert.Equal("ready", after.GetProperty("status").GetString());
            Assert.Equal("0", after.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            Assert.Equal("0", after.GetProperty("totalDebtVnd").GetString());
        }
    }
    [Fact]
    public async Task Fee_reduction_holds_excess_for_review_and_restoring_fees_does_not_reuse_it()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/payments";
            var before = await Payments(client, classId);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, new { studentId, amountVnd = "500000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() })).StatusCode);
            var sessions = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{classId}/sessions?from=2026-10-01&to=2026-10-31");
            var sessionId = sessions[0].GetProperty("id").GetString();
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{sessionId}/attendance/{studentId}", new { status = "absent", version = 1 })).StatusCode);
            var reduced = await Payments(client, classId);
            Assert.Equal("needs-review", reduced.GetProperty("status").GetString());
            Assert.Equal("500000", reduced.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            Assert.Equal("0", reduced.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("500000", reduced.GetProperty("students")[0].GetProperty("receipts")[0].GetProperty("allocations")[0].GetProperty("amountVnd").GetString());
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{sessionId}/attendance/{studentId}", new { status = "present", version = 2 })).StatusCode);
            var restored = await Payments(client, classId);
            Assert.Equal("needs-review", restored.GetProperty("status").GetString());
            Assert.Equal("500000", restored.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("500000", restored.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new { studentId, amountVnd = "1", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = restored.GetProperty("version").GetInt64() })).StatusCode);
        }
    }
    [Fact]
    public async Task Failure_after_database_save_rolls_back_receipt_allocations_and_version_then_same_key_can_retry()
    {
        var failure = new FailReceiptSave();
        await using var isolated = new TestApplication { BillingInterceptor = failure };
        await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/payments";
            var before = await Payments(client, classId);
            var input = new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() };
            failure.Armed = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => TestApplication.Post(client, path, input));
            var rolledBack = await Payments(client, classId);
            Assert.Equal(before.GetProperty("version").GetInt64(), rolledBack.GetProperty("version").GetInt64());
            Assert.Equal("500000", rolledBack.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(0, rolledBack.GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, input)).StatusCode);
            Assert.Equal("200000", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
        }
    }
    private sealed class FailReceiptSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public bool Armed;
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<EducationKeeptrack.Billing.Domain.Receipt>().Any())
            {
                Armed = false;
                throw new InvalidOperationException("Injected database failure after receipt/allocation writes, before commit");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task Missing_rates_prevent_receipt_allocation_until_fees_are_ready()
    {
        var (client, classId, studentId, _) = await Setup();
        using (client)
        {
            await Session(client, classId, studentId, "2026-10-02"); await Ready(client, classId);
            var before = await Payments(client, classId);
            Assert.Equal("missing-rate", before.GetProperty("status").GetString());
            var input = new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() };
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", input)).StatusCode);
            Assert.Equal(0, (await Payments(client, classId)).GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
        }
    }

    [Fact]
    public async Task Expired_service_keeps_receipt_history_readable_but_rejects_new_receipts()
    {
        await using var isolated = new TestApplication(); await isolated.InitializeAsync();
        var (client, classId, studentId, email) = await Setup(isolated);
        using (client)
        {
            var input = new { studentId, amountVnd = "999999999999999999", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = 0 };
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", input)).StatusCode);
            isolated.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = isolated.Client(); await TestApplication.Login(expired, email);
            var student = (await Payments(expired, classId)).GetProperty("students")[0];
            Assert.Equal("999999999999999999", student.GetProperty("unallocatedVnd").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, $"/api/billing/classes/{classId}/payments", input)).StatusCode);
        }
    }
    private static async Task<JsonElement> Payments(HttpClient client, string classId, string month = "2026-11")
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var response = await client.GetAsync($"/api/billing/classes/{classId}/payments?month={month}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (report.GetProperty("status").GetString() != "pending") return report;
            Assert.True(DateTime.UtcNow < deadline, "Payments projection did not catch up");
            await Task.Delay(100);
        }
    }

    private async Task<(HttpClient Client, string ClassId, string StudentId)> PaymentSetup(TestApplication? application = null)
    {
        var (client, classId, studentId, _) = await Setup(application);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "500000", version = 0 })).StatusCode);
        await Session(client, classId, studentId, "2026-10-02"); await Ready(client, classId);
        return (client, classId, studentId);
    }

    [Fact]
    public async Task Duplicate_receipts_return_original_and_changed_payload_is_rejected()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/payments";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var idempotencyKey = Guid.NewGuid();
            var input = new { studentId, amountVnd = "700000", receivedOn = "2026-11-05", note = "Thu dư", idempotencyKey, version };
            var first = await TestApplication.Post(client, path, input);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            var saved = await first.Content.ReadFromJsonAsync<JsonElement>();
            var duplicate = await TestApplication.Post(client, path, input);
            Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
            Assert.Equal(saved.GetProperty("id").GetGuid(), (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new { studentId, amountVnd = "800000", receivedOn = "2026-11-05", note = "Thu dư", idempotencyKey, version })).StatusCode);
            var report = await Payments(client, classId);
            Assert.Equal(version + 1, report.GetProperty("version").GetInt64());
            var student = report.GetProperty("students")[0];
            Assert.Equal("0", student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", student.GetProperty("unallocatedVnd").GetString());
            Assert.Equal(1, student.GetProperty("receipts").GetArrayLength());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-11-01", unitPriceVnd = "900000", version = 1 })).StatusCode);
            await Session(client, classId, studentId, "2026-11-02");
            await Ready(client, classId, "2026-11");
            var later = (await Payments(client, classId)).GetProperty("students")[0];
            Assert.Equal("900000", later.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", later.GetProperty("unallocatedVnd").GetString());
        }
    }

    [Fact]
    public async Task Concurrent_receipts_conflict_then_retry_uses_remaining_debt_and_keeps_excess_separate()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/payments";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var inputs = new[] { Guid.NewGuid(), Guid.NewGuid() }.Select(idempotencyKey => new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey, version }).ToArray();
            var replies = await Task.WhenAll(inputs.Select(input => TestApplication.Post(client, path, input)));
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            var failedIndex = Array.FindIndex(replies, r => r.StatusCode == HttpStatusCode.Conflict);
            var afterFirst = await Payments(client, classId);
            Assert.Equal("200000", afterFirst.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path, new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", inputs[failedIndex].idempotencyKey, version = afterFirst.GetProperty("version").GetInt64() })).StatusCode);
            var student = (await Payments(client, classId)).GetProperty("students")[0];
            Assert.Equal("0", student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("100000", student.GetProperty("unallocatedVnd").GetString());
            Assert.Equal(2, student.GetProperty("receipts").GetArrayLength());
        }
    }

    [Fact]
    public async Task Concurrent_retries_of_same_receipt_create_one_receipt_and_one_version()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var input = new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version };
            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", input)));
            foreach (var response in responses) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var first = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
            var second = await responses[1].Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
            var after = await Payments(client, classId);
            Assert.Equal(version + 1, after.GetProperty("version").GetInt64());
            Assert.Equal(1, after.GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
            Assert.Equal("200000", after.GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Delayed_attendance_projection_blocks_receipts_until_new_debt_is_known()
    {
        var outage = new TemporaryOutage();
        await using var isolated = new TestApplication { DecorateBillingHandler = handler => new GatedSubscription(outage, handler) };
        await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var sessions = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{classId}/sessions?from=2026-10-01&to=2026-10-31");
            var sessionId = sessions[0].GetProperty("id").GetString();
            var before = await Payments(client, classId);
            outage.Fail = true;
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/classes/{classId}/sessions/{sessionId}/attendance/{studentId}", new { status = "absent", version = 1 })).StatusCode);
            var pending = await client.GetFromJsonAsync<JsonElement>($"/api/billing/classes/{classId}/payments?month=2026-11");
            Assert.Equal("pending", pending.GetProperty("status").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() })).StatusCode);
            outage.Fail = false;
            var after = await Payments(client, classId);
            Assert.Equal("0", after.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(0, after.GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
        }
    }

    [Fact]
    public async Task Invalid_receipts_stale_versions_and_foreign_references_do_not_change_balances()
    {
        var (client, classId, studentId) = await PaymentSetup();
        var (other, otherClass, otherStudent, _) = await Setup();
        using (client) using (other)
        {
            var path = $"/api/billing/classes/{classId}/payments";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            foreach (var amount in new[] { "0", "-1", "1.5", "1000000000000000000" })
                Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { studentId, amountVnd = amount, receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { studentId, amountVnd = "1", receivedOn = "0001-01-01", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { studentId, amountVnd = "1", receivedOn = "2026-11-05", idempotencyKey = Guid.Empty, version })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { studentId, amountVnd = "1", receivedOn = "2026-11-05", note = new string('x', 1001), idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new { studentId, amountVnd = "1", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = version - 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(client, path, new { studentId = otherStudent, amountVnd = "1", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "?month=2026-11")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, path, new { studentId, amountVnd = "1", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { studentId, amountVnd = "1", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?month=2026-99")).StatusCode);
            var after = await Payments(client, classId);
            Assert.Equal(version, after.GetProperty("version").GetInt64());
            Assert.Equal("500000", after.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(0, after.GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
        }
    }

    [Fact]
    public async Task Receipt_pays_oldest_debt_first_and_survives_reload_without_copying_fees()
    {
        var (client, classId, studentId, email) = await Setup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "500000", version = 0 })).StatusCode);
            await Session(client, classId, studentId, "2026-10-02"); await Ready(client, classId);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"{path}/rates", new { effectiveFrom = "2026-11-01", unitPriceVnd = "900000", version = 1 })).StatusCode);
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var before = await Payments(client, classId);
            var response = await TestApplication.Post(client, $"{path}/payments", new { studentId, amountVnd = "700000", receivedOn = "2026-11-05", note = "Lần 1", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("500000", receipt.GetProperty("allocations")[0].GetProperty("amountVnd").GetString());
            Assert.Equal("2026-10", receipt.GetProperty("allocations")[0].GetProperty("month").GetString());
            Assert.Equal("200000", receipt.GetProperty("allocations")[1].GetProperty("amountVnd").GetString());
            using var reload = app.Client(); await TestApplication.Login(reload, email);
            var after = await Payments(reload, classId);
            var student = after.GetProperty("students")[0];
            Assert.Equal("0", student.GetProperty("oldDebtVnd").GetString());
            Assert.Equal("700000", student.GetProperty("currentDebtVnd").GetString());
            Assert.Equal("700000", student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("partial", student.GetProperty("periods")[1].GetProperty("status").GetString());
            Assert.Equal("paid", student.GetProperty("periods")[0].GetProperty("status").GetString());
            Assert.Equal("Lần 1", student.GetProperty("receipts")[0].GetProperty("note").GetString());
            Assert.Equal("900000", (await Ready(reload, classId, "2026-11")).GetProperty("totalVnd").GetString());
        }
    }
}
