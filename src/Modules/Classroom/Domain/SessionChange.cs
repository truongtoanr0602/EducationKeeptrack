namespace EducationKeeptrack.Classroom.Domain;

// Durable history and outbox are saved in the same transaction as the source change.
public sealed class SessionChange
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid SessionId { get; private set; }
    public int SessionVersion { get; private set; }
    public string Kind { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public string SessionStatus { get; private set; } = "";
    public Guid? StudentId { get; private set; }
    public int? AttendanceVersion { get; private set; }
    public string? AttendanceStatus { get; private set; }
    public bool? Excused { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    private SessionChange() { }
    public SessionChange(ClassSession session, string kind, DateTimeOffset recordedAt, Attendance? attendance = null)
    {
        OwnerId = session.OwnerId; ClassId = session.ClassId; SessionId = session.Id; SessionVersion = session.Version;
        Kind = kind; RecordedAt = recordedAt; SessionStatus = session.Status;
        StudentId = attendance?.StudentId; AttendanceVersion = attendance?.Version; AttendanceStatus = attendance?.Status;
        Excused = attendance?.Excused; Reason = attendance is null ? session.Reason : attendance.Reason;
    }
    public void Acknowledge(DateTimeOffset now) => DispatchedAt = now;
}
