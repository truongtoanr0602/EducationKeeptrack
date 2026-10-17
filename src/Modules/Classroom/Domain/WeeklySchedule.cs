namespace EducationKeeptrack.Classroom.Domain;

public record ScheduleSlotInput(int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public record ScheduleInput(int Version, DateOnly EffectiveFrom, ScheduleSlotInput[]? Slots)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (EffectiveFrom == default) errors["effectiveFrom"] = ["Vui lòng nhập ngày lịch bắt đầu áp dụng."];
        if (Slots is null || Slots.Length > 100) errors["slots"] = ["Vui lòng nhập lịch tuần, tối đa 100 khung giờ mỗi lần lưu."];
        else
        {
            if (Slots.Any(x => x is null || x.DayOfWeek is < 0 or > 6 || x.EndTime <= x.StartTime)) errors["slots"] = ["Ngày trong tuần hoặc giờ không hợp lệ; giờ kết thúc phải sau giờ bắt đầu trong cùng ngày."];
            else if (Slots.Any(x => Slots.Any(y => !ReferenceEquals(x, y) && x.DayOfWeek == y.DayOfWeek && x.StartTime < y.EndTime && y.StartTime < x.EndTime)))
                errors["slots"] = ["Các khung giờ cùng ngày không được trùng nhau."];
        }
        return errors;
    }
}

// Revisions are immutable; generated sessions copy their times, never reference mutable slots.
public sealed class WeeklySchedule
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public int Version { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public List<WeeklySlot> Slots { get; private set; } = [];
    private WeeklySchedule() { }
    public WeeklySchedule(string owner, Guid classId, ScheduleInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; Version = input.Version + 1; EffectiveFrom = input.EffectiveFrom; RecordedAt = now;
        Slots = input.Slots!.Select(x => new WeeklySlot(x)).ToList();
    }
}
public sealed class WeeklySlot
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    private WeeklySlot() { }
    public WeeklySlot(ScheduleSlotInput input) { DayOfWeek = input.DayOfWeek; StartTime = input.StartTime; EndTime = input.EndTime; }
}
