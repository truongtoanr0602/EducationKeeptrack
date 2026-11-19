using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Billing.Application;

namespace ApiTests;

// The sample QR (#54): the saved receiving account plus an amount the teacher types, tied to no student or debt.
public sealed class SampleQrTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZWQAAAAASUVORK5CYII=";
    private const string Recipient = "/api/billing/recipient";
    private const string Sample = "/api/billing/recipient/sample-qr";

    private sealed class Stub : IVietQrGenerator
    {
        public byte[]? Image = Convert.FromBase64String(Png);
        public Exception? Failure;
        public int Calls;
        public RecipientView? Recipient;
        public string? Amount;
        public string? Content;
        public Task<byte[]?> GenerateAsync(RecipientView recipient, string amountVnd, string content, CancellationToken ct)
        {
            Calls++; Recipient = recipient; Amount = amountVnd; Content = content;
            return Failure is null ? Task.FromResult(Image) : Task.FromException<byte[]?>(Failure);
        }
    }

    private static async Task<HttpClient> Teacher(TestApplication app, string email)
    {
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        return client;
    }

    private static Task<HttpResponseMessage> Save(HttpClient client, long version = 0, string? image = null) =>
        TestApplication.Post(client, Recipient, new { bankBin = "970436", bankName = "Vietcombank", accountNo = "0123456789", accountName = "NGUYEN VAN AN", staticQrBase64 = image, version });

    private static Task<HttpResponseMessage> Ask(HttpClient client, object amount) => TestApplication.Post(client, Sample, new { amountVnd = amount });

    [Fact]
    public async Task The_generator_gets_exactly_the_typed_amount_and_the_saved_account_and_nothing_is_written()
    {
        var stub = new Stub();
        await using var app = new TestApplication { QrGenerator = stub }; await app.InitializeAsync();
        using var client = await Teacher(app, "sample-ok@example.test");
        Assert.Equal(HttpStatusCode.OK, (await Save(client)).StatusCode);
        var version = (await client.GetFromJsonAsync<JsonElement>(Recipient)).GetProperty("version").GetInt64();

        var response = await Ask(client, "150000");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("150000", body.GetProperty("amountVnd").GetString());
        Assert.Equal("vietqr", body.GetProperty("qrKind").GetString());
        Assert.Equal("data:image/png;base64," + Png, body.GetProperty("qrDataUrl").GetString());
        Assert.Equal("EKT QR MAU", body.GetProperty("content").GetString());
        Assert.Equal("150000", stub.Amount);
        Assert.Equal("EKT QR MAU", stub.Content);
        Assert.Equal(("970436", "0123456789", "NGUYEN VAN AN"), (stub.Recipient!.BankBin, stub.Recipient.AccountNo, stub.Recipient.AccountName));

        // A second amount gives a new QR for the same account and the recipient row is untouched.
        Assert.Equal(HttpStatusCode.OK, (await Ask(client, " 9000 ")).StatusCode);
        Assert.Equal("9000", stub.Amount);
        Assert.Equal(version, (await client.GetFromJsonAsync<JsonElement>(Recipient)).GetProperty("version").GetInt64());
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("12.5")]
    [InlineData("0150000")]
    [InlineData("12345678901234")]
    public async Task An_amount_that_is_not_a_positive_whole_dong_of_at_most_13_digits_is_refused(string amount)
    {
        var stub = new Stub();
        await using var app = new TestApplication { QrGenerator = stub }; await app.InitializeAsync();
        using var client = await Teacher(app, $"sample-bad-{Math.Abs(amount.GetHashCode())}@example.test");
        await Save(client);
        var response = await Ask(client, amount);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("amountVnd", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, stub.Calls);
    }

    [Fact]
    public async Task A_number_in_the_body_instead_of_a_string_is_refused_too()
    {
        await using var app = new TestApplication { QrGenerator = new Stub() }; await app.InitializeAsync();
        using var client = await Teacher(app, "sample-number@example.test");
        await Save(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ask(client, 10000)).StatusCode);
    }

    [Fact]
    public async Task Without_a_saved_account_it_is_refused_and_another_teachers_account_is_never_used()
    {
        var stub = new Stub();
        await using var app = new TestApplication { QrGenerator = stub }; await app.InitializeAsync();
        using var owner = await Teacher(app, "sample-owner@example.test");
        using var other = await Teacher(app, "sample-other@example.test");
        await Save(owner);

        var response = await Ask(other, "10000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Chưa cấu hình tài khoản nhận tiền.", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, stub.Calls);
    }

    [Fact]
    public async Task An_expired_teacher_cannot_ask_for_one_but_can_still_read_the_account()
    {
        var stub = new Stub();
        await using var app = new TestApplication { QrGenerator = stub }; await app.InitializeAsync();
        using var client = await Teacher(app, "sample-expired@example.test");
        await Save(client);
        app.Clock.Advance(TimeSpan.FromDays(40));
        await TestApplication.Login(client, "sample-expired@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Recipient)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Ask(client, "10000")).StatusCode);
        Assert.Equal(0, stub.Calls);
    }

    [Fact]
    public async Task Anonymous_callers_get_nothing()
    {
        await using var app = new TestApplication { QrGenerator = new Stub() }; await app.InitializeAsync();
        using var anonymous = app.Client();
        var status = (await TestApplication.Post(anonymous, Sample, new { amountVnd = "10000" })).StatusCode;
        Assert.True(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, status.ToString());
    }

    [Fact]
    public async Task When_vietqr_fails_the_answer_is_the_static_image_if_there_is_one_and_otherwise_nothing()
    {
        var stub = new Stub();
        await using var app = new TestApplication { QrGenerator = stub }; await app.InitializeAsync();
        using var withStatic = await Teacher(app, "sample-static@example.test");
        using var plain = await Teacher(app, "sample-manual@example.test");
        await Save(withStatic, image: Png);
        await Save(plain);
        foreach (var failure in new Exception[] { new IOException("stream"), new HttpRequestException("down"), new TaskCanceledException("timeout") })
        {
            stub.Failure = failure;
            var fallback = await (await Ask(withStatic, "10000")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("static", fallback.GetProperty("qrKind").GetString());
            Assert.Equal("data:image/png;base64," + Png, fallback.GetProperty("qrDataUrl").GetString());
            var manual = await (await Ask(plain, "10000")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("manual", manual.GetProperty("qrKind").GetString());
            Assert.Equal(JsonValueKind.Null, manual.GetProperty("qrDataUrl").ValueKind);
        }
    }

    [Fact]
    public async Task A_generator_that_returns_something_that_is_not_a_png_is_treated_as_unavailable()
    {
        var stub = new Stub { Image = "<html>not a png</html>"u8.ToArray() };
        await using var app = new TestApplication { QrGenerator = stub }; await app.InitializeAsync();
        using var client = await Teacher(app, "sample-html@example.test");
        await Save(client);
        var body = await (await Ask(client, "10000")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("manual", body.GetProperty("qrKind").GetString());
    }
}
