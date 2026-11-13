using System.Net;
using System.Net.Sockets;
using System.Text;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiTests;

public sealed class SmtpEmailSenderTests
{
    /// <summary>A one-shot SMTP server that accepts a single message and records the envelope and DATA.</summary>
    private sealed class FakeSmtp : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public string? MailFrom, RcptTo, Data;
        public Task Done { get; }
        public FakeSmtp() { listener.Start(); Done = Run(); }

        private async Task Run()
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            async Task Send(string line) => await stream.WriteAsync(Encoding.ASCII.GetBytes(line + "\r\n"));
            await Send("220 fake ready");
            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase)) await Send("250 fake");
                else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase)) { MailFrom = line; await Send("250 ok"); }
                else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase)) { RcptTo = line; await Send("250 ok"); }
                else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await Send("354 go");
                    var data = new StringBuilder();
                    while (await reader.ReadLineAsync() is { } part && part != ".") data.AppendLine(part);
                    Data = data.ToString();
                    await Send("250 queued");
                }
                else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase)) { await Send("221 bye"); break; }
                else await Send("250 ok");
            }
        }
        public async ValueTask DisposeAsync() { listener.Stop(); try { await Done; } catch { /* the sender may hang up first */ } }
    }

    private sealed class ListLogger : ILogger<SmtpEmailSender>
    {
        public readonly List<string> Lines = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add($"{logLevel}: {formatter(state, exception)} {exception}");
    }

    private static SmtpEmailSender Sender(int port, ListLogger logger, string password = "", string user = "") => new(Options.Create(new EmailOptions
    {
        PublicUrl = "https://keeptrack.example.test",
        Smtp = new() { Host = "127.0.0.1", Port = port, EnableSsl = false, From = "noreply@keeptrack.example.test", User = user, Password = password, TimeoutSeconds = 5 }
    }), logger);

    private static string Body(string data)
    {
        var split = data.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (split < 0) split = data.IndexOf("\n\n", StringComparison.Ordinal);
        var headers = data[..split]; var body = data[split..].Trim();
        return headers.Contains("base64", StringComparison.OrdinalIgnoreCase) ? Encoding.UTF8.GetString(Convert.FromBase64String(body.Replace("\r", "").Replace("\n", ""))) : body;
    }

    [Fact]
    public async Task Sends_a_plain_text_utf8_message_from_the_configured_sender()
    {
        await using var server = new FakeSmtp();
        var logger = new ListLogger();
        await Sender(server.Port, logger).SendAsync(new EmailMessage("giaovien@example.test", "Xác minh email", "Mở liên kết https://keeptrack.example.test/xacminh/abc123 để xác minh."), default);
        await server.Done.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("noreply@keeptrack.example.test", server.MailFrom);
        Assert.Contains("giaovien@example.test", server.RcptTo);
        Assert.Contains("Mở liên kết https://keeptrack.example.test/xacminh/abc123 để xác minh.", Body(server.Data!));
        Assert.Contains("charset=utf-8", server.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(logger.Lines, x => x.StartsWith("Error"));
    }

    [Fact]
    public async Task A_delivery_failure_is_logged_without_secrets_or_message_content_and_does_not_throw()
    {
        var closed = new TcpListener(IPAddress.Loopback, 0); closed.Start(); var port = ((IPEndPoint)closed.LocalEndpoint).Port; closed.Stop(); // nothing listens here
        var logger = new ListLogger();
        await Sender(port, logger, password: "s3cret-app-password", user: "mailer@example.test")
            .SendAsync(new EmailMessage("giaovien@example.test", "Đặt lại mật khẩu", "token=TOKEN-IN-BODY"), default);
        var error = Assert.Single(logger.Lines);
        Assert.StartsWith("Error", error);
        Assert.Contains("Đặt lại mật khẩu", error); // the subject identifies what failed
        foreach (var secret in new[] { "s3cret-app-password", "TOKEN-IN-BODY", "giaovien@example.test", "mailer@example.test" }) Assert.DoesNotContain(secret, error);
    }

    [Fact]
    public async Task An_invalid_recipient_is_swallowed_and_nothing_is_sent()
    {
        await using var server = new FakeSmtp();
        var logger = new ListLogger();
        await Sender(server.Port, logger).SendAsync(new EmailMessage("not an address", "Chủ đề", "Nội dung"), default);
        Assert.Null(server.Data);
        Assert.Single(logger.Lines);
    }

    [Theory]
    [InlineData("", 587, "", "", "https://keeptrack.example.test", 0)] // SMTP disabled: nothing to validate
    [InlineData("smtp.gmail.com", 587, "me@gmail.com", "pw", "https://keeptrack.example.test", 0)]
    [InlineData("smtp.gmail.com", 70000, "me@gmail.com", "pw", "https://keeptrack.example.test", 1)]
    [InlineData("smtp.gmail.com", 587, "me@gmail.com", "", "https://keeptrack.example.test", 1)] // user without password
    [InlineData("smtp.gmail.com", 587, "me@gmail.com", "pw", "http://keeptrack.example.test", 1)] // links must be https
    [InlineData("smtp.gmail.com", 587, "me@gmail.com", "pw", "https://keeptrack.example.test/", 1)] // trailing slash doubles in links
    [InlineData("smtp.gmail.com", 587, "me@gmail.com", "pw", "http://127.0.0.1:5173", 1)] // the development default
    public void Options_validation_flags_half_configured_smtp_and_unusable_public_urls(string host, int port, string user, string password, string publicUrl, int problems)
    {
        var options = new EmailOptions { PublicUrl = publicUrl, Smtp = new() { Host = host, Port = port, User = user, Password = password } };
        Assert.Equal(problems, EmailOptionsValidation.Problems(options).Count());
    }

    [Fact]
    public async Task The_host_refuses_to_start_with_smtp_configured_but_an_unusable_public_url()
    {
        await using var isolated = new TestApplication();
        await isolated.InitializeAsync();
        using var bad = isolated.WithWebHostBuilder(b =>
        {
            b.UseSetting("Identity:Email:Smtp:Host", "smtp.example.test");
            b.UseSetting("Identity:Email:Smtp:From", "noreply@example.test");
            b.UseSetting("Identity:Email:PublicUrl", "http://127.0.0.1:5173");
        });
        var failure = Assert.ThrowsAny<Exception>(() => bad.CreateClient());
        Assert.Contains("PublicUrl", failure.ToString());
    }
}
