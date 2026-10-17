namespace EducationKeeptrack.Contracts;

public record LearningStudentSource(Guid Id, string Name);
public record LearningRosterSource(Guid ClassId, IReadOnlyList<LearningStudentSource> Students);

public interface IClassroomLearningSource
{
    Task<LearningRosterSource?> ReadRosterAsync(string owner, Guid classId, DateOnly date, CancellationToken ct);
}
