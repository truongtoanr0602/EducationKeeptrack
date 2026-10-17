namespace EducationKeeptrack.Classroom.Domain;

public record SessionRange(DateOnly From, DateOnly To)
{
    public Dictionary<string, string[]> Validate() => From == default || To == default || To < From || To.DayNumber - From.DayNumber > 365
        ? new() { ["range"] = ["Khoảng ngày không hợp lệ; mỗi lần xử lý tối đa 366 ngày."] } : [];
}
public record GenerateSessionsInput(DateOnly From, DateOnly To, int ScheduleVersion);
public record SessionInput(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Kind, Guid? ReplacesSessionId)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Date == default || EndTime <= StartTime) errors["time"] = ["Vui lòng nhập ngày và khung giờ hợp lệ trong cùng ngày."];
        if (Kind is not ("regular" or "makeup") || (ReplacesSessionId is not null && Kind != "makeup")) errors["kind"] = ["Loại buổi học không hợp lệ; chỉ buổi học bù được liên kết buổi đã hủy."];
        return errors;
    }
}
public record SessionStatusInput(string Status, string? Reason, int Version);

public sealed class ClassSession
{
    public const string TimeZone = "Asia/Ho_Chi_Minh";
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public string Status { get; private set; } = "planned";
    public string Kind { get; private set; } = "regular";
    public Guid? ReplacesSessionId { get; private set; }
    public string? Reason { get; private set; }
    public int? ScheduleVersion { get; private set; }
    public int Version { get; private set; } = 1;
    private ClassSession() { }
    public ClassSession(string owner, Guid classId, DateOnly date, TimeOnly startTime, TimeOnly endTime, int? scheduleVersion, string kind = "regular", Guid? replacesSessionId = null)
    {
        OwnerId = owner; ClassId = classId; Date = date; StartTime = startTime; EndTime = endTime; ScheduleVersion = scheduleVersion;
        Kind = kind; ReplacesSessionId = replacesSessionId;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        StartsAtUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(startTime, DateTimeKind.Unspecified), zone));
        EndsAtUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(endTime, DateTimeKind.Unspecified), zone));
    }
    public bool Overlaps(DateOnly date, TimeOnly start, TimeOnly end) => Date == date && StartTime < end && start < EndTime;
    public void RecordAttendanceChange()
    {
        if (Status != "held") throw new InvalidOperationException("Chỉ điểm danh buổi đã diễn ra.");
        Version++;
    }
    public EnrollmentTransitionProblem? TryChangeStatus(SessionStatusInput input)
    {
        if (input.Version != Version || Status == "cancelled") return new(true, "Buổi học đã thay đổi hoặc đã hủy. Vui lòng tải lại; tạo buổi học bù nếu cần.");
        if (input.Status == Status) return new(true, "Buổi học đang ở trạng thái này.");
        var errors = new Dictionary<string, string[]>();
        if (input.Status is not ("planned" or "held" or "cancelled")) errors["status"] = ["Trạng thái buổi học không hợp lệ."];
        if (input.Reason?.Length > 1000 || input.Status == "cancelled" && string.IsNullOrWhiteSpace(input.Reason)) errors["reason"] = ["Khi hủy buổi, vui lòng nhập lý do, tối đa 1000 ký tự."];
        if (errors.Count > 0) return new(false, "Dữ liệu buổi học không hợp lệ.", errors);
        Status = input.Status; Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim(); Version++;
        return null;
    }
}
