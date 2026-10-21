using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.Extensions.Options;

namespace ApiTests;

public sealed class ReportStorageConfigurationTests
{
    [Theory]
    [InlineData("http://127.0.0.1:1")]
    [InlineData("not-an-endpoint")]
    [InlineData("https://account.r2.cloudflarestorage.com/bucket")]
    [InlineData("https://account.r2.cloudflarestorage.com?secret=value")]
    public async Task Invalid_storage_configuration_returns_safe_error_before_network_access(string endpoint)
    {
        using var store = new S3ReportStore(Options.Create(new ReportingOptions
        {
            Endpoint = endpoint, AccessKey = "test-access", SecretKey = "test-secret", Bucket = "test-bucket"
        }));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var write = await Assert.ThrowsAsync<ReportStorageException>(() => store.PutAsync("reports/test.pdf", [1], timeout.Token));
        var read = await Assert.ThrowsAsync<ReportStorageException>(() => store.OpenAsync("reports/test.pdf", timeout.Token));
        Assert.DoesNotContain(endpoint, write.Message);
        Assert.DoesNotContain("test-secret", read.Message);
    }
}
