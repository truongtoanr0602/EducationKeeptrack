using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed partial class TuitionTests
{
    private static async Task<JsonElement> Cashflow(HttpClient client, string classId, string from = "2026-11-01", string to = "2026-11-30")
    {
        var response = await client.GetAsync($"/api/billing/classes/{classId}/cashflow?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> PeriodStatistics(HttpClient client, string classId, string month = "2026-10")
    {
        var response = await client.GetAsync($"/api/billing/classes/{classId}/period-balances?month={month}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Statistics_remain_readable_after_service_expiry_and_require_authentication()
    {
        await using var isolated = new TestApplication(); await isolated.InitializeAsync();
        var (client, classId, studentId, email) = await Setup(isolated);
        using (client)
        {
            await Receive(client, classId, studentId, "100000");
            isolated.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = isolated.Client(); await TestApplication.Login(expired, email);
            Assert.Equal("100000", (await Cashflow(expired, classId)).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("0", (await PeriodStatistics(expired, classId)).GetProperty("totalDebtVnd").GetString());
            using var anonymous = isolated.Client();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/billing/classes/{classId}/cashflow?from=2026-11-01&to=2026-11-30")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/billing/classes/{classId}/period-balances?month=2026-10")).StatusCode);
        }
    }

    [Fact]
    public async Task Statistics_expose_missing_rate_and_pending_projection_instead_of_claiming_complete_debt()
    {
        var outage = new TemporaryOutage();
        await using var isolated = new TestApplication { DecorateBillingHandler = handler => new GatedSubscription(outage, handler) }; await isolated.InitializeAsync();
        var (client, classId, studentId, _) = await Setup(isolated);
        using (client)
        {
            await Session(client, classId, studentId, "2026-10-02"); await Payments(client, classId);
            Assert.Equal("missing-rate", (await Cashflow(client, classId)).GetProperty("status").GetString());
            Assert.Equal("missing-rate", (await PeriodStatistics(client, classId)).GetProperty("status").GetString());
            outage.Fail = true;
            await Session(client, classId, studentId, "2026-10-03");
            Assert.Equal("pending", (await Cashflow(client, classId)).GetProperty("status").GetString());
            Assert.Equal("pending", (await PeriodStatistics(client, classId)).GetProperty("status").GetString());
            outage.Fail = false;
        }
    }

    [Fact]
    public async Task Statistics_use_vietnam_actual_calendar_dates_at_utc_month_boundary_and_include_both_endpoints()
    {
        await using var isolated = new TestApplication(); await isolated.InitializeAsync();
        isolated.Clock.Advance(DateTimeOffset.Parse("2026-10-31T17:05:00Z") - isolated.Clock.GetUtcNow());
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            await Receive(client, classId, studentId, "100000", "2026-10-31");
            var firstNovember = await Receive(client, classId, studentId, "200000", "2026-11-01");
            await Receive(client, classId, studentId, "50000", "2026-11-30");
            await Receive(client, classId, studentId, "10000", "2026-12-01");
            Assert.Equal(10, firstNovember.GetProperty("recordedAt").GetDateTimeOffset().UtcDateTime.Month);
            Assert.Equal("250000", (await Cashflow(client, classId)).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("100000", (await Cashflow(client, classId, "2026-10-31", "2026-10-31")).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("50000", (await Cashflow(client, classId, "2026-11-30", "2026-11-30")).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("200000", (await Cashflow(client, classId, "2026-11-01", "2026-11-01")).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("140000", (await PeriodStatistics(client, classId)).GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Statistics_move_replacement_to_correct_actual_month_and_exclude_cancelled_originals_without_mutating_history()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var receipt = await Receive(client, classId, studentId, "500000");
            var before = await Cashflow(client, classId);
            var command = new { action = "replace", amountVnd = "200000", receivedOn = "2026-10-31", reason = "Wrong date/amount", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() };
            var result = await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{receipt.GetProperty("id").GetGuid()}/corrections", command);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var replacement = (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("replacementId").GetGuid();
            var november = await Cashflow(client, classId);
            Assert.Equal("0", november.GetProperty("totalReceivedVnd").GetString());
            var october = await Cashflow(client, classId, "2026-10-01", "2026-10-31");
            Assert.Equal("200000", october.GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("300000", (await PeriodStatistics(client, classId)).GetProperty("totalDebtVnd").GetString());
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments/{replacement}/corrections", new { action = "cancel", reason = "Duplicate", idempotencyKey = Guid.NewGuid(), version = october.GetProperty("version").GetInt64() })).StatusCode);
            Assert.Equal("0", (await Cashflow(client, classId, "2026-10-01", "2026-11-30")).GetProperty("totalReceivedVnd").GetString());
            var after = await PeriodStatistics(client, classId);
            Assert.Equal("500000", after.GetProperty("totalDebtVnd").GetString());
            Assert.Empty(after.GetProperty("students")[0].GetProperty("allocations").EnumerateArray());
            Assert.Equal(command.version + 2, after.GetProperty("version").GetInt64());
            Assert.Equal(2, (await Payments(client, classId)).GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
        }
    }

    [Fact]
    public async Task Statistics_isolate_teachers_classes_and_validate_date_and_obligation_filters()
    {
        var (client, classId, studentId) = await PaymentSetup();
        var (other, otherClass, otherStudent) = await PaymentSetup();
        using (client) using (other)
        {
            await Receive(client, classId, studentId, "300000");
            await Receive(other, otherClass, otherStudent, "100000");
            Assert.Equal("300000", (await Cashflow(client, classId)).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("100000", (await Cashflow(other, otherClass)).GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("200000", (await PeriodStatistics(client, classId)).GetProperty("totalDebtVnd").GetString());
            Assert.Equal("400000", (await PeriodStatistics(other, otherClass)).GetProperty("totalDebtVnd").GetString());
            var path = $"/api/billing/classes/{classId}";
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/cashflow?from=2026-11-01&to=2026-11-30")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/period-balances?month=2026-10")).StatusCode);
            foreach (var query in new[] { "from=2026-11-30&to=2026-11-01", "from=2026-02-30&to=2026-11-30", "from=2026-11-01T00:00:00Z&to=2026-11-30", "from=2026-11-01", "from=&to=2026-11-30" })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/cashflow?" + query)).StatusCode);
            foreach (var month in new[] { "2026-13", "2026-1", "2026-10-01", "" })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/period-balances?month=" + month)).StatusCode);
            Assert.Equal("0", (await PeriodStatistics(client, classId, "2026-12")).GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task Statistics_keep_exact_large_receipts_separate_from_allocations_and_unallocated_credit()
    {
        var (client, classId, studentId, _) = await Setup();
        using (client)
        {
            await Receive(client, classId, studentId, "999999999999999999");
            await Receive(client, classId, studentId, "999999999999999999");
            var report = await Cashflow(client, classId);
            Assert.Equal("1999999999999999998", report.GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("1999999999999999998", report.GetProperty("totalUnallocatedVnd").GetString());
            Assert.Equal("0", (await PeriodStatistics(client, classId)).GetProperty("totalPaidVnd").GetString());
        }
    }

    [Fact]
    public async Task Statistics_distinguish_held_allocations_from_paid_debt_without_adding_fee_adjustments_to_cashflow()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            await Receive(client, classId, studentId, "500000");
            var version = (await Cashflow(client, classId)).GetProperty("version").GetInt64();
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/adjustments", new { studentId, month = "2026-10", direction = "decrease", amountVnd = "400000", reason = "Support", idempotencyKey = Guid.NewGuid(), version })).StatusCode);
            var report = await PeriodStatistics(client, classId);
            Assert.Equal("needs-review", report.GetProperty("status").GetString());
            Assert.Equal("100000", report.GetProperty("totalFeesVnd").GetString());
            Assert.Equal("100000", report.GetProperty("totalPaidVnd").GetString());
            Assert.Equal("400000", report.GetProperty("totalHeldVnd").GetString());
            Assert.Equal("0", report.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("500000", report.GetProperty("students")[0].GetProperty("allocatedVnd").GetString());
            Assert.Equal("500000", (await Cashflow(client, classId)).GetProperty("totalReceivedVnd").GetString());
        }
    }
    [Fact]
    public async Task Period_statistics_filter_obligation_month_and_reconcile_receipts_from_other_months()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var receipt = await Receive(client, classId, studentId, "700000");
            var response = await client.GetAsync($"/api/billing/classes/{classId}/period-balances?month=2026-10");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var october = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("500000", october.GetProperty("totalFeesVnd").GetString());
            Assert.Equal("500000", october.GetProperty("totalPaidVnd").GetString());
            Assert.Equal("0", october.GetProperty("totalDebtVnd").GetString());
            var row = Assert.Single(october.GetProperty("students").EnumerateArray());
            var allocation = Assert.Single(row.GetProperty("allocations").EnumerateArray());
            Assert.Equal(receipt.GetProperty("id").GetGuid(), allocation.GetProperty("receiptId").GetGuid());
            Assert.Equal("2026-11-05", allocation.GetProperty("receivedOn").GetString());
            Assert.Equal("500000", allocation.GetProperty("amountVnd").GetString());
            var november = await client.GetFromJsonAsync<JsonElement>($"/api/billing/classes/{classId}/period-balances?month=2026-11");
            Assert.Equal("200000", november.GetProperty("totalPaidVnd").GetString());
            Assert.Equal("300000", november.GetProperty("totalDebtVnd").GetString());
            Assert.Equal(october.GetProperty("version").GetInt64(), november.GetProperty("version").GetInt64());
        }
    }
    [Fact]
    public async Task Cashflow_counts_actual_november_receipt_once_even_when_it_pays_october_debt()
    {
        var (client, classId, studentId) = await PaymentSetup();
        using (client)
        {
            var receipt = await Receive(client, classId, studentId, "500000");
            var response = await client.GetAsync($"/api/billing/classes/{classId}/cashflow?from=2026-11-01&to=2026-11-30");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("500000", report.GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("Asia/Ho_Chi_Minh", report.GetProperty("timeZone").GetString());
            var row = Assert.Single(report.GetProperty("receipts").EnumerateArray());
            Assert.Equal(receipt.GetProperty("id").GetGuid(), row.GetProperty("receipt").GetProperty("id").GetGuid());
            Assert.Equal("2026-10", row.GetProperty("receipt").GetProperty("allocations")[0].GetProperty("month").GetString());
            var october = await client.GetFromJsonAsync<JsonElement>($"/api/billing/classes/{classId}/cashflow?from=2026-10-01&to=2026-10-31");
            Assert.Equal("0", october.GetProperty("totalReceivedVnd").GetString());
            Assert.Equal("0", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
        }
    }
}
