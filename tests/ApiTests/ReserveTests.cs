using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

/// <summary>Prepaid money and sick-leave reserves (#20). No policy exists yet, so these tests pin down what the system does enforce.</summary>
public sealed class ReserveTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static string Reserve(Guid classId, string suffix = "") => $"/api/billing/classes/{classId}/reserve{suffix}";

    private sealed record Fixture(HttpClient Client, Guid ClassId, Guid StudentId, string Email);

    /// <summary>Three held sessions at 250.000 = 750.000 of fees; the family paid 1.200.000 in advance, so 450.000 stayed unused.</summary>
    private static async Task<Fixture> Prepaid(TestApplication host, long amount = 1_200_000)
    {
        var (client, _, classId, studentId, email) = await ReportingFixture.Setup(host);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-10-01", unitPriceVnd = "250000", version = 0 })).StatusCode);
        foreach (var date in new[] { "2026-10-02", "2026-10-03", "2026-10-05" }) await ReportingFixture.Session(client, classId, studentId, date);
        var payments = await ReportingFixture.Payments(client, classId);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments",
            new { studentId, amountVnd = amount.ToString(), receivedOn = "2026-11-05", note = "Đóng trước", idempotencyKey = Guid.NewGuid(), version = payments.GetProperty("version").GetInt64() })).StatusCode);
        return new(client, classId, studentId, email);
    }

    private static object Request(Guid studentId, long amount, Guid? key = null, string reason = "Con ốm, nghỉ cả tuần") =>
        new { studentId, absentFrom = "2026-11-09", absentTo = "2026-11-13", reason, evidenceNote = "Giấy khám bệnh gửi qua Zalo", amountVnd = amount.ToString(), idempotencyKey = key ?? Guid.NewGuid() };

    private static async Task<JsonElement> View(Fixture f) => await f.Client.GetFromJsonAsync<JsonElement>(Reserve(f.ClassId));
    private static JsonElement Student(JsonElement view, Guid studentId) => view.GetProperty("students").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == studentId);
    private static async Task<JsonElement> Created(HttpResponseMessage response) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static Task<HttpResponseMessage> Decide(Fixture f, JsonElement request, string decision, Guid? key = null, string reason = "Đồng ý bảo lưu", int? version = null) =>
        TestApplication.Post(f.Client, Reserve(f.ClassId, $"/requests/{request.GetProperty("id").GetGuid()}/decision"),
            new { decision, reason, idempotencyKey = key ?? Guid.NewGuid(), version = version ?? request.GetProperty("version").GetInt32() });

    [Fact]
    public async Task Sources_show_collected_used_unused_and_that_no_policy_exists_yet()
    {
        var f = await Prepaid(app);
        using (f.Client)
        {
            var view = await View(f);
            var policy = view.GetProperty("policy");
            Assert.Equal("none", policy.GetProperty("status").GetString());
            Assert.Contains("Chưa có chính sách", policy.GetProperty("message").GetString());
            var student = Student(view, f.StudentId);
            Assert.Equal(("1200000", "750000", "450000", "0", "450000"), (student.GetProperty("collectedVnd").GetString(), student.GetProperty("usedVnd").GetString(),
                student.GetProperty("unusedVnd").GetString(), student.GetProperty("reservedVnd").GetString(), student.GetProperty("availableVnd").GetString()));
            var source = Assert.Single(student.GetProperty("sources").EnumerateArray());
            Assert.Equal("Đóng trước", source.GetProperty("note").GetString());
            Assert.Equal("450000", source.GetProperty("availableVnd").GetString());
        }
    }

    [Fact]
    public async Task Nothing_can_be_reserved_without_money_paid_and_unused_and_pending_requests_hold_their_share()
    {
        var f = await Prepaid(app);
        var (unpaid, _, unpaidClass, unpaidStudent, _) = await ReportingFixture.Setup(app);
        using (f.Client)
        using (unpaid)
        {
            // A student who was absent but never paid has nothing to reserve.
            var none = await TestApplication.Post(unpaid, Reserve(unpaidClass, "/requests"), Request(unpaidStudent, 1));
            Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
            Assert.Contains("chưa có tiền đã nhận mà chưa dùng", await none.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 450_001))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 0))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 100, reason: "  "))).StatusCode);
            await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 300_000)));
            // 300.000 is already asked for, so only 150.000 of the 450.000 can still be requested.
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 200_000))).StatusCode);
            await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 150_000)));
        }
    }

    [Fact]
    public async Task Approval_moves_only_unused_money_records_its_sources_and_history_and_leaves_debt_and_cashflow_alone()
    {
        var f = await Prepaid(app);
        using (f.Client)
        {
            var before = await ReportingFixture.Payments(f.Client, f.ClassId);
            var cashflowPath = $"/api/billing/classes/{f.ClassId}/cashflow?from=2026-11-01&to=2026-11-30";
            var cashflowBefore = await f.Client.GetFromJsonAsync<JsonElement>(cashflowPath);
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 450_000)));
            Assert.Equal("pending", request.GetProperty("status").GetString());

            var approved = await (await Decide(f, request, "approve", reason: "Phụ huynh có giấy khám bệnh")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("approved", approved.GetProperty("status").GetString());
            var sources = approved.GetProperty("sources").EnumerateArray().ToArray();
            Assert.Equal("450000", Assert.Single(sources).GetProperty("amountVnd").GetString());
            var history = approved.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray();
            Assert.Equal(["requested", "approved"], history);
            Assert.Equal("Phụ huynh có giấy khám bệnh", approved.GetProperty("history")[1].GetProperty("reason").GetString());

            var student = Student(await View(f), f.StudentId);
            Assert.Equal(("450000", "0"), (student.GetProperty("reservedVnd").GetString(), student.GetProperty("availableVnd").GetString()));
            Assert.Equal("450000", student.GetProperty("unusedVnd").GetString()); // still unallocated, but held
            // The reserve is not newly received money and does not touch fees or debt.
            var after = await ReportingFixture.Payments(f.Client, f.ClassId);
            Assert.Equal(before.GetProperty("totalDebtVnd").GetString(), after.GetProperty("totalDebtVnd").GetString());
            Assert.True(after.GetProperty("version").GetInt64() > before.GetProperty("version").GetInt64());
            Assert.Equal(cashflowBefore.GetProperty("totalReceivedVnd").GetString(), (await f.Client.GetFromJsonAsync<JsonElement>(cashflowPath)).GetProperty("totalReceivedVnd").GetString());

            // The same money cannot be asked for again.
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 1))).StatusCode);
        }
    }

    [Fact]
    public async Task Rejecting_moves_no_money_and_releases_the_pending_share()
    {
        var f = await Prepaid(app);
        using (f.Client)
        {
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 450_000)));
            var rejected = await (await Decide(f, request, "reject", reason: "Chưa đủ minh chứng")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("rejected", rejected.GetProperty("status").GetString());
            Assert.Empty(rejected.GetProperty("sources").EnumerateArray());
            Assert.Equal("0", Student(await View(f), f.StudentId).GetProperty("reservedVnd").GetString());
            await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 450_000))); // available again
        }
    }

    [Fact]
    public async Task A_decision_is_idempotent_stale_versions_conflict_and_a_decided_request_cannot_change()
    {
        var f = await Prepaid(app);
        using (f.Client)
        {
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 200_000)));
            Assert.Equal(HttpStatusCode.Conflict, (await Decide(f, request, "approve", version: 7)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Decide(f, request, "approve", reason: " ")).StatusCode);
            var key = Guid.NewGuid();
            var first = await Decide(f, request, "approve", key);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var replay = await Decide(f, request, "approve", key, version: 99); // a lost response is retried, even with an old version
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal("200000", Student(await View(f), f.StudentId).GetProperty("reservedVnd").GetString()); // not reserved twice
            Assert.Equal(HttpStatusCode.Conflict, (await Decide(f, request, "reject", Guid.NewGuid())).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Decide(f, request, "approve", Guid.NewGuid())).StatusCode);
            // The key of a decision cannot be reused for another request.
            var other = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 100_000)));
            Assert.Equal(HttpStatusCode.Conflict, (await Decide(f, other, "approve", key)).StatusCode);
        }
    }

    [Fact]
    public async Task Concurrent_decisions_on_one_request_reserve_the_money_once()
    {
        var f = await Prepaid(app);
        using (f.Client)
        {
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 450_000)));
            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Decide(f, request, "approve", Guid.NewGuid())));
            Assert.Equal(1, results.Count(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(4, results.Count(x => x.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal("450000", Student(await View(f), f.StudentId).GetProperty("reservedVnd").GetString());
        }
    }

    [Fact]
    public async Task A_failure_during_approval_rolls_everything_back_and_the_same_key_can_retry()
    {
        var failure = new FailReserveSave();
        await using var isolated = new TestApplication { BillingInterceptor = failure };
        await isolated.InitializeAsync();
        var f = await Prepaid(isolated);
        using (f.Client)
        {
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 450_000)));
            var versionBefore = (await ReportingFixture.Payments(f.Client, f.ClassId)).GetProperty("version").GetInt64();
            var key = Guid.NewGuid();
            failure.Armed = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => Decide(f, request, "approve", key));
            var student = Student(await View(f), f.StudentId);
            Assert.Equal("0", student.GetProperty("reservedVnd").GetString());
            Assert.Equal("pending", student.GetProperty("requests")[0].GetProperty("status").GetString());
            Assert.Equal(versionBefore, (await ReportingFixture.Payments(f.Client, f.ClassId)).GetProperty("version").GetInt64());
            Assert.Equal(HttpStatusCode.OK, (await Decide(f, request, "approve", key)).StatusCode);
            Assert.Equal("450000", Student(await View(f), f.StudentId).GetProperty("reservedVnd").GetString());
        }
    }

    [Fact]
    public async Task Receipts_with_reserved_money_cannot_be_corrected_until_reserves_are_handled()
    {
        var f = await Prepaid(app);
        using (f.Client)
        {
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 100_000)));
            Assert.Equal(HttpStatusCode.OK, (await Decide(f, request, "approve")).StatusCode);
            var payments = await ReportingFixture.Payments(f.Client, f.ClassId);
            var receipt = payments.GetProperty("students").EnumerateArray().Single(x => x.GetProperty("studentId").GetGuid() == f.StudentId).GetProperty("receipts")[0];
            var response = await TestApplication.Post(f.Client, $"/api/billing/classes/{f.ClassId}/payments/{receipt.GetProperty("id").GetGuid()}/corrections",
                new { action = "cancel", reason = "Nhầm", idempotencyKey = Guid.NewGuid(), version = payments.GetProperty("version").GetInt64() });
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("bảo lưu", await response.Content.ReadAsStringAsync());
            Assert.Equal("1200000", Student(await View(f), f.StudentId).GetProperty("collectedVnd").GetString());
        }
    }

    [Fact]
    public async Task Another_teacher_sees_nothing_and_an_expired_teacher_can_read_but_not_write()
    {
        await using var isolated = new TestApplication();
        await isolated.InitializeAsync();
        var f = await Prepaid(isolated);
        var (other, _, otherClass, _, _) = await ReportingFixture.Setup(isolated);
        using (f.Client)
        using (other)
        {
            var request = await Created(await TestApplication.Post(f.Client, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 100_000)));
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Reserve(f.ClassId))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 1))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, Reserve(otherClass, $"/requests/{request.GetProperty("id").GetGuid()}/decision"),
                new { decision = "approve", reason = "x", idempotencyKey = Guid.NewGuid(), version = 1 })).StatusCode);

            isolated.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = isolated.Client();
            await TestApplication.Login(expired, f.Email);
            Assert.Equal(HttpStatusCode.OK, (await expired.GetAsync(Reserve(f.ClassId))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, Reserve(f.ClassId, "/requests"), Request(f.StudentId, 1))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(expired, Reserve(f.ClassId, $"/requests/{request.GetProperty("id").GetGuid()}/decision"),
                new { decision = "approve", reason = "x", idempotencyKey = Guid.NewGuid(), version = 1 })).StatusCode);
        }
    }

    private sealed class FailReserveSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public bool Armed;
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<EducationKeeptrack.Billing.Domain.ReserveSource>().Any())
            {
                Armed = false;
                throw new InvalidOperationException("Injected database failure after the reserve was written, before commit");
            }
            return ValueTask.FromResult(result);
        }
    }
}
