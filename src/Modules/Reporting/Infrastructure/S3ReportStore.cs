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
        if (string.IsNullOrWhiteSpace(config.Endpoint) || string.IsNullOrWhiteSpace(config.AccessKey) || string.IsNullOrWhiteSpace(config.SecretKey))
            throw new ReportStorageException("Chưa cấu hình kho PDF riêng tư.");
        return new(new BasicAWSCredentials(config.AccessKey, config.SecretKey), new AmazonS3Config { ServiceURL = config.Endpoint, AuthenticationRegion = config.Region, ForcePathStyle = config.ForcePathStyle });
    });
    public async Task PutAsync(string key, byte[] pdf, CancellationToken ct)
    {
        try
        {
            using var stream = new MemoryStream(pdf, false);
            await client.Value.PutObjectAsync(new PutObjectRequest { BucketName = options.Value.Bucket, Key = key, InputStream = stream, ContentType = "application/pdf", UseChunkEncoding = false }, ct);
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
