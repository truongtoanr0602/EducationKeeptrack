namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class ReportingOptions
{
    public string? Endpoint { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string Bucket { get; set; } = "ekt-reports";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool ForcePathStyle { get; set; } = true;
    public int PreviewMinutes { get; set; } = 15;
    public int DownloadMinutes { get; set; } = 5;
    public int RenderTimeoutSeconds { get; set; } = 60;
    public int RenderConcurrency { get; set; } = 2;
}
