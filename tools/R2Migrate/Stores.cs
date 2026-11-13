using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EducationKeeptrack.Reporting.Infrastructure;

namespace R2Migrate;

public sealed record BlobInfo(string Key, long Length);

/// <summary>A flat key/value object store. Keys are the logical export keys; each store applies its own physical prefix.</summary>
public interface IBlobStore
{
    string Name { get; }
    Task<IReadOnlyList<BlobInfo>> ListAsync(CancellationToken ct);
    Task<byte[]?> GetAsync(string key, CancellationToken ct);
    /// <summary>Creates or replaces the object. The migrator only calls this after checking the key is absent.</summary>
    Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct);
}

public sealed class InMemoryBlobStore(string name) : IBlobStore
{
    private readonly Dictionary<string, byte[]> objects = [];
    public string Name => name;
    public int PutCount { get; private set; }
    public Task<IReadOnlyList<BlobInfo>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<BlobInfo>>(objects.Select(x => new BlobInfo(x.Key, x.Value.LongLength)).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray());
    public Task<byte[]?> GetAsync(string key, CancellationToken ct) => Task.FromResult(objects.TryGetValue(key, out var bytes) ? bytes.ToArray() : null);
    public Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct) { objects[key] = bytes.ToArray(); PutCount++; return Task.CompletedTask; }
    public void Set(string key, byte[] bytes) => objects[key] = bytes.ToArray();
}

public sealed class StoreSettings
{
    public required string Endpoint { get; init; }
    public required string Bucket { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public string Region { get; init; } = "auto";
    public string KeyPrefix { get; init; } = "";
    public bool ForcePathStyle { get; init; } = true;
    /// <summary>Only for a legacy source inside a private network. Never allowed for the R2 destination.</summary>
    public bool AllowHttp { get; init; }
}

public sealed class S3BlobStore : IBlobStore, IDisposable
{
    private readonly StoreSettings settings;
    private readonly AmazonS3Client client;
    public string Name { get; }

    public S3BlobStore(string name, StoreSettings settings)
    {
        Name = name; this.settings = settings;
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttps && !(settings.AllowHttp && endpoint.Scheme == Uri.UriSchemeHttp))
            || endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0
            || string.IsNullOrWhiteSpace(settings.Bucket) || string.IsNullOrWhiteSpace(settings.AccessKey) || string.IsNullOrWhiteSpace(settings.SecretKey))
            throw new ToolException($"{name} store configuration is invalid.");
        _ = ReportObjectPrefix.Apply(settings.KeyPrefix, "probe"); // rejects unsafe prefixes
        client = new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), new AmazonS3Config
        {
            ServiceURL = endpoint.AbsoluteUri, AuthenticationRegion = settings.Region, ForcePathStyle = settings.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 2, Timeout = TimeSpan.FromSeconds(60)
        });
    }

    private string Physical(string key) => ReportObjectPrefix.Apply(settings.KeyPrefix, key);

    public async Task<IReadOnlyList<BlobInfo>> ListAsync(CancellationToken ct)
    {
        var result = new List<BlobInfo>();
        string? token = null;
        do
        {
            var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = settings.Bucket, Prefix = settings.KeyPrefix, ContinuationToken = token }, ct);
            result.AddRange((page.S3Objects ?? []).Select(x => new BlobInfo(x.Key[settings.KeyPrefix.Length..], x.Size ?? 0)));
            token = page.IsTruncated == true ? page.NextContinuationToken : null;
        } while (token is not null);
        return result.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetObjectAsync(settings.Bucket, Physical(key), ct);
            using var bytes = new MemoryStream();
            await response.ResponseStream.CopyToAsync(bytes, ct);
            return bytes.ToArray();
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes, false);
        // Same flags as the application's R2 store: R2 rejects the SDK's streaming signature/checksum trailers.
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = settings.Bucket, Key = Physical(key), InputStream = stream, ContentType = contentType,
            UseChunkEncoding = false, DisablePayloadSigning = true, DisableDefaultChecksumValidation = true
        }, ct);
    }

    public void Dispose() => client.Dispose();
}
