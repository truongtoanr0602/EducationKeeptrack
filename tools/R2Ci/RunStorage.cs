using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EducationKeeptrack.Reporting.Infrastructure;

namespace R2Ci;

public sealed record RunState(Guid Id, string Prefix)
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public static RunState Create()
    {
        var id = Guid.NewGuid();
        return new(id, $"ekt-ci/{id:N}/");
    }
    public void Validate()
    {
        if (Id == Guid.Empty || Prefix != $"ekt-ci/{Id:N}/" || CreatedAt > DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Invalid CI run state; refusing access.");
    }
    public string Scope(string branch)
    {
        Validate();
        _ = ReportObjectPrefix.Apply(branch, "scope-check");
        return Prefix + branch;
    }
}

public sealed record CleanupSummary(Guid RunId, bool Success, int Deleted);

public sealed class RunStorage : IDisposable
{
    private readonly AmazonS3Client client;
    private readonly string bucket;
    public static ReportingOptions Configuration()
    {
        string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value : throw new InvalidOperationException("Missing R2 test configuration.");
        var endpoint = Required("TEST_S3_ENDPOINT");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("Invalid test storage endpoint.");
        return new() { Endpoint = endpoint, Bucket = Required("TEST_S3_BUCKET"),
            AccessKey = Required("TEST_S3_ACCESS_KEY"), SecretKey = Required("TEST_S3_SECRET_KEY") };
    }
    public RunStorage(ReportingOptions config)
    {
        bucket = config.Bucket ?? throw new InvalidOperationException("Missing test bucket.");
        client = new(new BasicAWSCredentials(config.AccessKey, config.SecretKey), new AmazonS3Config {
            ServiceURL = config.Endpoint, AuthenticationRegion = "auto", ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 2, Timeout = TimeSpan.FromSeconds(30)
        });
    }
    public async Task<CleanupSummary> CleanupAsync(RunState state, CancellationToken ct, string branch = "")
    {
        var prefix = state.Scope(branch);
        var deleted = 0;
        while (true)
        {
            var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, Prefix = prefix }, ct);
            if (page.S3Objects is not { Count: > 0 }) return new(state.Id, true, deleted);
            foreach (var item in page.S3Objects)
            {
                if (!item.Key.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing cleanup outside owned run.");
                await client.DeleteObjectAsync(bucket, item.Key, ct);
                deleted++;
            }
        }
    }
    public void Dispose() => client.Dispose();
}
