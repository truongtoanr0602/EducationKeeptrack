namespace EducationKeeptrack.Contracts;

public record TuitionChange(Guid EventId, string OwnerId, Guid ClassId, long Version, string Kind, DateTimeOffset RecordedAt);
// At-least-once delivery. Deduplicate EventId and track the class ledger version.
public interface ITuitionChangeHandler
{
    Task HandleAsync(TuitionChange change, CancellationToken ct);
}
