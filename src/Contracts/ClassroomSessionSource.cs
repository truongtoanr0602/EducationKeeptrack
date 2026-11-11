namespace EducationKeeptrack.Contracts;

public record ClassSessionInfo(Guid Id, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Status, string Kind);

// Trusted module contract: the sessions of a class between two dates (inclusive), or null when the
// class does not belong to the teacher. Kept apart from IClassroomLearningSource so that contract stays small.
public interface IClassroomSessionSource
{
    Task<IReadOnlyList<ClassSessionInfo>?> ReadRangeAsync(string owner, Guid classId, DateOnly from, DateOnly to, CancellationToken ct);
}
