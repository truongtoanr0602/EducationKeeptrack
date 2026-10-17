namespace EducationKeeptrack.Contracts;

public record ClassroomChange(Guid EventId, string OwnerId, Guid ClassId, Guid SessionId, int SessionVersion,
    string Kind, DateTimeOffset RecordedAt, string SessionStatus, Guid? StudentId, int? AttendanceVersion,
    string? AttendanceStatus, bool? Excused, string? Reason);

// Delivery is at least once. Consumers must deduplicate EventId and reject older source versions.
public interface IClassroomChangeHandler
{
    Task HandleAsync(ClassroomChange change, CancellationToken cancellationToken);
}
