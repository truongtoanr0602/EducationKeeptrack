using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Application;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTests;

// The "you can start" email (UX8-6): one message per successful grant, never for a failed one, and a failing
// mail provider must not undo the grant.
public sealed class AccessGrantedEmailTests
{
    private sealed class FailingSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => throw new InvalidOperationException("mail provider down");
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static IReadOnlyList<EmailMessage> Granted(TestApplication app, string email) =>
        [.. app.Emails.Where(x => x.To == email && x.Subject.Contains("đã được cấp quyền"))];

    private static async Task<(HttpClient Admin, string Key)> SignInAdmin(TestApplication app, HttpClient? client = null)
    {
        var key = await app.AddAdmin("grant-admin@example.test");
        var admin = client ?? app.Client();
        await app.LoginAdmin(admin, "grant-admin@example.test", key);
        return (admin, key);
    }

    [Fact]
    public async Task Approving_a_request_emails_the_teacher_once_with_a_link_to_the_setup()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        var (admin, _) = await SignInAdmin(app);
        var teacherId = await app.AddTeacher("waiting@example.test", approved: false);
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "waiting@example.test");
        var created = await Json(await TestApplication.Post(teacher, "/api/service/payment-requests", new { transactionCode = "FT77", note = "", idempotencyKey = "k-1" }));
        var requestId = created.GetProperty("id").GetString();
        var request = (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/teachers/{teacherId}")).GetProperty("requests").EnumerateArray().Single();
        var preview = await Json(await TestApplication.Post(admin, $"/api/admin/teachers/{teacherId}/period-preview", new { days = 30 }));
        var approve = new { days = 30, label = "Gói 1 tháng", expectedStartsAt = preview.GetProperty("startsAt").GetDateTimeOffset(), version = request.GetProperty("version").GetUInt32() };

        Assert.Empty(Granted(app, "waiting@example.test"));
        // Two Admins click at once: one grant, so one email.
        var results = await Task.WhenAll(
            TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", approve),
            TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", approve));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(admin, $"/api/admin/payment-requests/{requestId}/approve", approve)).StatusCode);

        var mail = Assert.Single(Granted(app, "waiting@example.test"));
        Assert.Contains("http://127.0.0.1:5173/#/setup/1", mail.Body);
        Assert.Equal("active", (await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_direct_grant_emails_once_per_grant_and_a_refused_grant_sends_nothing()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        var (admin, _) = await SignInAdmin(app);
        var id = await app.AddTeacher("direct@example.test", approved: false);
        var unconfirmed = await app.AddTeacher("unconfirmed@example.test", approved: false, confirmed: false);
        var start = app.Clock.GetUtcNow();

        var refused = await TestApplication.Post(admin, $"/api/admin/teachers/{unconfirmed}/periods", new { days = 30, label = "", expectedStartsAt = start, reason = "Cấp thử" });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Empty(Granted(app, "unconfirmed@example.test"));

        var first = await TestApplication.Post(admin, $"/api/admin/teachers/{id}/periods", new { days = 30, label = "Cấp", expectedStartsAt = start, reason = "Cấp trực tiếp" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Single(Granted(app, "direct@example.test"));

        // A stale preview is refused, and a refused call sends nothing.
        var stale = await TestApplication.Post(admin, $"/api/admin/teachers/{id}/periods", new { days = 30, label = "Cấp", expectedStartsAt = start, reason = "Cấp lại" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Single(Granted(app, "direct@example.test"));
    }

    [Fact]
    public async Task A_failing_mail_provider_does_not_undo_the_grant()
    {
        await using var app = new TestApplication(); await app.InitializeAsync();
        using var host = app.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<IEmailSender>(new FailingSender())));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (admin, _) = await SignInAdmin(app, client);
        var id = await app.AddTeacher("unlucky@example.test", approved: false);

        var response = await TestApplication.Post(admin, $"/api/admin/teachers/{id}/periods", new { days = 30, label = "Cấp", expectedStartsAt = app.Clock.GetUtcNow(), reason = "Cấp trực tiếp" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var teacher = app.Client();
        await TestApplication.Login(teacher, "unlucky@example.test");
        Assert.Equal("active", (await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("access").GetProperty("status").GetString());
        Assert.Empty(Granted(app, "unlucky@example.test"));
    }
}
