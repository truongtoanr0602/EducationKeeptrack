using System.Text.Json;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Application;

namespace EducationKeeptrack.Reporting.Domain;

public sealed class ReportPreview
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly Month { get; private set; }
    public long DraftVersion { get; private set; }
    public string Payload { get; private set; } = "";
    public DateTimeOffset ExpiresAt { get; private set; }
    private ReportPreview() { }
    public ReportPreview(ReportScope scope, DraftView draft, ReportSourceSnapshot sources, DateTimeOffset now)
    {
        OwnerId = scope.OwnerId; ClassId = scope.ClassId; StudentId = scope.StudentId; Month = scope.Month; DraftVersion = draft.Version;
        Payload = JsonSerializer.Serialize(new PreviewPayload(draft, sources)); ExpiresAt = now.AddMinutes(15);
    }
    public PreviewPayload Content() => JsonSerializer.Deserialize<PreviewPayload>(Payload)!;
}
public record PreviewPayload(DraftView Draft, ReportSourceSnapshot Sources);
