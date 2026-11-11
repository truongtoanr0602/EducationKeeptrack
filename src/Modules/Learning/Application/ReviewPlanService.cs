using System.Globalization;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public interface IReviewPlanRepository
{
    Task<IReadOnlyList<ReviewPlan>> ListAsync(string owner, Guid classId, CancellationToken ct);
    Task<ReviewPlan?> FindAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct);
    Task<bool> ExistsForWeekAsync(string owner, Guid classId, DateOnly weekStart, CancellationToken ct);
    void Add(IEnumerable<object> entities);
    Task<bool> SaveAsync(CancellationToken ct);
}

public sealed class ReviewPlanService(
    IReviewPlanRepository plans, ICurriculumRepository curricula, IMaterialRepository materials, CompetencyService competency,
    IClassroomLearningSource roster, IClassroomSessionSource sessions, TimeProvider clock)
{
    private sealed record Prepared(PlanContext Context, CompetencyProfile Profile);

    public async Task<LearningResult<IReadOnlyList<ReviewPlan>>> ListAsync(string owner, Guid classId, CancellationToken ct) =>
        await OwnsAsync(owner, classId, ct) ? new(await plans.ListAsync(owner, classId, ct)) : new(null, 404);

    public async Task<LearningResult<ReviewPlan>> GetAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        await OwnsAsync(owner, classId, ct) && await plans.FindAsync(owner, classId, id, false, ct) is { } plan ? new(plan) : new(null, 404);

    /// <summary>The sessions of the class in the week of the given date, for the plan screen.</summary>
    public async Task<LearningResult<IReadOnlyList<ClassSessionInfo>>> SessionsAsync(string owner, Guid classId, string? date, CancellationToken ct)
    {
        if (!TryDate(date, out var day)) return new(null, 400, new() { ["weekStart"] = ["Nhập ngày theo dạng năm-tháng-ngày."] });
        var monday = MondayOf(day);
        var list = await sessions.ReadRangeAsync(owner, classId, monday, monday.AddDays(6), ct);
        return list is null ? new(null, 404) : new(list);
    }

    public async Task<LearningResult<ReviewPlan>> CreateAsync(string owner, Guid classId, PlanCreateInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        if (!TryDate(input.WeekStart, out var week) || week.DayOfWeek != DayOfWeek.Monday)
            return new(null, 400, new() { ["weekStart"] = ["Tuần của kế hoạch phải bắt đầu vào thứ Hai (năm-tháng-ngày)."] });
        var prepared = await PrepareAsync(owner, classId, week, ct);
        if (prepared is null) return new(null, 404);
        var (content, errors) = PlanRules.Normalize(input.Content, prepared.Context, false);
        if (errors is not null) return new(null, 400, errors);
        if (await plans.ExistsForWeekAsync(owner, classId, week, ct)) return new(null, 409);
        var plan = new ReviewPlan(owner, classId, week, content!, Evidence(content!, prepared.Profile), clock.GetUtcNow());
        plans.Add([plan]);
        return await SaveAsync(owner, classId, plan.Id, ct);
    }

    public async Task<LearningResult<ReviewPlan>> SaveAsync(string owner, Guid classId, Guid id, PlanSaveInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var plan = await plans.FindAsync(owner, classId, id, true, ct);
        if (plan is null) return new(null, 404);
        if (input.Version != plan.Version) return new(null, 409);
        var prepared = await PrepareAsync(owner, classId, plan.WeekStart, ct);
        if (prepared is null) return new(null, 404);
        var (content, errors) = PlanRules.Normalize(input.Content, prepared.Context, false);
        if (errors is not null) return new(null, 400, errors);
        if (plan.Save(content!, Evidence(content!, prepared.Profile), owner, clock.GetUtcNow())) plans.Add(plan.TakeAdded());
        return await SaveAsync(owner, classId, id, ct);
    }

    public async Task<LearningResult<ReviewPlan>> ApproveAsync(string owner, Guid classId, Guid id, PlanVersionInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var plan = await plans.FindAsync(owner, classId, id, true, ct);
        if (plan is null) return new(null, 404);
        if (input.Version != plan.Version) return new(null, 409);
        if (plan.Status == PlanStatus.Approved) return new(null, 400, new() { ["plan"] = ["Kế hoạch này đã được duyệt."] });
        var prepared = await PrepareAsync(owner, classId, plan.WeekStart, ct);
        if (prepared is null) return new(null, 404);
        // Judge the saved content again with today's data: a material or skill may have changed since it was saved.
        var (_, errors) = PlanRules.Normalize(plan.GetContent(), prepared.Context, true);
        if (errors is not null) return new(null, 400, errors);
        plan.Approve(owner, clock.GetUtcNow());
        plans.Add(plan.TakeAdded());
        return await SaveAsync(owner, classId, id, ct);
    }

    private async Task<LearningResult<ReviewPlan>> SaveAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        await plans.SaveAsync(ct) ? new(await plans.FindAsync(owner, classId, id, false, ct)) : new(null, 409);

    private async Task<Prepared?> PrepareAsync(string owner, Guid classId, DateOnly week, CancellationToken ct)
    {
        var weekEnd = week.AddDays(6);
        var start = await roster.ReadRosterAsync(owner, classId, week, ct);
        var end = await roster.ReadRosterAsync(owner, classId, weekEnd, ct);
        var week7 = await sessions.ReadRangeAsync(owner, classId, week, weekEnd, ct);
        if (start is null || end is null || week7 is null) return null;
        var students = start.Students.Concat(end.Students).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.First().Name);
        var curriculum = await curricula.FindAsync(owner, classId, false, ct);
        var taught = (curriculum?.GetEffectiveSkills() ?? []).Where(x => x.IsTaught).Select(x => x.Id).ToHashSet();
        var approved = (await materials.ListAsync(owner, classId, ct)).Where(x => x.Status == MaterialStatus.Approved).ToDictionary(x => x.Id, x => x.SkillId);
        var held = week7.Where(x => x.Status != "cancelled").ToDictionary(x => x.Id, x => x.Date);
        var profile = (await competency.ProfileAsync(owner, classId, ct)).Value ?? new CompetencyProfile(false, null, 0, []);
        return new(new(week, students, taught, approved, held), profile);
    }

    /// <summary>The competency behind every goal, as it stood when the plan was saved.</summary>
    private static IReadOnlyList<PlanEvidence> Evidence(PlanContent content, CompetencyProfile profile)
    {
        var list = new List<PlanEvidence>();
        foreach (var student in content.Students ?? [])
        {
            var known = profile.Students.FirstOrDefault(x => x.StudentId == student.StudentId);
            foreach (var goal in student.Goals ?? [])
            {
                var skill = known?.Skills.FirstOrDefault(x => x.Skill.Id == goal.SkillId);
                if (skill is not null && profile.Rule is { } rule)
                    list.Add(new(student.StudentId, goal.SkillId, skill.Result.State, rule.Version, skill.Result.Evidence));
            }
        }
        return list;
    }

    private static bool TryDate(string? value, out DateOnly date) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private async Task<bool> OwnsAsync(string owner, Guid classId, CancellationToken ct) =>
        await roster.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is not null;
}
