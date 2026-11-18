namespace EducationKeeptrack.Learning.Domain;

// The request shapes follow the contract of ui/src/personalization-api.ts and Personalization.tsx.
public record CurriculumInput(string? Subject, string? Grade, string? Term, string? Textbook, int MasteryThreshold, int MinTasks, int RequiredAttempts, int Version);
/// <summary>Criteria, Source and Status are optional extensions; the screen never sends them, so a skill defaults to approved.</summary>
public record SkillInput(string? Code, string? Name, string? Strand, bool Taught, bool Archived, int Version, string? Criteria = null, string? Source = null, string? Status = null);

/// <summary>Result of a domain operation: 200 ok, 400 invalid, 404 missing, 409 stale version.</summary>
public readonly record struct CurriculumOutcome(int Status, Dictionary<string, string[]>? Errors = null)
{
    public static CurriculumOutcome Ok => new(200);
    public static CurriculumOutcome NotFound => new(404);
    public static CurriculumOutcome Conflict => new(409);
    public static CurriculumOutcome Invalid(string key, string message) => new(400, new() { [key] = [message] });
}

public static class SkillStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";
}

/// <summary>
/// Curriculum of one class: subject, term, textbook, the mastery rule and the skills.
/// Version is the optimistic-concurrency token of the whole aggregate and increases on every change,
/// so results and plans can cite the exact standards they were based on.
/// RuleVersion increases only when the mastery rule changes. Skills are never deleted; archiving
/// keeps what results and plans refer to.
/// </summary>
public sealed class ClassCurriculum
{
    public const int MaxSkills = 300;
    public const int DefaultThreshold = 80, DefaultMinTasks = 5, DefaultAttempts = 2;
    private readonly List<object> added = [];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string Subject { get; private set; } = "";
    public string Grade { get; private set; } = "";
    public string Term { get; private set; } = "";
    public string Textbook { get; private set; } = "";
    public int MasteryThreshold { get; private set; } = DefaultThreshold;
    public int MinTasks { get; private set; } = DefaultMinTasks;
    public int RequiredAttempts { get; private set; } = DefaultAttempts;
    public int RuleVersion { get; private set; } = 1;
    public int Version { get; private set; } = 1;
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<Skill> Skills { get; private set; } = [];
    private ClassCurriculum() { }
    public ClassCurriculum(string owner, Guid classId, CurriculumInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; UpdatedAt = now;
        Assign(input);
    }

    /// <summary>Entities created by the last operation; the caller must start tracking them explicitly.</summary>
    public IReadOnlyList<object> TakeAdded()
    {
        var result = added.ToArray();
        added.Clear();
        return result;
    }

    public static Dictionary<string, string[]> Validate(CurriculumInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Trim().Length > 80) errors["subject"] = ["Nhập môn học (tối đa 80 ký tự)."];
        if (string.IsNullOrWhiteSpace(input.Grade) || input.Grade.Trim().Length > 40) errors["grade"] = ["Nhập khối/lớp (tối đa 40 ký tự)."];
        if (string.IsNullOrWhiteSpace(input.Term) || input.Term.Trim().Length > 40) errors["term"] = ["Nhập học kỳ (tối đa 40 ký tự)."];
        if (string.IsNullOrWhiteSpace(input.Textbook) || input.Textbook.Trim().Length > 160) errors["textbook"] = ["Nhập bộ sách áp dụng (tối đa 160 ký tự)."];
        if (input.MasteryThreshold is < 1 or > 100) errors["masteryThreshold"] = ["Ngưỡng đạt từ 1 đến 100%."];
        if (input.MinTasks is < 1 or > 50) errors["minTasks"] = ["Số nhiệm vụ tối thiểu từ 1 đến 50."];
        if (input.RequiredAttempts is < 1 or > 5) errors["requiredAttempts"] = ["Số lần đánh giá cần có từ 1 đến 5."];
        return errors;
    }

    public CurriculumOutcome Configure(CurriculumInput input, DateTimeOffset now)
    {
        if (input.Version != Version) return CurriculumOutcome.Conflict;
        var errors = Validate(input);
        if (errors.Count > 0) return new(400, errors);
        var ruleChanged = input.MasteryThreshold != MasteryThreshold || input.MinTasks != MinTasks || input.RequiredAttempts != RequiredAttempts;
        var changed = ruleChanged || input.Subject!.Trim() != Subject || input.Grade!.Trim() != Grade || input.Term!.Trim() != Term || input.Textbook!.Trim() != Textbook;
        if (!changed) return CurriculumOutcome.Ok;
        Assign(input);
        if (ruleChanged) RuleVersion++;
        Bump(now);
        return CurriculumOutcome.Ok;
    }

    public CurriculumOutcome AddSkill(SkillInput input, string actor, DateTimeOffset now)
    {
        if (input.Version != Version) return CurriculumOutcome.Conflict;
        var errors = ValidateSkill(input);
        if (errors.Count > 0) return new(400, errors);
        if (Skills.Count >= MaxSkills) return CurriculumOutcome.Invalid("skill", $"Một lớp có tối đa {MaxSkills} kỹ năng.");
        if (!input.Archived && CodeTaken(input.Code!.Trim(), null)) return CurriculumOutcome.Invalid("code", "Mã kỹ năng đã được dùng trong lớp này.");
        var skill = new Skill(this, input, now, Today(now));
        Bump(now);
        added.Add(skill);
        added.Add(new SkillEvent(skill, Version, "created", actor, now, null));
        return CurriculumOutcome.Ok;
    }

    public CurriculumOutcome EditSkill(Guid id, SkillInput input, string actor, DateTimeOffset now)
    {
        var skill = Skills.SingleOrDefault(x => x.Id == id);
        if (skill is null) return CurriculumOutcome.NotFound;
        if (input.Version != Version) return CurriculumOutcome.Conflict;
        var errors = ValidateSkill(input);
        if (errors.Count > 0) return new(400, errors);
        var code = input.Code!.Trim();
        if (!input.Archived && CodeTaken(code, id)) return CurriculumOutcome.Invalid("code", "Mã kỹ năng đã được dùng trong lớp này.");
        var events = skill.Apply(input, Today(now), actor, now);
        if (events.Count == 0) return CurriculumOutcome.Ok;
        Bump(now);
        foreach (var action in events) added.Add(new SkillEvent(skill, Version, action, actor, now, null));
        return CurriculumOutcome.Ok;
    }

    /// <summary>Skills that count for results, profiles and plans: in use and approved.</summary>
    public IEnumerable<Skill> GetEffectiveSkills() => Skills.Where(x => !x.Archived && x.Status == SkillStatus.Approved);

    private void Assign(CurriculumInput input)
    {
        Subject = input.Subject!.Trim(); Grade = input.Grade!.Trim(); Term = input.Term!.Trim(); Textbook = input.Textbook!.Trim();
        MasteryThreshold = input.MasteryThreshold; MinTasks = input.MinTasks; RequiredAttempts = input.RequiredAttempts;
    }
    private void Bump(DateTimeOffset now) { Version++; UpdatedAt = now; }
    private static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    /// <summary>Two live skills of one class never share a code; archived skills free theirs.</summary>
    private bool CodeTaken(string code, Guid? except) =>
        Skills.Any(x => !x.Archived && x.Id != except && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string[]> ValidateSkill(SkillInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 20) errors["code"] = ["Nhập mã kỹ năng (tối đa 20 ký tự)."];
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 200) errors["name"] = ["Nhập tên kỹ năng (tối đa 200 ký tự)."];
        if (input.Strand is { } strand && strand.Trim().Length > 120) errors["strand"] = ["Mạch nội dung tối đa 120 ký tự."];
        if (input.Criteria is { } criteria && criteria.Trim().Length > 500) errors["criteria"] = ["Tiêu chí đạt tối đa 500 ký tự."];
        if (input.Source is { } source && source.Trim().Length > 200) errors["source"] = ["Nguồn tối đa 200 ký tự."];
        if (input.Status is { } status && status != SkillStatus.Draft && status != SkillStatus.Approved) errors["status"] = ["Trạng thái là draft hoặc approved."];
        return errors;
    }
}

public sealed class Skill
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CurriculumId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Strand { get; private set; } = "";
    public string? Criteria { get; private set; }
    public string? Source { get; private set; }
    public string Status { get; private set; } = SkillStatus.Approved;
    public bool Archived { get; private set; }
    public DateOnly? TaughtOn { get; private set; }
    public string? TaughtBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public List<SkillEvent> Events { get; private set; } = [];
    public bool IsTaught => TaughtOn is not null;
    private Skill() { }
    public Skill(ClassCurriculum curriculum, SkillInput input, DateTimeOffset now, DateOnly today)
    {
        CurriculumId = curriculum.Id; OwnerId = curriculum.OwnerId; ClassId = curriculum.ClassId; CreatedAt = UpdatedAt = now;
        Code = input.Code!.Trim(); Name = input.Name!.Trim(); Strand = input.Strand?.Trim() ?? "";
        Criteria = Clean(input.Criteria); Source = Clean(input.Source); Status = input.Status ?? SkillStatus.Approved; Archived = input.Archived;
        if (input.Taught) { TaughtOn = today; TaughtBy = curriculum.OwnerId; }
    }

    /// <summary>Applies the request; returns the history actions it produced (empty when nothing changed).</summary>
    internal List<string> Apply(SkillInput input, DateOnly today, string actor, DateTimeOffset now)
    {
        var actions = new List<string>();
        var code = input.Code!.Trim(); var name = input.Name!.Trim(); var strand = input.Strand?.Trim() ?? "";
        var criteria = Clean(input.Criteria ?? Criteria); var source = Clean(input.Source ?? Source); var status = input.Status ?? Status;
        if (code != Code || name != Name || strand != Strand || criteria != Criteria || source != Source || status != Status)
        {
            Code = code; Name = name; Strand = strand; Criteria = criteria; Source = source; Status = status;
            actions.Add("edited");
        }
        if (input.Taught != IsTaught)
        {
            TaughtOn = input.Taught ? today : null; TaughtBy = input.Taught ? actor : null;
            actions.Add(input.Taught ? "taught" : "untaught");
        }
        if (input.Archived != Archived)
        {
            Archived = input.Archived;
            actions.Add(input.Archived ? "archived" : "restored");
        }
        if (actions.Count > 0) UpdatedAt = now;
        return actions;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SkillEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SkillId { get; private set; }
    /// <summary>Curriculum version after this change; orders history even when timestamps coincide.</summary>
    public int CurriculumVersion { get; private set; }
    public string Action { get; private set; } = "";
    public string ActorId { get; private set; } = "";
    public DateTimeOffset At { get; private set; }
    public string? Detail { get; private set; }
    private SkillEvent() { }
    public SkillEvent(Skill skill, int curriculumVersion, string action, string actor, DateTimeOffset at, string? detail)
    {
        SkillId = skill.Id; CurriculumVersion = curriculumVersion; Action = action; ActorId = actor; At = at; Detail = detail;
    }
}
