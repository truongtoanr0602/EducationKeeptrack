namespace EducationKeeptrack.Classroom.Domain;

public record AttendanceInput(string Status, string? Reason, bool Excused, int Version)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Status is not ("unmarked" or "present" or "absent")) errors["status"] = ["Trạng thái điểm danh không hợp lệ."];
        if (Excused && Status != "absent") errors["excused"] = ["Chỉ học sinh vắng mới có trạng thái nghỉ có phép."];
        if (Reason?.Length > 1000) errors["reason"] = ["Lý do vắng tối đa 1000 ký tự."];
        return errors;
    }
}
public sealed class Attendance
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid SessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public string Status { get; private set; } = "unmarked";
    public string? Reason { get; private set; }
    public bool Excused { get; private set; }
    public int Version { get; private set; }
    private Attendance() { }
    public Attendance(string owner, Guid sessionId, Guid studentId) { OwnerId = owner; SessionId = sessionId; StudentId = studentId; }
    public bool TryMark(AttendanceInput input)
    {
        if (input.Version != Version) return false;
        Status = input.Status; Excused = Status == "absent" && input.Excused;
        Reason = Status == "absent" && !string.IsNullOrWhiteSpace(input.Reason) ? input.Reason.Trim() : null;
        Version++; return true;
    }
}
