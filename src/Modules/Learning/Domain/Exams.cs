using System.Security.Cryptography;
using System.Text.Json;
namespace EducationKeeptrack.Learning.Domain;

public static class ExamStatus
{
    public const string Draft = "draft", Open = "open", Closed = "closed", Confirmed = "confirmed";
}

public static class AttemptStatus
{
    public const string None = "none", InProgress = "in_progress", Submitted = "submitted", AutoSubmitted = "auto_submitted";
    public static bool IsDone(string status) => status is Submitted or AutoSubmitted;
}

/// <summary>"Cấu trúc đề": how many questions of each level (in <see cref="QuestionLevel.All"/> order) and from which scope.</summary>
public sealed record ExamScope(int? LessonFrom, int? LessonTo, Guid[] SkillIds);
public record BlueprintInput(string? Name, int[]? Counts, int? LessonFrom, int? LessonTo, Guid[]? SkillIds);

public static class ExamRules
{
    public const int MaxQuestions = 40, MaxPerLevel = 20, MaxDuration = 180, MaxFailedCodes = 5;

    public static Dictionary<string, string[]> ValidateStructure(int[]? counts, int? lessonFrom, int? lessonTo, Guid[]? skillIds)
    {
        var errors = new Dictionary<string, string[]>();
        if (counts is null || counts.Length != QuestionLevel.All.Length || counts.Any(x => x is < 0 or > MaxPerLevel))
            errors["counts"] = [$"Nhập số câu cho 4 mức, mỗi mức từ 0 đến {MaxPerLevel}."];
        else if (counts.Sum() is < 1 or > MaxQuestions) errors["counts"] = [$"Một đề có từ 1 đến {MaxQuestions} câu."];
        var bySkills = skillIds is { Length: > 0 };
        var byLessons = lessonFrom is not null || lessonTo is not null;
        if (bySkills == byLessons) errors["scope"] = ["Chọn phạm vi theo buổi (từ buổi đến buổi) hoặc theo danh sách kỹ năng."];
        if (byLessons && (lessonFrom is null || lessonTo is null || lessonFrom < 1 || lessonTo < lessonFrom))
            errors["scope"] = ["Nhập từ buổi và đến buổi hợp lệ."];
        return errors;
    }
}

public sealed class ExamBlueprint
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string Name { get; private set; } = "";
    public List<int> Counts { get; private set; } = [];
    public int? LessonFrom { get; private set; }
    public int? LessonTo { get; private set; }
    public List<Guid> SkillIds { get; private set; } = [];
    public DateTimeOffset UpdatedAt { get; private set; }
    private ExamBlueprint() { }
    public ExamBlueprint(string owner, Guid classId, BlueprintInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; Name = input.Name!.Trim();
        Apply(input, now);
    }
    public void Apply(BlueprintInput input, DateTimeOffset now)
    {
        Counts = [.. input.Counts!]; LessonFrom = input.LessonFrom; LessonTo = input.LessonTo;
        SkillIds = (input.SkillIds ?? []).Distinct().ToList(); UpdatedAt = now;
    }
}

/// <summary>
/// "Đề kiểm tra" drawn from the question bank. Items keep a snapshot of each question so later edits of the bank never
/// change an exam already handed out. Opening it creates the matching assessment and one seat per student.
/// </summary>
public sealed class Exam
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string Title { get; private set; } = "";
    /// <summary>"common" for the whole class; personalised mini-tests will use their own kind with per-seat items.</summary>
    public string Kind { get; private set; } = "common";
    public string Status { get; private set; } = ExamStatus.Draft;
    public int? DurationMinutes { get; private set; }
    public bool ShowResults { get; private set; }
    public string PublicToken { get; private set; } = "";
    public Guid? AssessmentId { get; private set; }
    public List<int> Counts { get; private set; } = [];
    public int? LessonFrom { get; private set; }
    public int? LessonTo { get; private set; }
    public List<Guid> SkillIds { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public int Version { get; private set; } = 1;
    public List<ExamItem> Items { get; private set; } = [];
    public List<ExamSeat> Seats { get; private set; } = [];
    private readonly List<object> added = [];
    private Exam() { }
    public Exam(string owner, Guid classId, string title, int[] counts, ExamScope scope, int? duration, IEnumerable<QuestionSnapshot> questions, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; Title = title.Trim(); Counts = [.. counts]; LessonFrom = scope.LessonFrom; LessonTo = scope.LessonTo;
        SkillIds = [.. scope.SkillIds]; DurationMinutes = duration; CreatedAt = now; PublicToken = NewToken();
        var order = 1;
        foreach (var question in questions) Items.Add(new ExamItem(this, order++, question));
    }

    public IReadOnlyList<object> TakeAdded() { var result = added.ToArray(); added.Clear(); return result; }
    public ExamScope Scope => new(LessonFrom, LessonTo, [.. SkillIds]);

    public static Dictionary<string, string[]> ValidateHeader(string? title, int? duration)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160) errors["title"] = ["Nhập tên đề (tối đa 160 ký tự)."];
        if (duration is < 1 or > ExamRules.MaxDuration) errors["durationMinutes"] = [$"Thời lượng từ 1 đến {ExamRules.MaxDuration} phút, hoặc để trống."];
        return errors;
    }

    public CurriculumOutcome Swap(int order, QuestionSnapshot replacement, int version)
    {
        if (version != Version) return CurriculumOutcome.Conflict;
        if (Status != ExamStatus.Draft) return CurriculumOutcome.Invalid("exam", "Chỉ đổi câu khi đề chưa mở.");
        var item = Items.SingleOrDefault(x => x.Order == order);
        if (item is null) return CurriculumOutcome.NotFound;
        item.Replace(replacement);
        Version++;
        return CurriculumOutcome.Ok;
    }

    /// <summary>Opens the exam for the given students; each gets a distinct four-digit code.</summary>
    public CurriculumOutcome Open(Guid assessmentId, IEnumerable<(Guid Id, string Name)> students, int version, DateTimeOffset now)
    {
        if (version != Version) return CurriculumOutcome.Conflict;
        if (Status != ExamStatus.Draft) return CurriculumOutcome.Invalid("exam", "Đề đã được mở trước đó.");
        var used = new HashSet<string>();
        foreach (var student in students)
        {
            string code;
            do code = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4"); while (!used.Add(code));
            var seat = new ExamSeat(this, student.Id, student.Name, code);
            Seats.Add(seat);
            added.Add(seat);
        }
        if (Seats.Count == 0) return CurriculumOutcome.Invalid("exam", "Lớp chưa có học sinh đang học để làm bài.");
        AssessmentId = assessmentId; Status = ExamStatus.Open; OpenedAt = now; Version++;
        return CurriculumOutcome.Ok;
    }

    /// <summary>Closes the exam; attempts still running are submitted with what they hold.</summary>
    public CurriculumOutcome Close(int version, DateTimeOffset now)
    {
        if (version != Version) return CurriculumOutcome.Conflict;
        if (Status != ExamStatus.Open) return CurriculumOutcome.Invalid("exam", "Chỉ đóng được đề đang mở.");
        foreach (var seat in Seats.Where(x => x.AttemptStatus == AttemptStatus.InProgress)) seat.Submit(this, true, now);
        Status = ExamStatus.Closed; ClosedAt = now; Version++;
        return CurriculumOutcome.Ok;
    }

    public CurriculumOutcome SetShowResults(bool show, int version)
    {
        if (version != Version) return CurriculumOutcome.Conflict;
        if (show && Status is not (ExamStatus.Closed or ExamStatus.Confirmed)) return CurriculumOutcome.Invalid("exam", "Chỉ cho xem kết quả sau khi đóng bài.");
        if (ShowResults != show) { ShowResults = show; Version++; }
        return CurriculumOutcome.Ok;
    }

    public CurriculumOutcome MarkConfirmed(int version, DateTimeOffset now)
    {
        if (version != Version) return CurriculumOutcome.Conflict;
        if (Status != ExamStatus.Closed) return CurriculumOutcome.Invalid("exam", "Đóng bài trước khi xác nhận kết quả.");
        Status = ExamStatus.Confirmed; ConfirmedAt = now; Version++;
        return CurriculumOutcome.Ok;
    }

    /// <summary>Lets one student start again: clears the attempt and unlocks the code.</summary>
    public CurriculumOutcome ResetSeat(Guid studentId, int version)
    {
        if (version != Version) return CurriculumOutcome.Conflict;
        if (Status != ExamStatus.Open) return CurriculumOutcome.Invalid("exam", "Chỉ cho làm lại khi đề đang mở.");
        var seat = Seats.SingleOrDefault(x => x.StudentId == studentId);
        if (seat is null) return CurriculumOutcome.NotFound;
        seat.Reset();
        Version++;
        return CurriculumOutcome.Ok;
    }

    /// <summary>Items a seat answers: the common items plus, for personalised kinds later, the seat's own.</summary>
    public IReadOnlyList<ExamItem> ItemsFor(ExamSeat seat) => Items.Where(x => x.SeatId is null || x.SeatId == seat.Id).OrderBy(x => x.Order).ToList();

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

public sealed class ExamItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ExamId { get; private set; }
    public int Order { get; private set; }
    public Guid? SeatId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Level { get; private set; } = "";
    public Guid PrimarySkillId { get; private set; }
    public string SnapshotJson { get; private set; } = "{}";
    public QuestionSnapshot Snapshot => JsonSerializer.Deserialize<QuestionSnapshot>(SnapshotJson, Question.Json)!;
    private ExamItem() { }
    internal ExamItem(Exam exam, int order, QuestionSnapshot question) { ExamId = exam.Id; Order = order; Replace(question); }
    internal void Replace(QuestionSnapshot question)
    {
        QuestionId = question.QuestionId; Level = question.Level; PrimarySkillId = question.PrimarySkillId;
        SnapshotJson = JsonSerializer.Serialize(question, Question.Json);
    }
}

public enum JoinOutcome { Ok, WrongCode, Locked }

/// <summary>One student's place in an exam: the entry code and the single attempt ("Lượt làm bài").</summary>
public sealed class ExamSeat
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ExamId { get; private set; }
    public Guid StudentId { get; private set; }
    public string StudentName { get; private set; } = "";
    public string Code { get; private set; } = "";
    public int FailedTries { get; private set; }
    public bool Locked { get; private set; }
    public string AttemptStatus { get; private set; } = Domain.AttemptStatus.None;
    public int AttemptNo { get; private set; }
    /// <summary>Changes on every join and reset, so only the latest device keeps working.</summary>
    public string Nonce { get; private set; } = "";
    public int Seed { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string AnswersJson { get; private set; } = "{}";
    public int AnsweredCount { get; private set; }
    public int CorrectCount { get; private set; }
    public int Version { get; private set; } = 1;
    private ExamSeat() { }
    internal ExamSeat(Exam exam, Guid studentId, string name, string code) { ExamId = exam.Id; StudentId = studentId; StudentName = name; Code = code; }

    public Dictionary<int, string[]> Answers => JsonSerializer.Deserialize<Dictionary<int, string[]>>(AnswersJson, Question.Json) ?? [];

    public JoinOutcome Join(Exam exam, string? code, DateTimeOffset now)
    {
        if (Locked) return JoinOutcome.Locked;
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(code?.Trim() ?? ""), System.Text.Encoding.UTF8.GetBytes(Code)))
        {
            FailedTries++;
            if (FailedTries >= ExamRules.MaxFailedCodes) Locked = true;
            Version++;
            return Locked ? JoinOutcome.Locked : JoinOutcome.WrongCode;
        }
        FailedTries = 0;
        if (AttemptStatus == Domain.AttemptStatus.None && exam.Status == ExamStatus.Open)
        {
            AttemptStatus = Domain.AttemptStatus.InProgress; AttemptNo++; StartedAt = now;
            Deadline = exam.DurationMinutes is { } minutes ? now.AddMinutes(minutes) : null;
            Seed = RandomNumberGenerator.GetInt32(1, int.MaxValue);
        }
        Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Version++;
        return JoinOutcome.Ok;
    }

    /// <summary>Submits the attempt when its time is over; returns true when that happened.</summary>
    public bool ExpireIfDue(Exam exam, DateTimeOffset now)
    {
        if (AttemptStatus != Domain.AttemptStatus.InProgress || Deadline is not { } deadline || now < deadline) return false;
        Submit(exam, true, now);
        return true;
    }

    public CurriculumOutcome SaveAnswer(Exam exam, int order, string[]? values, DateTimeOffset now)
    {
        if (exam.Status != ExamStatus.Open || AttemptStatus != Domain.AttemptStatus.InProgress) return CurriculumOutcome.Invalid("attempt", "Bài làm đã nộp hoặc đã đóng.");
        if (ExpireIfDue(exam, now)) return CurriculumOutcome.Invalid("attempt", "Đã hết giờ làm bài. Bài đã được nộp.");
        var item = exam.ItemsFor(this).SingleOrDefault(x => x.Order == order);
        if (item is null) return CurriculumOutcome.NotFound;
        var snapshot = item.Snapshot;
        var clean = (values ?? []).Select(x => (x ?? "").Trim()).ToArray();
        if (clean.Length != snapshot.Blanks || clean.Any(x => x.Length > 100)) return CurriculumOutcome.Invalid("value", "Câu trả lời không hợp lệ.");
        var answers = Answers;
        if (clean.All(x => x.Length == 0)) answers.Remove(order); else answers[order] = clean;
        AnswersJson = JsonSerializer.Serialize(answers, Question.Json);
        AnsweredCount = answers.Count;
        Version++;
        return CurriculumOutcome.Ok;
    }

    public CurriculumOutcome SubmitByStudent(Exam exam, DateTimeOffset now)
    {
        if (AttemptStatus != Domain.AttemptStatus.InProgress) return CurriculumOutcome.Invalid("attempt", "Bài làm đã nộp.");
        Submit(exam, ExpireIfDueOnly(now), now);
        return CurriculumOutcome.Ok;
    }

    private bool ExpireIfDueOnly(DateTimeOffset now) => Deadline is { } deadline && now >= deadline;

    internal void Submit(Exam exam, bool automatic, DateTimeOffset now)
    {
        var answers = Answers;
        var items = exam.ItemsFor(this);
        CorrectCount = items.Count(x => answers.TryGetValue(x.Order, out var given) && x.Snapshot.IsCorrect(given));
        AnsweredCount = items.Count(x => answers.ContainsKey(x.Order));
        AttemptStatus = automatic ? Domain.AttemptStatus.AutoSubmitted : Domain.AttemptStatus.Submitted;
        SubmittedAt = now;
        Version++;
    }

    internal void Reset()
    {
        AttemptStatus = Domain.AttemptStatus.None; StartedAt = null; Deadline = null; SubmittedAt = null;
        AnswersJson = "{}"; AnsweredCount = 0; CorrectCount = 0; FailedTries = 0; Locked = false; Nonce = ""; Version++;
    }

    /// <summary>Tasks and right answers per primary skill of a finished attempt.</summary>
    public Dictionary<Guid, (int Tasks, int Correct)> SkillBreakdown(Exam exam)
    {
        var answers = Answers;
        return exam.ItemsFor(this).GroupBy(x => x.PrimarySkillId).ToDictionary(g => g.Key,
            g => (g.Count(), g.Count(x => answers.TryGetValue(x.Order, out var given) && x.Snapshot.IsCorrect(given))));
    }

    /// <summary>The order the student sees the items in, fixed by the attempt's seed.</summary>
    public IReadOnlyList<ExamItem> PresentedItems(Exam exam)
    {
        var items = exam.ItemsFor(this).ToList();
        var random = new Random(Seed);
        for (var i = items.Count - 1; i > 0; i--) { var j = random.Next(i + 1); (items[i], items[j]) = (items[j], items[i]); }
        return items;
    }

    /// <summary>Options (or tap buttons) of one item in the order this student sees them.</summary>
    public IReadOnlyList<QuestionOption> PresentedOptions(ExamItem item)
    {
        var options = item.Snapshot.Options.ToList();
        var random = new Random(unchecked(Seed * 31 + item.Order));
        for (var i = options.Count - 1; i > 0; i--) { var j = random.Next(i + 1); (options[i], options[j]) = (options[j], options[i]); }
        return options;
    }
}

public sealed record Shortfall(string Level, int Needed, int Available);

/// <summary>
/// Draws questions for an exam: for each level the skill drawn least so far goes next (ties broken at random), and a
/// question of that skill is picked at random, so the skills in scope are covered evenly. Never fills a gap from
/// another level: any level without enough questions is reported instead.
/// </summary>
public static class ExamGenerator
{
    public static (List<Question> Picked, List<Shortfall> Shortfalls) Draw(IReadOnlyList<Question> candidates, IReadOnlyList<int> counts, Random random)
    {
        var shortfalls = new List<Shortfall>();
        for (var i = 0; i < QuestionLevel.All.Length; i++)
        {
            var available = candidates.Count(x => x.Level == QuestionLevel.All[i]);
            if (available < counts[i]) shortfalls.Add(new(QuestionLevel.All[i], counts[i], available));
        }
        if (shortfalls.Count > 0) return ([], shortfalls);
        var picked = new List<Question>();
        var perSkill = new Dictionary<Guid, int>();
        for (var i = 0; i < QuestionLevel.All.Length; i++)
        {
            var pool = candidates.Where(x => x.Level == QuestionLevel.All[i]).GroupBy(x => x.PrimarySkillId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Code, StringComparer.Ordinal).ToList());
            for (var n = 0; n < counts[i]; n++)
            {
                var least = pool.Where(x => x.Value.Count > 0).Select(x => x.Key).GroupBy(x => perSkill.GetValueOrDefault(x))
                    .OrderBy(g => g.Key).First().OrderBy(x => x).ToList();
                var skill = least[random.Next(least.Count)];
                var list = pool[skill];
                var index = random.Next(list.Count);
                picked.Add(list[index]);
                list.RemoveAt(index);
                perSkill[skill] = perSkill.GetValueOrDefault(skill) + 1;
            }
        }
        return (picked, shortfalls);
    }
}

/// <summary>Turns counts into the two levels of an assessment result (25, 50, 75 or 100; nearest, ties upward).</summary>
public static class ResultLevels
{
    public static int Nearest(int part, int total)
    {
        if (part <= 0 || total <= 0) return 0;
        var percent = part * 100.0 / total;
        return new[] { 100, 75, 50, 25 }.OrderBy(x => Math.Abs(x - percent)).ThenByDescending(x => x).First();
    }
}
