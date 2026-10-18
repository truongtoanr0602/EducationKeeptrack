using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed partial class TuitionTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZWQAAAAASUVORK5CYII=";
    private sealed class QrStub : EducationKeeptrack.Billing.Application.IVietQrGenerator
    {
        public byte[]? Image = Convert.FromBase64String(Png);
        public Func<Task>? During;
        public string? Amount;
        public string? Content;
        public Exception? Failure;
        public async Task<byte[]?> GenerateAsync(EducationKeeptrack.Billing.Application.RecipientView r, string amount, string content, CancellationToken ct)
        {
            Amount = amount; Content = content;
            if (Failure is not null) throw Failure;
            if (During is not null) await During();
            return Image;
        }
    }
    private static Task<HttpResponseMessage> SaveRecipient(HttpClient client, long version = 0, string? image = null, string accountNo = "123456789") =>
        TestApplication.Post(client, "/api/billing/recipient", new { bankBin = "970415", bankName = "VietinBank", accountNo, accountName = "NGUYEN VAN AN", staticQrBase64 = image, version });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Malformed_png_and_transport_failure_use_static_fallback_and_invalid_upload_is_rejected(bool corruptHeader)
    {
        var malformed = Convert.FromBase64String(Png);
        // Keep PNG signature/end marker while corrupting either header or compressed pixels.
        if (corruptHeader) Array.Fill(malformed, (byte)0, 8, 25);
        else Array.Fill(malformed, (byte)0, malformed.AsSpan().IndexOf("IDAT"u8) + 4, 6);
        var qr = new QrStub { Image = malformed };
        await using var isolated = new TestApplication { QrGenerator = qr }; await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            await SaveRecipient(client, image: Png);
            var report = await Payments(client, classId);
            var input = new { studentId, month = "2026-11", version = report.GetProperty("version").GetInt64(), recipientVersion = 1 };
            var path = $"/api/billing/classes/{classId}/transfer-request";
            var first = await TestApplication.Post(client, path, input);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal("static", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("qrKind").GetString());
            foreach (var failure in new Exception[] { new IOException("Interrupted QR stream"), new HttpRequestException("QR unavailable"), new TaskCanceledException("QR timeout") })
            {
                qr.Failure = failure;
                var response = await TestApplication.Post(client, path, input);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("static", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("qrKind").GetString());
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await SaveRecipient(client, 1, Convert.ToBase64String(malformed))).StatusCode);
        }
    }

    [Fact]
    public async Task Vietqr_uses_total_debt_including_old_period_and_never_records_payment()
    {
        var qr = new QrStub();
        await using var isolated = new TestApplication { QrGenerator = qr }; await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            await TestApplication.Post(client, $"/api/billing/classes/{classId}/rates", new { effectiveFrom = "2026-11-01", unitPriceVnd = "900000", version = 1 });
            await Session(client, classId, studentId, "2026-11-02"); await Ready(client, classId, "2026-11");
            var before = await Payments(client, classId);
            await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", new { studentId, amountVnd = "700000", receivedOn = "2026-11-05", idempotencyKey = Guid.NewGuid(), version = before.GetProperty("version").GetInt64() });
            Assert.Equal(HttpStatusCode.OK, (await SaveRecipient(client)).StatusCode);
            var report = await Payments(client, classId);
            var response = await TestApplication.Post(client, $"/api/billing/classes/{classId}/transfer-request", new { studentId, month = "2026-11", version = report.GetProperty("version").GetInt64(), recipientVersion = 1 });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var transfer = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("700000", transfer.GetProperty("amountVnd").GetString());
            Assert.Equal("700000", qr.Amount); Assert.Contains("202611", qr.Content);
            Assert.Contains(studentId.Replace("-", "")[..10], qr.Content);
            Assert.Equal("vietqr", transfer.GetProperty("qrKind").GetString());
            Assert.Equal("NGUYEN VAN AN", transfer.GetProperty("accountName").GetString());
            Assert.Equal("700000", (await Payments(client, classId)).GetProperty("totalDebtVnd").GetString());
            Assert.Equal(1, (await Payments(client, classId)).GetProperty("students")[0].GetProperty("receipts").GetArrayLength());
        }
    }

    [Fact]
    public async Task Qr_failure_falls_back_to_private_static_then_complete_manual_details()
    {
        var qr = new QrStub { Image = null };
        await using var isolated = new TestApplication { QrGenerator = qr }; await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        var (other, otherClass, otherStudent) = await PaymentSetup(isolated);
        using (client) using (other)
        {
            Assert.Equal(HttpStatusCode.OK, (await SaveRecipient(client, image: Png)).StatusCode);
            var report = await Payments(client, classId);
            var input = new { studentId, month = "2026-11", version = report.GetProperty("version").GetInt64(), recipientVersion = 1 };
            var response = await TestApplication.Post(client, $"/api/billing/classes/{classId}/transfer-request", input);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("static", result.GetProperty("qrKind").GetString());
            Assert.Equal("data:image/png;base64," + Png, result.GetProperty("qrDataUrl").GetString());
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, $"/api/billing/classes/{classId}/transfer-request", input)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(client, $"/api/billing/classes/{otherClass}/transfer-request", new { studentId = otherStudent, month = "2026-11", version = 0, recipientVersion = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/billing/recipient")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SaveRecipient(client, 1, "")).StatusCode);
            response = await TestApplication.Post(client, $"/api/billing/classes/{classId}/transfer-request", new { input.studentId, input.month, input.version, recipientVersion = 2 });
            result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("manual", result.GetProperty("qrKind").GetString());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("qrDataUrl").ValueKind);
            Assert.Equal("500000", result.GetProperty("amountVnd").GetString());
            foreach (var field in new[] { "bankBin", "bankName", "accountNo", "accountName", "content" }) Assert.False(string.IsNullOrWhiteSpace(result.GetProperty(field).GetString()));
            Assert.Equal(HttpStatusCode.BadRequest, (await SaveRecipient(client, 2, "not png")).StatusCode);
        }
    }

    [Fact]
    public async Task Balance_or_recipient_changed_during_generation_discards_qr_and_zero_debt_is_rejected()
    {
        var qr = new QrStub();
        await using var isolated = new TestApplication { QrGenerator = qr }; await isolated.InitializeAsync();
        var (client, classId, studentId) = await PaymentSetup(isolated);
        using (client)
        {
            var report = await Payments(client, classId);
            var version = report.GetProperty("version").GetInt64();
            var path = $"/api/billing/classes/{classId}/transfer-request";
            var input = new { studentId, month = "2026-11", version, recipientVersion = 1 };
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, input)).StatusCode);
            await SaveRecipient(client, image: Png);
            qr.During = async () => { Assert.Equal(HttpStatusCode.OK, (await SaveRecipient(client, 1, accountNo: "987654321")).StatusCode); };
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, input)).StatusCode);
            var changed = await client.GetFromJsonAsync<JsonElement>("/api/billing/recipient");
            Assert.False(changed.GetProperty("hasStaticQr").GetBoolean());
            qr.During = async () => { Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/billing/classes/{classId}/payments", new { studentId, amountVnd = "500000", receivedOn = "2026-11-05", version, idempotencyKey = Guid.NewGuid() })).StatusCode); };
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new { studentId, month = "2026-11", version, recipientVersion = 2 })).StatusCode);
            qr.During = null;
            report = await Payments(client, classId);
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { studentId, month = "2026-11", version = report.GetProperty("version").GetInt64(), recipientVersion = 2 })).StatusCode);
        }
    }
    [Fact]
    public async Task Recipient_is_private_validated_and_survives_reload()
    {
        var (client, _, _, _) = await Setup();
        var (other, _, _, _) = await Setup();
        using (client) using (other)
        {
            const string path = "/api/billing/recipient";
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { bankBin = "bad", bankName = "Bank", accountNo = "1", accountName = "A", version = 0 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await TestApplication.Post(client, path, new { bankBin = "970415", bankName = "VietinBank", accountNo = "123456789", accountName = "NGUYEN VAN AN", version = 0 })).StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(path);
            Assert.Equal("123456789", saved.GetProperty("accountNo").GetString());
            Assert.Equal(1, saved.GetProperty("version").GetInt64());
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client, path, new { bankBin = "970415", bankName = "VietinBank", accountNo = "987654321", accountName = "NGUYEN VAN AN", version = 0 })).StatusCode);
        }
    }
}
