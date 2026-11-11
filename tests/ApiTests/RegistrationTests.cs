using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiTests;

public sealed class RegistrationTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpClient> Register(string email)
    {
        var client = app.Client();
        var response = await TestApplication.Post(client, "/api/auth/register", new { email, password = TestApplication.Password, role = "Admin" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return client;
    }

    private string LatestToken(string email, string action = "verify-email") =>
        Regex.Match(app.Emails.Last(x => x.To == email).Body, $@"action={action}&token=([A-Za-z0-9_-]+)").Groups[1].Value;

    private async Task<(HttpClient Client, string Key)> Admin(string email)
    {
        var key = await app.AddAdmin(email);
        var client = app.Client();
        await app.LoginAdmin(client, email, key);
        return (client, key);
    }

    [Fact]
    public async Task Registered_teacher_must_verify_email_before_anything_else()
    {
        using var client = await Register("new-teacher@example.test");
        await TestApplication.Login(client, "new-teacher@example.test");
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Teacher", me.GetProperty("role").GetString());
        Assert.Equal("unverified", me.GetProperty("access").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(client, "/api/service/payment-requests", new { transactionCode = "FT1", note = "", idempotencyKey = "k1" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, "/api/auth/verify-email", new { token = "not-a-real-token" })).StatusCode);
        var expiring = LatestToken("new-teacher@example.test");
        app.Clock.Advance(TimeSpan.FromHours(25));
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, "/api/auth/verify-email", new { token = expiring })).StatusCode);

        await TestApplication.Login(client, "new-teacher@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(client, "/api/auth/resend-verification", new { })).StatusCode);
        var token = LatestToken("new-teacher@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(client, "/api/auth/verify-email", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, "/api/auth/verify-email", new { token })).StatusCode);

        me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("pending", me.GetProperty("access").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/classes")).StatusCode);
    }

    [Fact]
    public async Task Admin_reconciles_payment_and_grants_first_period_once()
    {
        var (admin, _) = await Admin("grant-admin@example.test");
        var instructions = await admin.GetFromJsonAsync<JsonElement>("/api/admin/payment-instructions");
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Put(admin, "/api/admin/payment-instructions", new
        {
            bankDetails = "Ngân hàng A — STK 0001 — Chủ TK EKT",
            contact = "Zalo 0900000000",
            version = instructions.GetProperty("version").GetUInt32()
        })).StatusCode);

        using var teacher = await Register("paying-teacher@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(teacher, "/api/auth/verify-email", new { token = LatestToken("paying-teacher@example.test") })).StatusCode);
        await TestApplication.Login(teacher, "paying-teacher@example.test");
        var service = await teacher.GetFromJsonAsync<JsonElement>("/api/service");
        Assert.Equal("pending", service.GetProperty("status").GetString());
        Assert.Contains("STK 0001", service.GetProperty("instructions").GetProperty("bankDetails").GetString());

        var first = await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "FT26111", note = "Gói 3 tháng", idempotencyKey = "pay-1" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var requestId = (await Json(first)).GetProperty("id").GetString();
        var repeated = await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "FT26111", note = "Gói 3 tháng", idempotencyKey = "pay-1" });
        Assert.Equal(requestId, (await Json(repeated)).GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "FT999", note = "", idempotencyKey = "pay-2" })).StatusCode);
        Assert.Equal("pending", (await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("status").GetString());

        // A teacher can neither approve nor reach Admin data, even by guessing IDs.
        Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(teacher, $"/api/admin/payment-requests/{requestId}/approve", new { days = 30 })).StatusCode);

        var teacherId = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/teachers")).EnumerateArray().Single(x => x.GetProperty("email").GetString() == "paying-teacher@example.test").GetProperty("id").GetString();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}");
        var request = detail.GetProperty("requests").EnumerateArray().Single();
        Assert.Equal("FT26111", request.GetProperty("transactionCode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/verification", new { note = "Đã thấy 300.000đ trong sao kê", version = request.GetProperty("version").GetUInt32() })).StatusCode);

        var preview = await Json(await TestApplication.Post(admin, $"/api/admin/teachers/{teacherId}/period-preview", new { days = 90 }));
        var startsAt = preview.GetProperty("startsAt").GetDateTimeOffset();
        Assert.Equal(app.Clock.GetUtcNow(), startsAt);
        Assert.Equal(startsAt.AddDays(90), preview.GetProperty("endsAt").GetDateTimeOffset());

        request = (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}")).GetProperty("requests").EnumerateArray().Single();
        var approve = new { days = 90, label = "Gói 3 tháng", expectedStartsAt = startsAt, version = request.GetProperty("version").GetUInt32() };
        var results = await Task.WhenAll(
            TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", approve),
            TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", approve));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", approve)).StatusCode);

        detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}");
        var period = Assert.Single(detail.GetProperty("periods").EnumerateArray());
        Assert.Equal(startsAt.AddDays(90), period.GetProperty("endsAt").GetDateTimeOffset());
        var history = detail.GetProperty("history").EnumerateArray().ToList();
        Assert.Contains(history, x => x.GetProperty("action").GetString() == "request.verified");
        Assert.Contains(history, x => x.GetProperty("action").GetString() == "request.approved" && x.GetProperty("actor").GetString() == "grant-admin@example.test");

        Assert.Equal("active", (await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await teacher.GetAsync("/api/classes")).StatusCode);
    }

    [Fact]
    public async Task Rejection_keeps_the_account_and_allows_a_new_request()
    {
        var (admin, _) = await Admin("reject-admin@example.test");
        using var teacher = await Register("rejected-teacher@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(teacher, "/api/auth/verify-email", new { token = LatestToken("rejected-teacher@example.test") })).StatusCode);
        await TestApplication.Login(teacher, "rejected-teacher@example.test");
        var requestId = (await Json(await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "WRONG", note = "", idempotencyKey = "r-1" }))).GetProperty("id").GetString();

        var teacherId = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/teachers")).EnumerateArray().Single(x => x.GetProperty("email").GetString() == "rejected-teacher@example.test").GetProperty("id").GetString();
        var version = (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}")).GetProperty("requests").EnumerateArray().Single().GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/reject", new { reason = "", version })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/reject", new { reason = "Không tìm thấy giao dịch", version })).StatusCode);

        var service = await teacher.GetFromJsonAsync<JsonElement>("/api/service");
        Assert.Equal("rejected", service.GetProperty("status").GetString());
        Assert.Equal("Không tìm thấy giao dịch", service.GetProperty("requests").EnumerateArray().Single().GetProperty("decisionReason").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "FT-RIGHT", note = "", idempotencyKey = "r-2" })).StatusCode);
        Assert.Equal("pending", (await teacher.GetFromJsonAsync<JsonElement>("/api/service")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Stale_period_preview_and_unverified_accounts_are_not_granted()
    {
        var (admin, _) = await Admin("stale-admin@example.test");
        using var teacher = await Register("stale-teacher@example.test");
        Assert.Equal(HttpStatusCode.NoContent, (await TestApplication.Post(teacher, "/api/auth/verify-email", new { token = LatestToken("stale-teacher@example.test") })).StatusCode);
        await TestApplication.Login(teacher, "stale-teacher@example.test");
        var requestId = (await Json(await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "FT-S", note = "", idempotencyKey = "s-1" }))).GetProperty("id").GetString();
        var teacherId = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/teachers")).EnumerateArray().Single(x => x.GetProperty("email").GetString() == "stale-teacher@example.test").GetProperty("id").GetString();
        var version = (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}")).GetProperty("requests").EnumerateArray().Single().GetProperty("version").GetUInt32();
        var preview = await Json(await TestApplication.Post(admin, $"/api/admin/teachers/{teacherId}/period-preview", new { days = 30 }));
        app.Clock.Advance(TimeSpan.FromMinutes(5));
        var stale = await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", new { days = 30, label = "", expectedStartsAt = preview.GetProperty("startsAt").GetDateTimeOffset(), version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(admin, $"/api/admin/teachers/{teacherId}/period-preview", new { days = 0 })).StatusCode);

        var unverifiedId = await app.AddTeacher("unverified-applicant@example.test", approved: false, confirmed: false);
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(admin, $"/api/admin/teachers/{unverifiedId}/period-preview", new { days = 30 })).StatusCode);
    }
}
