using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EducationKeeptrack.Reporting.Application;
using Microsoft.Extensions.Options;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class S3ReportStore(IOptions<ReportingOptions> options) : IReportObjectStore, IDisposable
{
    private readonly Lazy<AmazonS3Client> client = new(() =>
    {
        var config = options.Value;
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
            || endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.UserInfo.Length != 0
            || string.IsNullOrWhiteSpace(config.Bucket) || string.IsNullOrWhiteSpace(config.AccessKey) || string.IsNullOrWhiteSpace(config.SecretKey))
            throw new ReportStorageException("Chưa cấu hình kho PDF riêng tư.");
        return new(new BasicAWSCredentials(config.AccessKey, config.SecretKey), new AmazonS3Config
        {
            ServiceURL = endpoint.AbsoluteUri, AuthenticationRegion = config.Region, ForcePathStyle = config.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 2, Timeout = TimeSpan.FromSeconds(30)
        });
    });
    public async Task PutAsync(string key, byte[] pdf, CancellationToken ct)
    {
        try
        {
            using var stream = new MemoryStream(pdf, false);
            // R2 does not support the SDK's streaming SigV4/checksum trailers. HTTPS is mandatory above.
            await client.Value.PutObjectAsync(new PutObjectRequest
            {
                BucketName = options.Value.Bucket, Key = key, InputStream = stream, ContentType = "application/pdf",
                UseChunkEncoding = false, DisablePayloadSigning = true, DisableDefaultChecksumValidation = true
            }, ct);
        }
        catch (AmazonS3Exception e) { throw new ReportStorageException("Không thể lưu PDF vào kho riêng tư. Vui lòng thử lại.", e); }
        catch (HttpRequestException e) { throw new ReportStorageException("Kho PDF tạm thời không truy cập được.", e); }
    }
    public async Task<Stream> OpenAsync(string key, CancellationToken ct)
    {
        try
        {
            using var response = await client.Value.GetObjectAsync(options.Value.Bucket, key, ct);
            var bytes = new MemoryStream(); await response.ResponseStream.CopyToAsync(bytes, ct); bytes.Position = 0; return bytes;
        }
        catch (AmazonS3Exception e) { throw new ReportStorageException("Không thể tải PDF từ kho riêng tư.", e); }
        catch (HttpRequestException e) { throw new ReportStorageException("Kho PDF tạm thời không truy cập được.", e); }
    }
    public void Dispose() { if (client.IsValueCreated) client.Value.Dispose(); }
}
