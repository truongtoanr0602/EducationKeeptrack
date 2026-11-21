using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public record LessonLinkInput(Guid? SessionId);
public sealed record LessonPlan(ClassCurriculum? Curriculum, IReadOnlyList<LessonSlot> Slots, IReadOnlySet<Guid> TaughtSkills, IReadOnlyList<ScheduledSession> Sessions);

/// <summary>"Buổi chương trình": the lessons of the class curriculum and their pairing with dated class sessions.</summary>
public sealed class LessonService(IQuestionBankRepository repository, IClassroomSessionSource sessions, IClassroomLearningSource classroom, TimeProvider clock)
{
    private static readonly DateOnly From = new(2000, 1, 1), To = new(2100, 12, 31);

    public async Task<LearningResult<LessonPlan>> PlanAsync(string owner, Guid classId, CancellationToken ct)
    {
        var list = await sessions.ReadRangeAsync(owner, classId, From, To, ct);
        if (list is null) return new(null, 404);
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        return new(await BuildAsync(owner, classId, curriculum, list, ct));
    }

    /// <summary>
    /// Skills counted as taught: marked by the teacher, or listed by a lesson whose paired session was held.
    /// Competency and review plans use this instead of the teacher's mark alone.
    /// </summary>
    public async Task<IReadOnlySet<Guid>> TaughtSkillsAsync(string owner, Guid classId, ClassCurriculum? curriculum, CancellationToken ct)
    {
        if (curriculum is null) return new HashSet<Guid>();
        var list = await sessions.ReadRangeAsync(owner, classId, From, To, ct) ?? [];
        return (await BuildAsync(owner, classId, curriculum, list, ct)).TaughtSkills;
    }

    public Task<LearningResult<LessonPlan>> AddAsync(string owner, Guid classId, LessonInput input, CancellationToken ct) =>
        MutateAsync(owner, classId, (c, now) => c.AddLesson(input, now), ct);
    public Task<LearningResult<LessonPlan>> EditAsync(string owner, Guid classId, Guid lessonId, LessonInput input, CancellationToken ct) =>
        MutateAsync(owner, classId, (c, now) => c.EditLesson(lessonId, input, now), ct);

    /// <summary>Pins a lesson to one session, or (null) returns it to pairing by order.</summary>
    public async Task<LearningResult<LessonPlan>> LinkAsync(string owner, Guid classId, Guid lessonId, LessonLinkInput input, CancellationToken ct)
    {
        var list = await sessions.ReadRangeAsync(owner, classId, From, To, ct);
        if (list is null) return new(null, 404);
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        if (curriculum?.Lessons.SingleOrDefault(x => x.Id == lessonId && !x.Archived) is null) return new(null, 404);
        var links = await repository.ListLinksAsync(owner, classId, ct);
        foreach (var existing in links.Where(x => x.LessonId == lessonId || x.SessionId == input.SessionId)) repository.Remove(existing);
        if (input.SessionId is { } sessionId)
        {
            var session = list.FirstOrDefault(x => x.Id == sessionId);
            if (session is null || session.Status == "cancelled") return new(null, 400, new() { ["sessionId"] = ["Chọn một buổi học của lớp chưa bị hủy."] });
            repository.Add([new LessonSessionLink(owner, classId, lessonId, sessionId, clock.GetUtcNow())]);
        }
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return await PlanAsync(owner, classId, ct);
    }

    private async Task<LearningResult<LessonPlan>> MutateAsync(string owner, Guid classId, Func<ClassCurriculum, DateTimeOffset, CurriculumOutcome> operation, CancellationToken ct)
    {
        if (await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is null) return new(null, 404);
        var curriculum = await repository.FindCurriculumAsync(owner, classId, true, ct);
        if (curriculum is null) return new(null, 400, new() { ["curriculum"] = ["Cấu hình môn, học kỳ và bộ sách của lớp trước khi thêm buổi."] });
        var outcome = operation(curriculum, clock.GetUtcNow());
        if (outcome.Status != 200) return new(null, outcome.Status, outcome.Errors);
        repository.Add(curriculum.TakeAdded());
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return await PlanAsync(owner, classId, ct);
    }

    private async Task<LessonPlan> BuildAsync(string owner, Guid classId, ClassCurriculum? curriculum, IReadOnlyList<ClassSessionInfo> list, CancellationToken ct)
    {
        var scheduled = list.Select(x => new ScheduledSession(x.Id, x.Date, x.StartTime, x.Status)).ToList();
        if (curriculum is null) return new(null, [], new HashSet<Guid>(), scheduled);
        var links = await repository.ListLinksAsync(owner, classId, ct);
        var slots = LessonSchedule.Pair(curriculum.Lessons, scheduled, links);
        var taught = LessonSchedule.TaughtSkills(slots);
        taught.UnionWith(curriculum.Skills.Where(x => x.IsTaught).Select(x => x.Id));
        return new(curriculum, slots, taught, scheduled);
    }
}
