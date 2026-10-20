using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Reporting.Domain;

public record ExportInput(Guid PreviewId, long DraftVersion, Guid IdempotencyKey);
public sealed class ReportExport
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly Month { get; private set; }
    public Guid PreviewId { get; private set; }
    public long DraftVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string State { get; private set; } = "pending";
    public string? ObjectKey { get; private set; }
    public string? Sha256 { get; private set; }
    public long ByteLength { get; private set; }
    public string ContentType { get; private set; } = "application/pdf";
    public string TemplateVersion { get; private set; } = "monthly-v1";
    public string WarningsJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    private ReportExport() { }
    public ReportExport(ReportScope scope, ExportInput input, DateTimeOffset now)
    {
        OwnerId = scope.OwnerId; ClassId = scope.ClassId; StudentId = scope.StudentId; Month = scope.Month; PreviewId = input.PreviewId;
        DraftVersion = input.DraftVersion; IdempotencyKey = input.IdempotencyKey; Fingerprint = ReportStamp.Of(new { scope, input }); CreatedAt = now;
    }
    public bool Matches(ReportScope scope, ExportInput input) => Fingerprint == ReportStamp.Of(new { scope, input });
    public void Complete(string key, string sha256, long length, string warnings, DateTimeOffset now)
    { ObjectKey = key; Sha256 = sha256; ByteLength = length; WarningsJson = warnings; CompletedAt = now; State = "ready"; }
    public void Fail() { if (State != "ready") State = "failed"; }
}
