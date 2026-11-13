using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UglyToad.PdfPig;

namespace ApiTests;

/// <summary>Deducting an approved reserve from a later period, reversing it, cancelling it and showing it on the PDF (#21).</summary>
public sealed class ReserveUseTests(TestApplication app) : IClassFixture<TestApplication>
{
    private sealed record Fixture(HttpClient Client, Guid ClassId, Guid StudentId, Guid RequestId, int RequestVersion);

    private static string Reserve(Guid classId, string suffix = "") => $"/api/billing/classes/{classId}/reserve{suffix}";
    private static string CurrentMonth => DateTimeOffset.UtcNow.AddHours(7).ToString("yyyy-MM");

    /// <summary>
    /// Paid 1.200.000 in advance, October fees 750.000, so 450.000 was unused and is now an approved reserve.
    /// November has <paramref name="novemberSessions"/> sessions of 250.000.
    /// </summary>
    private static async Task<Fixture> Approved(TestApplication host, int novemberSessions = 2)
    {
        var (client, _, classId, studentId, _) = await ReportingFixture.Setup(host);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "250000", version = 0 })).StatusCode);
        foreach (var date in new[] { "2026-10-02", "2026-10-03", "2026-10-05" }) await ReportingFixture.Session(client, classId, studentId, date);
        var payments = await ReportingFixture.Payments(client, classId);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments",
            new { studentId, amountVnd = "1200000", receivedOn = "2026-11-05", note = "Đóng trước", idempotencyKey = Guid.NewGuid(), version = payments.GetProperty("version").GetInt64() })).StatusCode);
        var request = await (await TestApplication.Post(client, Reserve(classId, "/requests"), new
        { studentId, absentFrom = "2026-11-02", absentTo = "2026-11-06", reason = "Con ốm", amountVnd = "450000", idempotencyKey = Guid.NewGuid() })).Content.ReadFromJsonAsync<JsonElement>();
        var decision = await TestApplication.Post(client, Reserve(classId, $"/requests/{request.GetProperty("id").GetGuid()}/decision"),
            new { decision = "approve", reason = "Có giấy khám", idempotencyKey = Guid.NewGuid(), version = request.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        var approved = await decision.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var date in new[] { "2026-11-09", "2026-11-10" }.Take(novemberSessions)) await ReportingFixture.Session(client, classId, studentId, date);
        await ReportingFixture.Payments(client, classId);
        return new(client, classId, studentId, approved.GetProperty("id").GetGuid(), approved.GetProperty("version").GetInt32());
    }

    private static async Task<JsonElement> Ledger(Fixture f) => await ReportingFixture.Payments(f.Client, f.ClassId);
    private static async Task<JsonElement> View(Fixture f, string? month = null) => await f.Client.GetFromJsonAsync<JsonElement>(Reserve(f.ClassId, month is null ? "" : $"?month={month}"));
    private static JsonElement Student(JsonElement view, Guid id) => view.GetProperty("students").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == id);
    private static JsonElement Period(JsonElement student, string month) => student.GetProperty("periods").EnumerateArray().Single(x => x.GetProperty("month").GetString() == month);

    private static async Task<HttpResponseMessage> Use(Fixture f, long amount, string month = "2026-11", Guid? key = null, long? version = null) =>
        await TestApplication.Post(f.Client, Reserve(f.ClassId, "/uses"), new
        { studentId = f.StudentId, month, amountVnd = amount.ToString(), idempotencyKey = key ?? Guid.NewGuid(), version = version ?? (await Ledger(f)).GetProperty("version").GetInt64() });

    private static Task<HttpResponseMessage> Reverse(Fixture f, Guid useId, Guid? key = null, string reason = "Nhập nhầm kỳ") =>
        TestApplication.Post(f.Client, Reserve(f.ClassId, $"/uses/{useId}/reversal"), new { reason, idempotencyKey = key ?? Guid.NewGuid() });

    private static async Task<Guid> Used(HttpResponseMessage response) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }

    [Fact]
    public async Task A_use_lowers_that_periods_debt_and_the_reserve_balance_but_creates_no_money()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            var before = await Ledger(f);
            Assert.Equal("500000", before.GetProperty("totalDebtVnd").GetString());
            var cashflowPath = $"/api/billing/classes/{f.ClassId}/cashflow?from=2026-11-01&to=2026-11-30";
            var cashflow = (await f.Client.GetFromJsonAsync<JsonElement>(cashflowPath)).GetProperty("totalReceivedVnd").GetString();
            var balanceBefore = Student(await View(f), f.StudentId).GetProperty("reserve");
            Assert.Equal(("450000", "0", "450000"), (balanceBefore.GetProperty("approvedVnd").GetString(), balanceBefore.GetProperty("usedVnd").GetString(), balanceBefore.GetProperty("remainingVnd").GetString()));

            await Used(await Use(f, 450_000));

            var after = await Ledger(f);
            Assert.Equal("50000", after.GetProperty("totalDebtVnd").GetString());
            var november = after.GetProperty("students")[0].GetProperty("periods").EnumerateArray().Single(x => x.GetProperty("month").GetString() == "2026-11");
            Assert.Equal(("500000", "50000", "450000", "partial"), (november.GetProperty("feesVnd").GetString(), november.GetProperty("debtVnd").GetString(),
                november.GetProperty("reserveAppliedVnd").GetString(), november.GetProperty("status").GetString()));
            Assert.Equal(cashflow, (await f.Client.GetFromJsonAsync<JsonElement>(cashflowPath)).GetProperty("totalReceivedVnd").GetString()); // not newly received
            var student = Student(await View(f), f.StudentId);
            Assert.Equal(("450000", "450000", "0"), (student.GetProperty("reserve").GetProperty("approvedVnd").GetString(), student.GetProperty("reserve").GetProperty("usedVnd").GetString(), student.GetProperty("reserve").GetProperty("remainingVnd").GetString()));
            Assert.Equal("450000", student.GetProperty("reservedVnd").GetString()); // the receipt still holds the money; it is just spent
            // The ledger of the month the money moved in: nothing carried forward.
            var movements = Student(await View(f, CurrentMonth), f.StudentId).GetProperty("movements");
            Assert.Equal(("450000", "450000", "0"), (movements.GetProperty("approvedVnd").GetString(), movements.GetProperty("usedVnd").GetString(), movements.GetProperty("carriedVnd").GetString()));

            // A new receipt now only allocates what is still payable; the surplus stays unallocated, not paid twice.
            var paid = await TestApplication.Post(f.Client, $"/api/billing/classes/{f.ClassId}/payments", new { studentId = f.StudentId, amountVnd = "100000", receivedOn = "2026-11-20", idempotencyKey = Guid.NewGuid(), version = after.GetProperty("version").GetInt64() });
            var receipt = await paid.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.Created, paid.StatusCode);
            Assert.Equal("50000", receipt.GetProperty("allocations")[0].GetProperty("amountVnd").GetString());
            Assert.Equal("50000", receipt.GetProperty("unallocatedVnd").GetString());
            Assert.Equal("0", (await Ledger(f)).GetProperty("totalDebtVnd").GetString());
        }
    }

    [Fact]
    public async Task A_use_is_limited_to_what_is_still_payable_and_to_what_the_reserve_still_holds()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Use(f, 0)).StatusCode);
            var overDebt = await Use(f, 500_001);
            Assert.Equal(HttpStatusCode.BadRequest, overDebt.StatusCode);
            Assert.Contains("tối đa 500000", await overDebt.Content.ReadAsStringAsync());
            var overReserve = await Use(f, 450_001);
            Assert.Equal(HttpStatusCode.BadRequest, overReserve.StatusCode);
            Assert.Contains("450000", await overReserve.Content.ReadAsStringAsync());
            var paidPeriod = await Use(f, 1, "2026-10"); // October is fully paid: no obligation left
            Assert.Equal(HttpStatusCode.BadRequest, paidPeriod.StatusCode);
            Assert.Contains("không còn nghĩa vụ", await paidPeriod.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest, (await Use(f, 1, "bad")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Use(f, 1_000, version: 1_000_000)).StatusCode); // stale ledger version
            Assert.Equal("450000", Student(await View(f), f.StudentId).GetProperty("reserve").GetProperty("remainingVnd").GetString()); // nothing changed
        }
    }

    [Fact]
    public async Task A_reserve_larger_than_the_fee_clears_the_debt_and_the_rest_stays_separate()
    {
        var f = await Approved(app, novemberSessions: 1); // November fee is only 250.000
        using (f.Client)
        {
            await Used(await Use(f, 250_000));
            var ledger = await Ledger(f);
            Assert.Equal("0", ledger.GetProperty("totalDebtVnd").GetString());
            Assert.Equal("paid", ledger.GetProperty("students")[0].GetProperty("periods").EnumerateArray().Single(x => x.GetProperty("month").GetString() == "2026-11").GetProperty("status").GetString());
            var reserve = Student(await View(f), f.StudentId).GetProperty("reserve");
            Assert.Equal("200000", reserve.GetProperty("remainingVnd").GetString());
            var none = await Use(f, 1);
            Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode); // nothing left to pay in November
        }
    }

    [Fact]
    public async Task The_same_command_is_applied_once_and_concurrent_uses_cannot_over_spend()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            var key = Guid.NewGuid();
            var version = (await Ledger(f)).GetProperty("version").GetInt64();
            var first = await Use(f, 450_000, key: key, version: version);
            var replay = await Use(f, 450_000, key: key, version: version + 99); // a lost response is retried, even with a stale version
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            Assert.Equal("50000", (await Ledger(f)).GetProperty("totalDebtVnd").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await Use(f, 1_000, key: key)).StatusCode); // the key belongs to another command

            var g = await Approved(app);
            using (g.Client)
            {
                var v = (await Ledger(g)).GetProperty("version").GetInt64();
                var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Use(g, 450_000, version: v)));
                Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.Created));
                Assert.All(results.Where(x => x.StatusCode != HttpStatusCode.Created), x => Assert.True(x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest, x.StatusCode.ToString()));
                Assert.Equal("450000", Student(await View(g), g.StudentId).GetProperty("reserve").GetProperty("usedVnd").GetString());
                Assert.Equal("50000", (await Ledger(g)).GetProperty("totalDebtVnd").GetString());
            }
        }
    }

    [Fact]
    public async Task Reversing_a_use_restores_the_debt_and_the_reserve_and_cannot_be_repeated_differently()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            var useId = await Used(await Use(f, 450_000));
            var key = Guid.NewGuid();
            Assert.Equal(HttpStatusCode.BadRequest, (await Reverse(f, useId, reason: " ")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Reverse(f, useId, key)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Reverse(f, useId, key)).StatusCode); // same command replays
            Assert.Equal(HttpStatusCode.Conflict, (await Reverse(f, useId, Guid.NewGuid())).StatusCode);
            Assert.Equal("500000", (await Ledger(f)).GetProperty("totalDebtVnd").GetString());
            var student = Student(await View(f), f.StudentId);
            Assert.Equal("450000", student.GetProperty("reserve").GetProperty("remainingVnd").GetString());
            Assert.Equal("reversed", student.GetProperty("uses")[0].GetProperty("status").GetString());
            Assert.Equal("Nhập nhầm kỳ", student.GetProperty("uses")[0].GetProperty("reversalReason").GetString());
            await Used(await Use(f, 450_000)); // the money can be used again
        }
    }

    [Fact]
    public async Task A_reserve_with_active_uses_cannot_be_cancelled_until_they_are_reversed_and_cancelling_frees_the_money_and_the_receipt()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            var useId = await Used(await Use(f, 300_000));
            string Path() => Reserve(f.ClassId, $"/requests/{f.RequestId}/cancellation");
            Task<HttpResponseMessage> Cancel(Guid? key = null, string reason = "Phụ huynh xin hủy", int? version = null) =>
                TestApplication.Post(f.Client, Path(), new { reason, idempotencyKey = key ?? Guid.NewGuid(), version = version ?? f.RequestVersion });
            var blocked = await Cancel();
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Contains("đã được khấu trừ 300000", await blocked.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, (await Reverse(f, useId)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Cancel(reason: " ")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Cancel(version: 99)).StatusCode);
            var key = Guid.NewGuid();
            var cancelled = await Cancel(key);
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
            var view = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("cancelled", view.GetProperty("status").GetString());
            Assert.Equal(["requested", "approved", "cancelled"], view.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray());
            Assert.Equal(HttpStatusCode.OK, (await Cancel(key, version: 99)).StatusCode); // replay
            Assert.Equal(HttpStatusCode.Conflict, (await Cancel(Guid.NewGuid())).StatusCode);

            var student = Student(await View(f), f.StudentId);
            Assert.Equal(("0", "450000"), (student.GetProperty("reservedVnd").GetString(), student.GetProperty("availableVnd").GetString())); // money is free again
            Assert.Equal("0", student.GetProperty("reserve").GetProperty("remainingVnd").GetString());
            Assert.Equal(HttpStatusCode.BadRequest, (await Use(f, 1)).StatusCode); // a cancelled reserve cannot be used

            // The receipt can be corrected again now that nothing is held on it.
            var payments = await Ledger(f);
            var receiptId = payments.GetProperty("students")[0].GetProperty("receipts")[0].GetProperty("id").GetGuid();
            var correction = await TestApplication.Post(f.Client, $"/api/billing/classes/{f.ClassId}/payments/{receiptId}/corrections",
                new { action = "cancel", reason = "Nhầm", idempotencyKey = Guid.NewGuid(), version = payments.GetProperty("version").GetInt64() });
            Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        }
    }

    [Fact]
    public async Task Reducing_the_fee_after_a_use_reports_the_excess_for_the_teacher_to_reverse_instead_of_reusing_it()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            await Used(await Use(f, 450_000));
            var version = (await Ledger(f)).GetProperty("version").GetInt64();
            var adjusted = await TestApplication.Post(f.Client, $"/api/billing/classes/{f.ClassId}/adjustments",
                new { studentId = f.StudentId, month = "2026-11", direction = "decrease", amountVnd = "300000", reason = "Giảm học phí", idempotencyKey = Guid.NewGuid(), version });
            Assert.Equal(HttpStatusCode.Created, adjusted.StatusCode);
            var ledger = await Ledger(f);
            Assert.Equal("0", ledger.GetProperty("totalDebtVnd").GetString());
            var period = Period(Student(await View(f), f.StudentId), "2026-11");
            Assert.Equal(("200000", "250000"), (period.GetProperty("reserveAppliedVnd").GetString(), period.GetProperty("reserveExcessVnd").GetString()));
        }
    }

    [Fact]
    public async Task The_pdf_shows_the_reserve_ledger_and_the_updated_total_and_the_old_export_becomes_outdated()
    {
        var f = await Approved(app);
        using (f.Client)
        {
            var path = $"/api/reporting/classes/{f.ClassId}/students/{f.StudentId}/months/2026-11";
            Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(f.Client, path + "/draft", ReportingFixture.Draft())).StatusCode);
            var (_, first) = await ReportDownloadTests.ExportAndLink(f.Client, path, 1);
            await Used(await Use(f, 450_000));
            var stale = (await f.Client.GetFromJsonAsync<JsonElement>(path + "/exports")).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == first.GetProperty("id").GetGuid());
            Assert.Equal("outdated", stale.GetProperty("freshness").GetProperty("state").GetString());
            Assert.Contains(stale.GetProperty("freshness").GetProperty("changes").EnumerateArray(), x => x.GetProperty("label").GetString()!.Contains("Bảo lưu"));

            var (url, second) = await ReportDownloadTests.ExportAndLink(f.Client, path, 1);
            using var pdf = PdfDocument.Open(await f.Client.GetByteArrayAsync(url));
            var text = string.Join(" ", pdf.GetPages().Select(p => p.Text));
            Assert.Contains("Bảo lưu đã dùng trong tháng", text);
            Assert.Contains("Bảo lưu chuyển sang kỳ sau", text);
            Assert.Contains("Đã khấu trừ bảo lưu vào học phí tháng này", text);
            Assert.Contains("450.000 đồng", text);
            Assert.Contains("Tổng cần thu: 50.000 đồng", text); // 500.000 of fees minus the 450.000 reserve
            Assert.NotEqual(first.GetProperty("sha256").GetString(), second.GetProperty("sha256").GetString());
        }
    }
}
