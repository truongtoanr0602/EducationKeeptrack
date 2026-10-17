namespace EducationKeeptrack.Classroom.Domain;

public sealed class Enrollment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string Status { get; private set; } = "active";
    public int Version { get; private set; } = 1;
    public List<EnrollmentChange> History { get; private set; } = [];
    private Enrollment() { }
    public Enrollment(string owner, Guid classId, Guid studentId, DateOnly startDate, DateTimeOffset recordedAt)
    {
        OwnerId = owner; ClassId = classId; StudentId = studentId; StartDate = startDate;
        History.Add(new EnrollmentChange(Id, Status, StartDate, recordedAt, Version));
    }
    public EnrollmentTransitionProblem? TryChange(EnrollmentStatusInput input, DateTimeOffset recordedAt)
    {
        if (input.Version != Version || Status == "stopped")
            return new(true, "Đăng ký đã thay đổi hoặc đã dừng học. Vui lòng tải lại danh sách; tạo đăng ký mới nếu học lại.");
        if (Status == input.Status) return new(true, "Đăng ký đang ở tình trạng này.");
        var errors = new Dictionary<string, string[]>();
        if (input.Status is not ("active" or "leave" or "stopped")) errors["status"] = ["Tình trạng theo học không hợp lệ."];
        if (input.EffectiveDate == default || input.EffectiveDate < History.Max(x => x.EffectiveDate))
            errors["effectiveDate"] = ["Ngày thay đổi không được trước ngày bắt đầu hoặc lần thay đổi gần nhất."];
        if (errors.Count > 0) return new(false, "Dữ liệu tình trạng không hợp lệ.", errors);
        Status = input.Status;
        EndDate = Status == "stopped" ? input.EffectiveDate : null;
        Version++;
        History.Add(new EnrollmentChange(Id, Status, input.EffectiveDate, recordedAt, Version));
        return null;
    }
}

public record EnrollmentInput(Guid StudentId, DateOnly StartDate);
public record EnrollmentStatusInput(string Status, DateOnly EffectiveDate, int Version);
public record EnrollmentTransitionProblem(bool IsConflict, string Message, Dictionary<string, string[]>? Errors = null);

public sealed class EnrollmentChange
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid EnrollmentId { get; private set; }
    public string Status { get; private set; } = "";
    public DateOnly EffectiveDate { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public int Version { get; private set; }
    private EnrollmentChange() { }
    public EnrollmentChange(Guid enrollmentId, string status, DateOnly effectiveDate, DateTimeOffset recordedAt, int version)
    {
        EnrollmentId = enrollmentId; Status = status; EffectiveDate = effectiveDate; RecordedAt = recordedAt; Version = version;
    }
}
