namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class LearningStorageOptions
{
    public string? Endpoint { get; set; }
    public string Region { get; set; } = "auto";
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string KeyPrefix { get; set; } = "photos/";
    public bool ForcePathStyle { get; set; } = true;
    public int TokenMinutes { get; set; } = 30;
}
