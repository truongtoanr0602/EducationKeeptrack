namespace EducationKeeptrack.Contracts;

public record LearningResultChange(Guid EventId, string OwnerId, Guid ClassId, Guid AssessmentId, Guid StudentId,
    int Version, string Status, int? Completion, int? Correctness, bool IncludedInStatistics, DateTimeOffset RecordedAt);

// At-least-once notifications for confirmed results only. Consumers deduplicate EventId.
public interface ILearningResultChangeHandler
{
    Task HandleAsync(LearningResultChange change, CancellationToken ct);
}
