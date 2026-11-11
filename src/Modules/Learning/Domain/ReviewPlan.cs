using System.Text.Json;
namespace EducationKeeptrack.Learning.Domain;

// Shapes follow ui/src/personalization-api.ts (PlanContent) and ui/src/ReviewPlans.tsx.
public sealed record PlanSession(DateOnly Date, int Minutes, Guid? SessionId);
public sealed record PlanGoal(Guid SkillId, string? Reason, string? Recheck);
public sealed record PlanStudent(Guid StudentId, string? StudentName, PlanGoal[]? Goals);
public sealed record PlanActivity(int Session, Guid[]? StudentIds, Guid? SkillId, Guid? MaterialId, int Minutes, string? Description);
public sealed record PlanContent(PlanSession[]? Sessions, PlanStudent[]? Students, PlanActivity[]? Activities, string? Note);
public sealed record PlanCreateInput(string? WeekStart, PlanContent? Content);
public sealed record PlanSaveInput(PlanContent? Content, int Version);
public sealed record PlanVersionInput(int Version);

/// <summary>What the rules need to know about the class to judge a plan.</summary>
public sealed record PlanContext(
    DateOnly WeekStart,
    IReadOnlyDictionary<Guid, string> Students,
    IReadOnlySet<Guid> TaughtSkills,
    IReadOnlyDictionary<Guid, Guid> ApprovedMaterials,
    IReadOnlyDictionary<Guid, DateOnly> ClassSessions);

/// <summary>The competency behind one goal at the moment the plan was saved.</summary>
public sealed record PlanEvidence(Guid StudentId, Guid SkillId, string State, int RuleVersion, IReadOnlyList<EvidenceItem> Evidence);

public static class PlanStatus
{
    public const string Draft = "draft", Approved = "approved";
}

public static class PlanJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

/// <summary>
/// Rules of a weekly review plan: at most two priority skills per student, only skills already taught,
/// materials only when approved and of the same skill, and no student's activities beyond the session's
/// minutes. Approval is stricter: every goal needs its reason and re-check, every activity a description.
/// </summary>
public static class PlanRules
{
    public const int MaxGoals = 2, MaxSessions = 7, MaxActivities = 100;

    public static (PlanContent? Content, Dictionary<string, string[]>? Errors) Normalize(PlanContent? input, PlanContext context, bool forApproval)
    {
        if (input is null) return (null, new() { ["content"] = ["Thiếu nội dung kế hoạch."] });
        var errors = new Dictionary<string, List<string>>();
        void Fail(string key, string message) { if (!errors.TryGetValue(key, out var list)) errors[key] = list = []; if (!list.Contains(message)) list.Add(message); }

        var weekEnd = context.WeekStart.AddDays(6);
        var sessions = input.Sessions ?? [];
        if (sessions.Length is < 1 or > MaxSessions || sessions.Any(x => x is null)) { Fail("sessions", $"Kế hoạch cần từ 1 đến {MaxSessions} buổi ôn."); sessions = []; }
        var usedSessions = new HashSet<Guid>();
        foreach (var session in sessions)
        {
            if (session.Date < context.WeekStart || session.Date > weekEnd) Fail("sessions", "Ngày buổi ôn phải nằm trong tuần của kế hoạch.");
            if (session.Minutes is < 5 or > 240) Fail("sessions", "Quỹ phút mỗi buổi từ 5 đến 240.");
            if (session.SessionId is { } id)
            {
                if (!context.ClassSessions.TryGetValue(id, out var date)) Fail("sessions", "Buổi học của lớp không tồn tại, đã hủy hoặc không thuộc tuần này.");
                else if (date != session.Date) Fail("sessions", "Ngày buổi ôn phải trùng ngày của buổi học đã chọn.");
                if (!usedSessions.Add(id)) Fail("sessions", "Mỗi buổi học của lớp chỉ được chọn một lần.");
            }
        }

        var students = new List<PlanStudent>();
        var goalsByStudent = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var student in input.Students ?? [])
        {
            if (student is null) { Fail("students", "Dữ liệu học sinh không hợp lệ."); continue; }
            if (!context.Students.TryGetValue(student.StudentId, out var name)) { Fail("students", "Học sinh không thuộc lớp trong tuần này."); continue; }
            if (goalsByStudent.ContainsKey(student.StudentId)) { Fail("students", "Mỗi học sinh chỉ xuất hiện một lần."); continue; }
            var goals = new List<PlanGoal>();
            if ((student.Goals ?? []).Length > MaxGoals) Fail("goals", $"Mỗi học sinh tối đa {MaxGoals} kỹ năng ưu tiên.");
            foreach (var goal in student.Goals ?? [])
            {
                if (goal is null) { Fail("goals", "Dữ liệu mục tiêu không hợp lệ."); continue; }
                if (!context.TaughtSkills.Contains(goal.SkillId)) Fail("goals", "Chỉ chọn kỹ năng đang dùng và đã dạy.");
                if (goals.Any(x => x.SkillId == goal.SkillId)) Fail("goals", "Một học sinh không chọn trùng kỹ năng.");
                var reason = Clean(goal.Reason); var recheck = Clean(goal.Recheck);
                if (reason?.Length > 500) Fail("goals", "Lý do tối đa 500 ký tự.");
                if (recheck?.Length > 300) Fail("goals", "Cách kiểm tra lại tối đa 300 ký tự.");
                if (forApproval && (reason is null || recheck is null)) Fail("goals", "Mỗi kỹ năng ưu tiên cần có lý do kèm bằng chứng và cách kiểm tra lại.");
                goals.Add(new(goal.SkillId, reason, recheck));
            }
            goalsByStudent[student.StudentId] = goals.Select(x => x.SkillId).ToHashSet();
            students.Add(new(student.StudentId, name, goals.ToArray()));
        }
        if (forApproval && students.All(x => x.Goals!.Length == 0)) Fail("goals", "Cần ít nhất một học sinh có kỹ năng ưu tiên trước khi duyệt.");

        var activities = new List<PlanActivity>();
        var load = new Dictionary<(int Session, Guid Student), int>();
        if ((input.Activities ?? []).Length > MaxActivities) Fail("activities", $"Tối đa {MaxActivities} hoạt động.");
        foreach (var activity in input.Activities ?? [])
        {
            if (activity is null) { Fail("activities", "Dữ liệu hoạt động không hợp lệ."); continue; }
            var participants = (activity.StudentIds ?? []).Distinct().ToArray();
            if (activity.Session < 0 || activity.Session >= sessions.Length) Fail("activities", "Hoạt động phải thuộc một buổi ôn của kế hoạch.");
            if (participants.Length == 0 || participants.Any(x => !goalsByStudent.ContainsKey(x))) Fail("activities", "Hoạt động cần ít nhất một học sinh và chỉ gồm học sinh có trong kế hoạch.");
            if (activity.Minutes is < 1 or > 240) Fail("activities", "Thời lượng hoạt động từ 1 đến 240 phút.");
            var description = Clean(activity.Description);
            if (description?.Length > 1000) Fail("activities", "Mô tả hoạt động tối đa 1000 ký tự.");
            if (forApproval && description is null) Fail("activities", "Mỗi hoạt động cần có mô tả.");
            if (activity.SkillId is { } skill && participants.Any(x => !goalsByStudent.TryGetValue(x, out var goals) || !goals.Contains(skill)))
                Fail("activities", "Kỹ năng của hoạt động phải là kỹ năng ưu tiên của mọi học sinh tham gia.");
            if (activity.MaterialId is { } material)
            {
                if (activity.SkillId is null) Fail("activities", "Chọn kỹ năng trước khi chọn học liệu.");
                else if (!context.ApprovedMaterials.TryGetValue(material, out var materialSkill) || materialSkill != activity.SkillId)
                    Fail("activities", "Học liệu phải đã duyệt và đúng kỹ năng của hoạt động.");
            }
            foreach (var participant in participants) load[(activity.Session, participant)] = load.GetValueOrDefault((activity.Session, participant)) + Math.Max(activity.Minutes, 0);
            activities.Add(new(activity.Session, participants, activity.SkillId, activity.MaterialId, activity.Minutes, description));
        }
        foreach (var ((session, student), minutes) in load)
            if (session >= 0 && session < sessions.Length && minutes > sessions[session].Minutes)
                Fail("minutes", $"{context.Students.GetValueOrDefault(student, "Học sinh")} vượt quỹ {sessions[session].Minutes} phút của buổi {session + 1}.");

        var note = Clean(input.Note);
        if (note?.Length > 1000) Fail("note", "Ghi chú tối đa 1000 ký tự.");
        if (errors.Count > 0) return (null, errors.ToDictionary(x => x.Key, x => x.Value.ToArray()));
        return (new PlanContent(sessions, students.ToArray(), activities.ToArray(), note), null);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// The review plan of one class for one week. Every change makes a new version (a plan version is a
/// revision counter): saving, approving and later recording what was done. Approving freezes the
/// approved content; editing afterwards makes a new draft but the approved content stays until re-approved.
/// </summary>
public sealed class ReviewPlan
{
    private readonly List<object> added = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public DateOnly WeekStart { get; private set; }
    public string Status { get; private set; } = PlanStatus.Draft;
    public int Version { get; private set; }
    public int? ApprovedVersion { get; private set; }
    /// <summary>Optimistic-concurrency token; bumped by every write.</summary>
    public int Stamp { get; private set; }
    public string ContentJson { get; private set; } = "{}";
    public string? ApprovedContentJson { get; private set; }
    public string EvidenceJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<PlanRevision> Revisions { get; private set; } = [];
    private ReviewPlan() { }
    public ReviewPlan(string owner, Guid classId, DateOnly weekStart, PlanContent content, IReadOnlyList<PlanEvidence> evidence, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; WeekStart = weekStart; CreatedAt = now;
        Record("created", content, evidence, owner, now);
    }

    public PlanContent GetContent() => PlanJson.Read<PlanContent>(ContentJson);
    public PlanContent? GetApprovedContent() => ApprovedContentJson is null ? null : PlanJson.Read<PlanContent>(ApprovedContentJson);
    public IReadOnlyList<PlanEvidence> GetEvidence() => PlanJson.Read<List<PlanEvidence>>(EvidenceJson);
    public IReadOnlyList<object> TakeAdded() { var result = added.ToArray(); added.Clear(); return result; }

    /// <summary>Saves new content as a new version; returns false when nothing changed.</summary>
    public bool Save(PlanContent content, IReadOnlyList<PlanEvidence> evidence, string actor, DateTimeOffset now)
    {
        if (PlanJson.Write(content) == ContentJson) return false;
        Record("saved", content, evidence, actor, now);
        return true;
    }

    public void Approve(string actor, DateTimeOffset now)
    {
        Version++; Stamp++; UpdatedAt = now;
        Status = PlanStatus.Approved; ApprovedVersion = Version; ApprovedContentJson = ContentJson;
        added.Add(new PlanRevision(this, "approved", actor, now));
    }

    private void Record(string action, PlanContent content, IReadOnlyList<PlanEvidence> evidence, string actor, DateTimeOffset now)
    {
        Version++; Stamp++; UpdatedAt = now;
        Status = PlanStatus.Draft; ContentJson = PlanJson.Write(content); EvidenceJson = PlanJson.Write(evidence);
        var revision = new PlanRevision(this, action, actor, now);
        if (action == "created") Revisions.Add(revision); else added.Add(revision);
    }
}

/// <summary>One version of a plan with the content it had, so older versions stay readable.</summary>
public sealed class PlanRevision
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlanId { get; private set; }
    public int Version { get; private set; }
    public string Action { get; private set; } = "";
    public string Status { get; private set; } = "";
    public string ActorId { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public string ContentJson { get; private set; } = "{}";
    private PlanRevision() { }
    public PlanRevision(ReviewPlan plan, string action, string actor, DateTimeOffset now)
    {
        PlanId = plan.Id; Version = plan.Version; Action = action; Status = plan.Status; ActorId = actor; RecordedAt = now; ContentJson = plan.ContentJson;
    }
}
