using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiTests;

public sealed class ReportStorageUnavailableTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Network_or_credential_failure_keeps_export_failed_and_valid_storage_can_retry(bool invalidCredentials)
    {
        var endpoint = Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT");
        var bucket = Environment.GetEnvironmentVariable("TEST_S3_BUCKET");
        Assert.False(string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(bucket),
            "R2 integration has NOT been verified. Configure TEST_S3_* with a dedicated test bucket.");
        var settings = new ReportingOptions
        {
            Endpoint = invalidCredentials ? endpoint : "https://127.0.0.1:1",
            Bucket = bucket, AccessKey = new string('0', 32), SecretKey = new string('0', 64)
        };
        using var failedHost = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IReportObjectStore>(new S3ReportStore(Options.Create(settings)))));
        var (owner, path, _, _, email) = await ReportingFixture.Setup(app);
        using (owner)
        using (var client = failedHost.CreateClient())
        {
            await TestApplication.Login(client, email);
            var preview = await (await TestApplication.Post(owner, path + "/previews", new { })).Content.ReadFromJsonAsync<JsonElement>();
            var input = new { previewId = preview.GetProperty("id").GetGuid(), draftVersion = 0, idempotencyKey = Guid.NewGuid() };
            var failure = await TestApplication.Post(client, path + "/exports", input);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
            var error = await failure.Content.ReadAsStringAsync();
            Assert.DoesNotContain(settings.SecretKey, error);
            Assert.DoesNotContain(settings.Endpoint!, error);
            var exports = await owner.GetFromJsonAsync<JsonElement>(path + "/exports");
            Assert.Equal("failed", exports[0].GetProperty("state").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client,
                path + "/exports/" + exports[0].GetProperty("id").GetGuid() + "/download-link", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/classes")).StatusCode);

            var retry = await TestApplication.Post(owner, path + "/exports", input);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var ready = await retry.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("ready", ready.GetProperty("state").GetString());
            Assert.Equal(exports[0].GetProperty("id").GetGuid(), ready.GetProperty("id").GetGuid());
            Assert.Single((await owner.GetFromJsonAsync<JsonElement>(path + "/exports")).EnumerateArray());
            var link = await (await TestApplication.Post(owner,
                path + "/exports/" + ready.GetProperty("id").GetGuid() + "/download-link", new { })).Content.ReadFromJsonAsync<JsonElement>();
            var bytes = await owner.GetByteArrayAsync(link.GetProperty("url").GetString());
            Assert.Equal(ready.GetProperty("sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        }
    }

    [Fact]
    public async Task Missing_storage_returns_503_without_publishing_a_ready_export_or_disabling_other_modules()
    {
        // Use the real adapter with missing configuration, not a successful in-memory storage replacement.
        using var host = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IReportObjectStore>(new S3ReportStore(Options.Create(new ReportingOptions())))));
        var (setup, path, _, _, email) = await ReportingFixture.Setup(app);
        setup.Dispose();
        using var client = host.CreateClient();
        await TestApplication.Login(client, email);
        var previewResponse = await TestApplication.Post(client, path + "/previews", new { });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var input = new { previewId = preview.GetProperty("id").GetGuid(), draftVersion = 0, idempotencyKey = Guid.NewGuid() };

        var failed = await TestApplication.Post(client, path + "/exports", input);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var exports = await client.GetFromJsonAsync<JsonElement>(path + "/exports");
        Assert.Equal("failed", exports[0].GetProperty("state").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await TestApplication.Post(client,
            path + "/exports/" + exports[0].GetProperty("id").GetGuid() + "/download-link", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/classes")).StatusCode);
    }
}
