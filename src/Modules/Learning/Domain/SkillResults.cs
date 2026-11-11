namespace EducationKeeptrack.Learning.Domain;

// Request shapes follow ui/src/SkillResults.tsx.
public record SkillMapItem(Guid SkillId, int TaskCount);
public record SkillMapInput(SkillMapItem[]? Skills, int Version);
public record SkillResultInput(string? Status, int? Correct, string? Note, int Version, bool Confirm);

public static class SkillResultStatus
{
    public const string Unassessed = "unassessed", Assessed = "assessed", NotDone = "not_done", Unassigned = "unassigned";
    public static readonly string[] All = [Unassessed, Assessed, NotDone, Unassigned];
}

/// <summary>
/// The skills one assessment measures and the per-student, per-skill results. It is kept apart from the
/// two overall indicators of <see cref="Submission"/>: counts per skill are never derived from them.
/// Version is the concurrency token of the skill list. A skill that already has results is locked.
/// </summary>
public sealed class AssessmentSkillSet
{
    public const int MaxSkills = 30;
    private readonly List<object> added = [];
    private readonly List<object> removed = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AssessmentId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<AssessmentSkill> Skills { get; private set; } = [];
    public List<SkillResult> Results { get; private set; } = [];
    private AssessmentSkillSet() { }
    public AssessmentSkillSet(string owner, Guid classId, Guid assessmentId, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; AssessmentId = assessmentId; UpdatedAt = now;
    }

    /// <summary>Entities created by the last operation; the caller must start tracking them explicitly.</summary>
    public IReadOnlyList<object> TakeAdded() { var result = added.ToArray(); added.Clear(); return result; }
    public IReadOnlyList<object> TakeRemoved() { var result = removed.ToArray(); removed.Clear(); return result; }

    public bool IsLocked(Guid skillId) => Results.Any(x => x.SkillId == skillId && x.History.Count > 0);

    public CurriculumOutcome SetSkills(SkillMapInput input, IReadOnlyCollection<Skill> classSkills, IReadOnlyCollection<(Guid Id, string Name)> students, DateTimeOffset now)
    {
        if (input.Version != Version) return CurriculumOutcome.Conflict;
        var items = input.Skills ?? [];
        if (items.Any(x => x is null) || items.Length > MaxSkills) return CurriculumOutcome.Invalid("skills", $"Gắn tối đa {MaxSkills} kỹ năng cho một bài.");
        if (items.Select(x => x.SkillId).Distinct().Count() != items.Length) return CurriculumOutcome.Invalid("skills", "Mỗi kỹ năng chỉ được gắn một lần.");
        foreach (var item in items)
        {
            if (item.TaskCount is < 1 or > 100) return CurriculumOutcome.Invalid("skills", "Số nhiệm vụ mỗi kỹ năng từ 1 đến 100.");
            var skill = classSkills.FirstOrDefault(x => x.Id == item.SkillId);
            if (skill is null) return CurriculumOutcome.Invalid("skills", "Kỹ năng không thuộc chương trình của lớp.");
            var existing = Skills.FirstOrDefault(x => x.SkillId == item.SkillId);
            if (existing is null && (skill.Archived || skill.Status != SkillStatus.Approved))
                return CurriculumOutcome.Invalid("skills", "Chỉ gắn kỹ năng đang dùng và đã duyệt.");
            if (existing is not null && existing.TaskCount != item.TaskCount && IsLocked(item.SkillId))
                return CurriculumOutcome.Invalid("skills", "Kỹ năng đã có kết quả nên không đổi số nhiệm vụ.");
        }
        var dropped = Skills.Where(x => items.All(i => i.SkillId != x.SkillId)).ToList();
        if (dropped.Any(x => IsLocked(x.SkillId))) return CurriculumOutcome.Invalid("skills", "Kỹ năng đã có kết quả nên không thể bỏ khỏi bài.");

        var changed = false;
        foreach (var skill in dropped)
        {
            removed.Add(skill);
            removed.AddRange(Results.Where(x => x.SkillId == skill.SkillId));
            changed = true;
        }
        foreach (var item in items)
        {
            var existing = Skills.FirstOrDefault(x => x.SkillId == item.SkillId);
            if (existing is null)
            {
                added.Add(new AssessmentSkill(this, item.SkillId, item.TaskCount));
                foreach (var student in students) added.Add(new SkillResult(this, student.Id, student.Name, item.SkillId));
                changed = true;
            }
            else if (existing.TaskCount != item.TaskCount) { existing.SetTaskCount(item.TaskCount); changed = true; }
        }
        if (changed) { Version++; UpdatedAt = now; }
        return CurriculumOutcome.Ok;
    }

    public CurriculumOutcome RecordResult(Guid studentId, Guid skillId, SkillResultInput input, string actor, DateTimeOffset now)
    {
        var row = Results.SingleOrDefault(x => x.StudentId == studentId && x.SkillId == skillId);
        var mapped = Skills.SingleOrDefault(x => x.SkillId == skillId);
        if (row is null || mapped is null) return CurriculumOutcome.NotFound;
        if (input.Version != row.Version) return CurriculumOutcome.Conflict;
        var errors = new Dictionary<string, string[]>();
        var status = input.Status;
        if (status is null || !SkillResultStatus.All.Contains(status)) errors["status"] = ["Chọn trạng thái hợp lệ."];
        else if (status == SkillResultStatus.Assessed)
        {
            if (input.Correct is null) errors["correct"] = ["Nhập số câu đúng."];
            else if (input.Correct < 0 || input.Correct > mapped.TaskCount) errors["correct"] = [$"Số câu đúng từ 0 đến {mapped.TaskCount}."];
        }
        else if (input.Confirm && status == SkillResultStatus.Unassessed) errors["status"] = ["Chọn trạng thái đã đánh giá, không làm bài hoặc không giao trước khi xác nhận."];
        if (input.Note is { } note && note.Trim().Length > 500) errors["note"] = ["Ghi chú tối đa 500 ký tự."];
        if (errors.Count > 0) return new(400, errors);
        var revision = row.Record(status!, status == SkillResultStatus.Assessed ? input.Correct : null, input.Note, mapped.TaskCount, input.Confirm, actor, now);
        added.Add(revision);
        return CurriculumOutcome.Ok;
    }
}

public sealed class AssessmentSkill
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SetId { get; private set; }
    public Guid SkillId { get; private set; }
    public int TaskCount { get; private set; }
    private AssessmentSkill() { }
    public AssessmentSkill(AssessmentSkillSet set, Guid skillId, int taskCount) { SetId = set.Id; SkillId = skillId; TaskCount = taskCount; }
    internal void SetTaskCount(int taskCount) => TaskCount = taskCount;
}

/// <summary>
/// One student's result for one skill. Like a submission, the current fields hold the latest save
/// (draft or confirmed) while the final result is the latest confirmed revision; only that one is evidence.
/// </summary>
public sealed class SkillResult
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SetId { get; private set; }
    public Guid AssessmentId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public string StudentName { get; private set; } = "";
    public Guid SkillId { get; private set; }
    public string Status { get; private set; } = SkillResultStatus.Unassessed;
    public int? Correct { get; private set; }
    public string? Note { get; private set; }
    public int Version { get; private set; } = 1;
    public List<SkillRevision> History { get; private set; } = [];
    private SkillResult() { }
    public SkillResult(AssessmentSkillSet set, Guid studentId, string studentName, Guid skillId)
    {
        SetId = set.Id; AssessmentId = set.AssessmentId; OwnerId = set.OwnerId; ClassId = set.ClassId;
        StudentId = studentId; StudentName = studentName; SkillId = skillId;
    }

    public SkillRevision? GetFinalResult() => History.Where(x => x.Action == "confirmed").OrderByDescending(x => x.Version).FirstOrDefault();

    internal SkillRevision Record(string status, int? correct, string? note, int taskCount, bool confirm, string actor, DateTimeOffset now)
    {
        Version++;
        Status = status; Correct = correct; Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return new SkillRevision(this, taskCount, confirm ? "confirmed" : "edited", actor, now);
    }
}

public sealed class SkillRevision
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ResultId { get; private set; }
    public int Version { get; private set; }
    public string Status { get; private set; } = "";
    public int TaskCount { get; private set; }
    public int? Correct { get; private set; }
    public string? Note { get; private set; }
    public string Action { get; private set; } = "";
    public string ActorId { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    private SkillRevision() { }
    public SkillRevision(SkillResult result, int taskCount, string action, string actor, DateTimeOffset now)
    {
        ResultId = result.Id; Version = result.Version; Status = result.Status; TaskCount = taskCount;
        Correct = result.Correct; Note = result.Note; Action = action; ActorId = actor; RecordedAt = now;
    }
}
