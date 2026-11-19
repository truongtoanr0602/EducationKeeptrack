using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class S3LearningPhotoStore(IOptions<LearningStorageOptions> options, IConfiguration configuration) : ILearningPhotoStore, IDisposable
{
    private readonly Lazy<AmazonS3Client?> client = new(() =>
    {
        var config = options.Value;
        var endpointStr = config.Endpoint ?? configuration["Reporting:Endpoint"];
        var bucket = config.Bucket ?? configuration["Reporting:Bucket"];
        var accessKey = config.AccessKey ?? configuration["Reporting:AccessKey"];
        var secretKey = config.SecretKey ?? configuration["Reporting:SecretKey"];
        var region = string.IsNullOrWhiteSpace(config.Region) ? (configuration["Reporting:Region"] ?? "auto") : config.Region;

        if (string.IsNullOrWhiteSpace(endpointStr) || string.IsNullOrWhiteSpace(bucket) ||
            string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            return null;

        if (!Uri.TryCreate(endpointStr, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            return null;

        return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint.AbsoluteUri,
            AuthenticationRegion = region,
            ForcePathStyle = config.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 2,
            Timeout = TimeSpan.FromSeconds(30)
        });
    });

    private string Bucket => options.Value.Bucket ?? configuration["Reporting:Bucket"] ?? "ekt-photos";
    private string Prefix => options.Value.KeyPrefix ?? "photos/";

    public async Task PutPhotoAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        var s3 = client.Value ?? throw new PhotoStorageException("Chưa cấu hình kho lưu trữ ảnh R2.");
        try
        {
            using var stream = new MemoryStream(bytes, false);
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = Bucket,
                Key = CombineKey(Prefix, key),
                InputStream = stream,
                ContentType = contentType,
                UseChunkEncoding = false,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            }, ct);
        }
        catch (AmazonS3Exception e) { throw new PhotoStorageException("Không thể lưu ảnh vào kho riêng tư.", e); }
        catch (HttpRequestException e) { throw new PhotoStorageException("Kho lưu trữ ảnh tạm thời không truy cập được.", e); }
    }

    public async Task<Stream> OpenPhotoAsync(string key, CancellationToken ct)
    {
        var s3 = client.Value ?? throw new PhotoStorageException("Chưa cấu hình kho lưu trữ ảnh R2.");
        try
        {
            var response = await s3.GetObjectAsync(Bucket, CombineKey(Prefix, key), ct);
            var memory = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memory, ct);
            memory.Position = 0;
            return memory;
        }
        catch (AmazonS3Exception e) { throw new PhotoStorageException("Không thể tải ảnh từ kho riêng tư.", e); }
        catch (HttpRequestException e) { throw new PhotoStorageException("Kho lưu trữ ảnh tạm thời không truy cập được.", e); }
    }

    public async Task DeletePhotoAsync(string key, CancellationToken ct)
    {
        var s3 = client.Value;
        if (s3 is null) return;
        try
        {
            await s3.DeleteObjectAsync(Bucket, CombineKey(Prefix, key), ct);
        }
        catch (AmazonS3Exception e) { throw new PhotoStorageException("Không thể xóa ảnh khỏi kho riêng tư.", e); }
        catch (HttpRequestException e) { throw new PhotoStorageException("Kho lưu trữ ảnh tạm thời không truy cập được.", e); }
    }

    private static string CombineKey(string prefix, string key)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return key;
        return prefix.TrimEnd('/') + "/" + key.TrimStart('/');
    }

    public void Dispose()
    {
        if (client.IsValueCreated && client.Value is not null)
            client.Value.Dispose();
    }
}
