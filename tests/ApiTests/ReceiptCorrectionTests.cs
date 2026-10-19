using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed partial class TuitionTests
{
    private static async Task<JsonElement> Receive(HttpClient client, string classId, string studentId, string amount, string date = "2026-11-05")
    {
        var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
        var response = await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", new { studentId, amountVnd = amount, receivedOn = date, idempotencyKey = Guid.NewGuid(), version });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Correction_replays_remaining_receipts_across_periods_and_keeps_original_allocation_history()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}";
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/rates", new { effectiveFrom = "2026-11-01", unitPriceVnd = "900000", version = 1 })).StatusCode);
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var first = await Receive(client, classId, studentId, "700000");
            var second = await Receive(client, classId, studentId, "300000", "2026-11-06");
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var result = await TestApplication.Post(client, $"{path}/payments/{first.GetProperty("id").GetGuid()}/corrections", new { action = "cancel", reason = "Wrong transfer", idempotencyKey = Guid.NewGuid(), version });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var after = await Payments(client, classId);
            var student = after.GetProperty("students")[0];
            Assert.Equal("1100000", student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", student.GetProperty("oldDebtVnd").GetString());
            Assert.Equal("900000", student.GetProperty("currentDebtVnd").GetString());
            var remaining = student.GetProperty("receipts").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == second.GetProperty("id").GetGuid());
            Assert.Equal("2026-10", remaining.GetProperty("allocations")[0].GetProperty("month").GetString());
            Assert.Equal("300000", remaining.GetProperty("allocations")[0].GetProperty("amountVnd").GetString());
            var original = student.GetProperty("receipts").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == first.GetProperty("id").GetGuid());
            Assert.Equal(2, original.GetProperty("allocations").GetArrayLength());
            Assert.Equal("500000", original.GetProperty("allocations")[0].GetProperty("amountVnd").GetString());
            var changes = original.GetProperty("correction").GetProperty("changes");
            var oldSecond = changes.GetProperty("before").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == second.GetProperty("id").GetGuid());
            Assert.Equal("2026-11", oldSecond.GetProperty("allocations")[0].GetProperty("month").GetString());
        }
    }

    [Fact]
    public async Task Replacing_amount_and_actual_date_keeps_original_and_stable_retry_result_even_after_later_cancellation()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            await Receive(client, classId, studentId, "300000");
            var original = (await Payments(client, classId)).GetProperty("students")[0].GetProperty("receipts")[0];
            app.Clock.Advance(TimeSpan.FromMinutes(1));
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var path = $"/api/billing/classes/{classId}/payments/{original.GetProperty("id").GetGuid()}/corrections";
            var command = new { action = "replace", amountVnd = "100000", receivedOn = "2026-10-08", note = "Corrected", reason = "Wrong amount and date", idempotencyKey = Guid.NewGuid(), version };
            var response = await TestApplication.Post(client, path, command);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var after = await Payments(client, classId);
            Assert.Equal("400000", after.GetProperty("totalDebtVnd").GetString());
            var receipts = after.GetProperty("students")[0].GetProperty("receipts");
            var replaced = receipts.EnumerateArray().Single(x => x.GetProperty("status").GetString() == "replaced");
            Assert.Equal("300000", replaced.GetProperty("amountVnd").GetString());
            Assert.Equal(original.GetProperty("recordedAt").GetDateTimeOffset(), replaced.GetProperty("recordedAt").GetDateTimeOffset());
            Assert.True(replaced.GetProperty("correction").GetProperty("recordedAt").GetDateTimeOffset() > replaced.GetProperty("recordedAt").GetDateTimeOffset());
            var replacement = receipts.EnumerateArray().Single(x => x.GetProperty("status").GetString() == "active");
            Assert.Equal("2026-10-08", replacement.GetProperty("receivedOn").GetString());
            Assert.Equal("Corrected", replacement.GetProperty("note").GetString());
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{replacement.GetProperty("id").GetGuid()}/corrections", new { action = "cancel", reason = "Duplicate", idempotencyKey = Guid.NewGuid(), version = after.GetProperty("version").GetInt64() })).StatusCode);
            var retry = await (await TestApplication.Post(client, path, command)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(result.GetRawText(), retry.GetRawText());
            Assert.Equal("500000", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Concurrent_correction_and_new_receipt_share_version_lock_and_repeat_correction_is_applied_once()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var receipt = await Receive(client, classId, studentId, "300000");
            var path = $"/api/billing/classes/{classId}/payments";
            var correctionPath = $"{path}/{receipt.GetProperty("id").GetGuid()}/corrections";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var command = new { action = "cancel", reason = "Duplicate", idempotencyKey = Guid.NewGuid(), version };
            var replies = await Task.WhenAll(TestApplication.Post(client, correctionPath, command), TestApplication.Post(client, path, new { studentId, amountVnd = "100000", receivedOn = "2026-11-06", idempotencyKey = Guid.NewGuid(), version }));
            Assert.Single(replies, x => x.StatusCode == HttpStatusCode.Conflict);
            var after = await Payments(client, classId);
            Assert.Equal(version + 1, after.GetProperty("version").GetInt64());
            Assert.Equal(replies[0].StatusCode == HttpStatusCode.OK ? "500000" : "100000", after.GetProperty("totalDebtVnd").GetString());
            var retry = command with { version = after.GetProperty("version").GetInt64() };
            var retries = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => TestApplication.Post(client, correctionPath, retry)));
            foreach (var response in retries) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var final = await Payments(client, classId);
            Assert.Equal(replies[0].StatusCode == HttpStatusCode.OK ? version + 1 : version + 2, final.GetProperty("version").GetInt64());
        }
    }

    [Fact]
    public async Task Correction_rejects_missing_reason_key_conflicts_foreign_access_csrf_and_stale_versions()
    {
        var (client, classId, studentId) = await PaymentSetup();
        var (other, _, _, _) = await Setup();
        using (client) using (other)
        {
            var receipt = await Receive(client, classId, studentId, "300000");
            var path = $"/api/billing/classes/{classId}/payments/{receipt.GetProperty("id").GetGuid()}/corrections";
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var command = new { action = "cancel", reason = (string?)"Wrong transfer", idempotencyKey = Guid.NewGuid(), version };
            foreach (var reason in new string?[] { null, " ", new('x', 1001) })
                Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, command with { reason = reason })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, command with { action = "delete" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, command with { idempotencyKey = Guid.Empty })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, command with { version = version - 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, path, command)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, command)).StatusCode);
            Assert.Equal("200000", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, path, command)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, command with { reason = "Different" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, command with { idempotencyKey = Guid.NewGuid() })).StatusCode);
        }
    }

    [Fact]
    public async Task Correction_does_not_spend_previously_unallocated_money_on_later_fees()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var original = await Receive(client, classId, studentId, "300000");
            await Receive(client, classId, studentId, "400000", "2026-11-06");
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{original.GetProperty("id").GetGuid()}/corrections", new { action = "cancel", reason = "Duplicate", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var student = (await Payments(client, classId)).GetProperty("students")[0];
            Assert.Equal("800000", student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", student.GetProperty("unallocatedVnd").GetString());
        }
    }

    [Fact]
    public async Task Cancelling_receipt_removes_only_its_held_money_without_spending_other_holds()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var first = await Receive(client, classId, studentId, "300000");
            await Receive(client, classId, studentId, "200000", "2026-11-06");
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/adjustments", new { studentId, month = "2026-10", direction = "decrease", amountVnd = "400000", reason = "Support", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var held = await Payments(client, classId);
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{first.GetProperty("id").GetGuid()}/corrections", new { action = "cancel", reason = "Duplicate", idempotencyKey = Guid.NewGuid(), version = held.GetProperty("version").GetInt64() })).StatusCode);
            var after = await Payments(client, classId);
            Assert.Equal("needs-review", after.GetProperty("status").GetString());
            Assert.Equal("100000", after.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", after.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
        }
    }

    [Fact]
    public async Task Cancelling_receipt_restores_debt_and_preserves_original_and_reason_after_reload()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var path = $"/api/billing/classes/{classId}/payments";
            var before = await Payments(client, classId);
            var receipt = await (await TestApplication.Post(client, path, new { studentId, amountVnd = "300000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() })).Content.ReadFromJsonAsync<JsonElement>();
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var command = new { action = "cancel", reason = "Duplicate transfer", idempotencyKey = Guid.NewGuid(), version };
            var correctionPath = $"{path}/{receipt.GetProperty("id").GetGuid()}/corrections";
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, correctionPath, command)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, correctionPath, command)).StatusCode);
            var after = await Payments(client, classId);
            Assert.Equal(version + 1, after.GetProperty("version").GetInt64());
            Assert.Equal("500000", after.GetProperty("totalDebtVnd").GetString());
            var original = after.GetProperty("students")[0].GetProperty("receipts")[0];
            Assert.Equal("300000", original.GetProperty("amountVnd").GetString());
            Assert.Equal("cancelled", original.GetProperty("status").GetString());
            Assert.Equal("Duplicate transfer", original.GetProperty("correction").GetProperty("reason").GetString());
            Assert.Equal("2026-11-05", original.GetProperty("receivedOn").GetString());
            Assert.Equal("0", after.GetProperty("students")[0].GetProperty("unallocatedVnd").GetString());
        }
    }

    [Fact]
    public async Task Failed_correction_rolls_back_original_replacement_allocations_and_version_then_retries_safely()
    {
        var failure = new FailReceiptSave();
        await using var isolated = new TestApplication { BillingInterceptor = failure }; await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var receipt = await Receive(client, classId, studentId, "300000");
            var before = await Payments(client, classId);
            var path = $"/api/billing/classes/{classId}/payments/{receipt.GetProperty("id").GetGuid()}/corrections";
            var command = new { action = "replace", amountVnd = "100000", receivedOn = "2026-10-08", reason = "Wrong amount", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() };
            failure.Armed = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => TestApplication.Post(client, path, command));
            Assert.Equal(before.GetRawText(), (await Payments(client, classId)).GetRawText());
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, path, command)).StatusCode);
            Assert.Equal("400000", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Correction_invalidates_old_transfer_request_and_emits_versioned_balance_change()
    {
        var changes = new ReceiptChanges();
        await using var isolated = new TestApplication { QrGenerator = new QrStub(), TuitionChangeHandler = changes }; await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var receipt = await Receive(client, classId, studentId, "300000");
            Assert.Equal(HttpStatusCode.OK, (await SaveRecipient(client)).StatusCode);
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            var transferPath = $"/api/billing/classes/{classId}/transfer-request";
            var transfer = new { studentId, month = "2026-11", version, recipientVersion = 1 };
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, transferPath, transfer)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{receipt.GetProperty("id").GetGuid()}/corrections", new { action = "cancel", reason = "Duplicate", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, transferPath, transfer)).StatusCode);
            var updated = await TestApplication.Post(client, transferPath, transfer with { version = version + 1 });
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal("500000", (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("amountVnd").GetString());
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!changes.Events.Any(x => x.ClassId.ToString() == classId && x.Version == version + 1 && x.Kind == "receipt.corrected"))
            {
                Assert.True(DateTime.UtcNow < deadline, "Correction event was not delivered");
                await Task.Delay(100);
            }
        }
    }
    private sealed class ReceiptChanges : EducationKeeptrack.Contracts.ITuitionChangeHandler
    {
        public System.Collections.Concurrent.ConcurrentBag<EducationKeeptrack.Contracts.TuitionChange> Events { get; } = [];
        public Task HandleAsync(EducationKeeptrack.Contracts.TuitionChange change, CancellationToken ct) { Events.Add(change); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Date_only_replacement_preserves_its_own_unallocated_credit_after_later_fees()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var original = await Receive(client, classId, studentId, "700000");
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{original.GetProperty("id").GetGuid()}/corrections", new { action = "replace", amountVnd = "700000", receivedOn = "2026-11-04", reason = "Correct date", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var student = (await Payments(client, classId)).GetProperty("students")[0];
            Assert.Equal("500000", student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("200000", student.GetProperty("unallocatedVnd").GetString());
        }
    }

    [Fact]
    public async Task Date_only_replacement_does_not_release_its_own_held_money()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var original = await Receive(client, classId, studentId, "500000");
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/adjustments", new { studentId, month = "2026-10", direction = "decrease", amountVnd = "400000", reason = "Support", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var held = await Payments(client, classId);
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{original.GetProperty("id").GetGuid()}/corrections", new { action = "replace", amountVnd = "500000", receivedOn = "2026-11-04", reason = "Correct date", idempotencyKey = Guid.NewGuid(), version = held.GetProperty("version").GetInt64() })).StatusCode);
            var after = await Payments(client, classId);
            Assert.Equal("needs-review", after.GetProperty("status").GetString());
            Assert.Equal("400000", after.GetProperty("students")[0].GetProperty("allocationExcessVnd").GetString());
            Assert.Equal("0", after.GetProperty("students")[0].GetProperty("unallocatedVnd").GetString());
        }
    }

    [Theory]
    [InlineData("100000", "1000000", "100000")]
    [InlineData("600000", "600000", "200000")]
    [InlineData("800000", "400000", "200000")]
    public async Task Amount_replacement_keeps_existing_surplus_up_to_the_corrected_amount(string amount, string debt, string credit)
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var original = await Receive(client, classId, studentId, "700000");
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var version = (await Payments(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{original.GetProperty("id").GetGuid()}/corrections", new { action = "replace", amountVnd = amount, receivedOn = "2026-11-05", reason = "Correct amount", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var student = (await Payments(client, classId)).GetProperty("students")[0];
            Assert.Equal(debt, student.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(credit, student.GetProperty("unallocatedVnd").GetString());
        }
    }

    [Fact]
    public async Task Expired_teacher_can_read_correction_history_but_cannot_correct_receipts()
    {
        await using var isolated = new TestApplication(); await isolated.InitializeAsync();
        var (client, classId, studentId, email) = await Setup(isolated);
        using (client)
        {
            var receipt = await Receive(client, classId, studentId, "100000");
            isolated.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = isolated.Client(); await TestApplication.Login(expired, email);
            var report = await Payments(expired, classId);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, $"/api/billing/classes/{classId}/payments/{receipt.GetProperty("id").GetGuid()}/corrections", new { action = "cancel", reason = "Wrong", idempotencyKey = Guid.NewGuid(), version = report.GetProperty("version").GetInt64() })).StatusCode);
            Assert.Equal("active", report.GetProperty("students")[0].GetProperty("receipts")[0].GetProperty("status").GetString());
        }
    }
}
