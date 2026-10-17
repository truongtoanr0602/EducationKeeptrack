namespace EducationKeeptrack.Contracts;

public record BillingStudentSource(Guid Id, string Name);
public record BillingAttendanceSource(Guid StudentId, string Status, int Version);
public record BillingSessionSource(Guid Id, DateOnly Date, TimeOnly StartTime, string Status, string Kind, int Version, IReadOnlyList<BillingAttendanceSource> Attendance);
public record BillingClassSource(Guid Id, string Name, IReadOnlyList<BillingStudentSource> Students, IReadOnlyList<BillingSessionSource> Sessions);

// Trusted module contract. The adapter enforces owner/class scope and returns a consistent snapshot.
public interface IClassroomBillingSource
{
    Task<BillingClassSource?> ReadClassAsync(string owner, Guid classId, CancellationToken ct);
    Task<BillingSessionSource?> ReadSessionAsync(string owner, Guid classId, Guid sessionId, CancellationToken ct);
}
