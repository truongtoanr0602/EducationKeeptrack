namespace EducationKeeptrack.Learning.Domain;

/// <summary>Request shape of one lesson; SkillIds are skills of the same curriculum.</summary>
public record LessonInput(int No, string? Topic, string? Title, string? Knowledge, string? Outcomes, string? SkillsText, string? Competencies, Guid[]? SkillIds, bool Archived, int Version);

/// <summary>
/// "Buổi chương trình": lesson N of the class curriculum, without a date. It is not a "Buổi học" (a dated class
/// meeting); the two are paired by order (see <see cref="LessonSchedule"/>). Lessons are never deleted, only archived.
/// </summary>
public sealed class CurriculumLesson
{
    public const int MaxSkills = 20;
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CurriculumId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public int No { get; private set; }
    public string Topic { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Knowledge { get; private set; } = "";
    public string Outcomes { get; private set; } = "";
    public string SkillsText { get; private set; } = "";
    public string Competencies { get; private set; } = "";
    public List<Guid> SkillIds { get; private set; } = [];
    public bool Archived { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private CurriculumLesson() { }
    internal CurriculumLesson(ClassCurriculum curriculum, LessonInput input, DateTimeOffset now)
    {
        CurriculumId = curriculum.Id; OwnerId = curriculum.OwnerId; ClassId = curriculum.ClassId; CreatedAt = UpdatedAt = now;
        Assign(input);
    }

    /// <summary>Applies the request; returns true when something changed.</summary>
    internal bool Apply(LessonInput input, DateTimeOffset now)
    {
        var skills = Distinct(input.SkillIds);
        if (input.No == No && Clean(input.Topic) == Topic && Clean(input.Title) == Title && Clean(input.Knowledge) == Knowledge
            && Clean(input.Outcomes) == Outcomes && Clean(input.SkillsText) == SkillsText && Clean(input.Competencies) == Competencies
            && skills.SequenceEqual(SkillIds) && input.Archived == Archived) return false;
        Assign(input);
        UpdatedAt = now;
        return true;
    }

    private void Assign(LessonInput input)
    {
        No = input.No; Topic = Clean(input.Topic); Title = Clean(input.Title); Knowledge = Clean(input.Knowledge);
        Outcomes = Clean(input.Outcomes); SkillsText = Clean(input.SkillsText); Competencies = Clean(input.Competencies);
        SkillIds = Distinct(input.SkillIds); Archived = input.Archived;
    }

    internal static Dictionary<string, string[]> Validate(LessonInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.No is < 1 or > 500) errors["no"] = ["Số buổi từ 1 đến 500."];
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 300) errors["title"] = ["Nhập nội dung buổi (tối đa 300 ký tự)."];
        if (input.Topic is { } topic && topic.Trim().Length > 200) errors["topic"] = ["Chủ đề tối đa 200 ký tự."];
        if (input.Knowledge is { } knowledge && knowledge.Trim().Length > 1000) errors["knowledge"] = ["Kiến thức tối đa 1000 ký tự."];
        if (input.Outcomes is { } outcomes && outcomes.Trim().Length > 1000) errors["outcomes"] = ["Chuẩn đầu ra tối đa 1000 ký tự."];
        if (input.SkillsText is { } text && text.Trim().Length > 1000) errors["skillsText"] = ["Mô tả kỹ năng tối đa 1000 ký tự."];
        if (input.Competencies is { } competencies && competencies.Trim().Length > 300) errors["competencies"] = ["Năng lực – thái độ tối đa 300 ký tự."];
        if (Distinct(input.SkillIds).Count > MaxSkills) errors["skillIds"] = [$"Một buổi gắn tối đa {MaxSkills} kỹ năng."];
        return errors;
    }

    private static List<Guid> Distinct(Guid[]? ids) => (ids ?? []).Distinct().ToList();
    private static string Clean(string? value) => value?.Trim() ?? "";
}

/// <summary>A teacher's manual pairing of one lesson with one dated class session; without it the pairing is by order.</summary>
public sealed class LessonSessionLink
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid SessionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private LessonSessionLink() { }
    public LessonSessionLink(string owner, Guid classId, Guid lessonId, Guid sessionId, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; LessonId = lessonId; SessionId = sessionId; CreatedAt = now;
    }
}

public sealed record ScheduledSession(Guid Id, DateOnly Date, TimeOnly StartTime, string Status);
public sealed record LessonSlot(CurriculumLesson Lesson, ScheduledSession? Session, bool Manual);

/// <summary>
/// Pairs lessons with dated sessions. Manual links win; the remaining live lessons (by number) take the remaining
/// sessions that are not cancelled (by date and start time) in order. A lesson whose session was held is taught.
/// </summary>
public static class LessonSchedule
{
    public static IReadOnlyList<LessonSlot> Pair(IEnumerable<CurriculumLesson> lessons, IEnumerable<ScheduledSession> sessions, IEnumerable<LessonSessionLink> links)
    {
        var live = lessons.Where(x => !x.Archived).OrderBy(x => x.No).ThenBy(x => x.CreatedAt).ToList();
        var usable = sessions.Where(x => x.Status != "cancelled").OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToList();
        var byId = usable.ToDictionary(x => x.Id);
        var manual = new Dictionary<Guid, ScheduledSession>();
        foreach (var link in links)
            if (byId.TryGetValue(link.SessionId, out var session) && live.Any(x => x.Id == link.LessonId) && manual.Values.All(x => x.Id != session.Id))
                manual[link.LessonId] = session;
        var free = new Queue<ScheduledSession>(usable.Where(x => manual.Values.All(m => m.Id != x.Id)));
        return live.Select(lesson => manual.TryGetValue(lesson.Id, out var session)
            ? new LessonSlot(lesson, session, true)
            : new LessonSlot(lesson, free.Count > 0 ? free.Dequeue() : null, false)).ToList();
    }

    /// <summary>Skills of lessons whose paired session has been held.</summary>
    public static HashSet<Guid> TaughtSkills(IEnumerable<LessonSlot> slots) =>
        slots.Where(x => x.Session?.Status == "held").SelectMany(x => x.Lesson.SkillIds).ToHashSet();
}
