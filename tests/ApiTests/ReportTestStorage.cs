using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.Extensions.Options;

namespace ApiTests;

// A fixture owns objects under one random prefix, never the shared bucket itself.
public sealed class ReportTestStorage : IAsyncDisposable
{
    private readonly string prefix = $"ekt-tests/{Guid.NewGuid():N}/";
    private readonly Lazy<ReportingOptions> options = new(ReadConfiguration);
    private bool used;
    private readonly List<S3ReportStore> stores = [];

    public string Bucket => options.Value.Bucket!;

    private static ReportingOptions ReadConfiguration()
    {
        var names = new[] { "TEST_S3_ENDPOINT", "TEST_S3_ACCESS_KEY", "TEST_S3_SECRET_KEY", "TEST_S3_BUCKET" };
        var missing = names.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("R2 integration has NOT been verified. Configure a pre-provisioned private test bucket: " + string.Join(", ", missing));
        return new ReportingOptions
        {
            Endpoint = Environment.GetEnvironmentVariable(names[0]), AccessKey = Environment.GetEnvironmentVariable(names[1]),
            SecretKey = Environment.GetEnvironmentVariable(names[2]), Bucket = Environment.GetEnvironmentVariable(names[3])
        };
    }

    public IReportObjectStore CreateStore()
    {
        // Configuration is required only for tests that actually use object storage.
        var store = new S3ReportStore(Options.Create(options.Value));
        lock (stores) stores.Add(store);
        used = true;
        return new ScopedStore(store, prefix);
    }

    private AmazonS3Client Client() => new(new BasicAWSCredentials(options.Value.AccessKey, options.Value.SecretKey), new AmazonS3Config
    {
        ServiceURL = options.Value.Endpoint, AuthenticationRegion = "auto", ForcePathStyle = true,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        MaxErrorRetry = 2, Timeout = TimeSpan.FromSeconds(30)
    });

    public async Task VerifyObjectAsync(string key, long length, CancellationToken ct)
    {
        using var client = Client();
        var head = await client.GetObjectMetadataAsync(Bucket, prefix + key, ct);
        Assert.Equal(length, head.ContentLength);
        Assert.Equal("application/pdf", head.Headers.ContentType);
        await client.DeleteObjectAsync(Bucket, prefix + key, ct);
        var missing = await Assert.ThrowsAsync<AmazonS3Exception>(() => client.GetObjectMetadataAsync(Bucket, prefix + key, ct));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        lock (stores)
        {
            foreach (var store in stores) store.Dispose();
            stores.Clear();
        }
        if (!used) return;
        using var client = Client();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        // Always list from the start after deleting a page; a continuation token can skip deleted keys.
        while (true)
        {
            var objects = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = prefix }, timeout.Token);
            if (objects.S3Objects is not { Count: > 0 }) break;
            foreach (var item in objects.S3Objects)
            {
                if (!item.Key.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing to clean an object outside this test run.");
                await client.DeleteObjectAsync(Bucket, item.Key, timeout.Token);
            }
        }
    }

    private sealed class ScopedStore(S3ReportStore inner, string prefix) : IReportObjectStore, IDisposable
    {
        public Task PutAsync(string key, byte[] pdf, CancellationToken ct) => inner.PutAsync(prefix + key, pdf, ct);
        public Task<Stream> OpenAsync(string key, CancellationToken ct) => inner.OpenAsync(prefix + key, ct);
        public void Dispose() => inner.Dispose();
    }
}
