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
public record AttendanceBatchItem(Guid StudentId, string Status, string? Reason, bool Excused, int Version)
{
    public AttendanceInput Input => new(Status, Reason, Excused, Version);
}
// One save for a whole session: every mark is checked before any is written.
public record AttendanceBatchInput(IReadOnlyList<AttendanceBatchItem>? Marks)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Marks is null || Marks.Count == 0) { errors["marks"] = ["Chưa có điểm danh nào để lưu."]; return errors; }
        if (Marks.Count > 500) errors["marks"] = ["Tối đa 500 học sinh mỗi lần lưu."];
        if (Marks.Select(x => x.StudentId).Distinct().Count() != Marks.Count) errors["marks"] = ["Mỗi học sinh chỉ xuất hiện một lần."];
        for (var i = 0; i < Marks.Count; i++)
            foreach (var (key, value) in Marks[i].Input.Validate()) errors[$"marks[{i}].{key}"] = value;
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
    // True when saving the input would leave the mark exactly as it is.
    public static bool Same(Attendance? mark, AttendanceInput input)
    {
        var status = mark?.Status ?? "unmarked";
        var excused = input.Status == "absent" && input.Excused;
        var reason = input.Status == "absent" && !string.IsNullOrWhiteSpace(input.Reason) ? input.Reason.Trim() : null;
        return status == input.Status && (mark?.Excused ?? false) == excused && mark?.Reason == reason;
    }
    public bool TryMark(AttendanceInput input)
    {
        if (input.Version != Version) return false;
        Status = input.Status; Excused = Status == "absent" && input.Excused;
        Reason = Status == "absent" && !string.IsNullOrWhiteSpace(input.Reason) ? input.Reason.Trim() : null;
        Version++; return true;
    }
}
