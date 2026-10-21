namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class ReportingOptions
{
    public string? Endpoint { get; set; }
    public string Region { get; set; } = "auto";
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool ForcePathStyle { get; set; } = true;
    public int PreviewMinutes { get; set; } = 15;
    public int DownloadMinutes { get; set; } = 5;
    public int RenderTimeoutSeconds { get; set; } = 60;
    public int RenderConcurrency { get; set; } = 2;
}
