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

/// <summary>Which model produced a draft and how it was set, recorded with every AI turn (BR-AI-13).</summary>
public sealed record PlanModelInfo(string Name, string Explainer, string ParamSource, BktParams Params);
public sealed record PlanDraftResult(PlanContent Content, PlanModelInfo Model, IReadOnlyList<DraftTrace> Trace);

public sealed class ReviewPlanService(
    IReviewPlanRepository plans, ICurriculumRepository curricula, IMaterialRepository materials, CompetencyService competency,
    IClassroomLearningSource roster, IClassroomSessionSource sessions, IAiQuota quota, IPlanExplainer explainer, TimeProvider clock)
{
    private sealed record Prepared(PlanContext Context, CompetencyProfile Profile);

    /// <summary>
    /// A class needs this many skill histories with two or more attempts before its own BKT parameters are fitted. Offline evaluation
    /// (docs/ml/danh-gia-mo-hinh.md) found no gain from fitting at any size tried and a loss in prioritisation when the data do not
    /// follow the model, so the default parameters stay in use until a class has a large history.
    /// </summary>
    public const int MinSequencesToFit = 100;
    public const string ModelName = "bkt-priority-v1";

    /// <summary>
    /// AI draft of the weekly plan (UC-AI-04). Order: check input, hold one planning turn, build the draft from the model,
    /// judge it with the same rules as a hand-made plan, then commit the turn. Anything invalid releases the turn and
    /// nothing is saved (BR-AI-14); the teacher receives the draft in the editor and saves it as usual.
    /// </summary>
    public async Task<LearningResult<PlanDraftResult>> DraftAsync(string owner, Guid classId, PlanDraftInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        if (!TryDate(input.WeekStart, out var week) || week.DayOfWeek != DayOfWeek.Monday)
            return new(null, 400, new() { ["weekStart"] = ["Tuần của kế hoạch phải bắt đầu vào thứ Hai (năm-tháng-ngày)."] });
        var prepared = await PrepareAsync(owner, classId, week, ct);
        if (prepared is null) return new(null, 404);
        var curriculum = await curricula.FindAsync(owner, classId, false, ct);
        if (curriculum is null || prepared.Profile.Rule is not { } rule)
            return new(null, 400, new() { ["curriculum"] = ["Cần thiết lập chương trình và kỹ năng của lớp trước khi nhờ AI lập kế hoạch."] });

        var planSessions = input.Sessions is { Length: > 0 } ? input.Sessions : await ClassSessionsAsync(owner, classId, week, ct);
        if (planSessions is null) return new(null, 404);
        if (planSessions.Length == 0)
            return new(null, 400, new() { ["sessions"] = ["Tuần này lớp chưa có buổi học. Chọn ngày và số phút của buổi ôn trước khi nhờ AI."] });

        var results = (await competency.ConfirmedAsync(owner, classId, ct)).ToLookup(x => (x.StudentId, x.SkillId));
        var skills = curriculum.GetEffectiveSkills().Where(x => x.IsTaught).ToDictionary(x => x.Id, x => new DraftSkill(x.Id, x.Code, x.Name));
        List<Attempt> AttemptsOf(Guid student, Guid skill) => results[(student, skill)]
            .Where(x => x.Result.Status == SkillResultStatus.Assessed && x.Result.Correct is not null && x.Result.TaskCount > 0)
            .OrderBy(x => x.Result.Date).ThenBy(x => x.Result.AssessmentId).Select(x => new Attempt(x.Result.TaskCount, x.Result.Correct!.Value)).ToList();
        var sequences = prepared.Context.Students.Keys.SelectMany(student => skills.Keys.Select(skill => (IReadOnlyList<Attempt>)AttemptsOf(student, skill)))
            .Where(x => x.Count >= 2).ToList();
        var fitted = sequences.Count >= MinSequencesToFit;
        var parameters = fitted ? KnowledgeTracing.Fit(sequences) : BktParams.Default;
        var model = new PlanModelInfo(ModelName, explainer.Name, fitted ? $"fitted:{sequences.Count}" : "default", parameters);

        var draftStudents = new List<DraftStudent>();
        foreach (var (studentId, name) in prepared.Context.Students.OrderBy(x => x.Value).ThenBy(x => x.Key))
        {
            var known = prepared.Profile.Students.FirstOrDefault(x => x.StudentId == studentId);
            var beliefs = new List<SkillBelief>();
            foreach (var skill in skills.Values)
            {
                var attempts = AttemptsOf(studentId, skill.Id);
                var last = results[(studentId, skill.Id)].Where(x => attempts.Count > 0 && x.Result.Status == SkillResultStatus.Assessed).Select(x => (DateOnly?)x.Result.Date).Max();
                var state = known?.Skills.FirstOrDefault(x => x.Skill.Id == skill.Id)?.Result.State ?? CompetencyState.Insufficient;
                beliefs.Add(new(skill.Id, KnowledgeTracing.Current(attempts, parameters), attempts.Count, last, state));
            }
            draftStudents.Add(new(studentId, name, beliefs));
        }
        var approved = (await materials.ListAsync(owner, classId, ct)).Where(x => x.Status == MaterialStatus.Approved && skills.ContainsKey(x.SkillId))
            .Select(x => new DraftMaterial(x.Id, x.SkillId, x.Title, x.Minutes, x.Difficulty)).ToList();
        var facts = new DraftFacts(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), planSessions, draftStudents, skills, approved, rule);
        var draft = PlanDrafter.Build(facts, explainer);
        if (draft.Content.Students is not { Length: > 0 })
            return new(null, 400, new() { ["goals"] = ["Chưa có kỹ năng nào cần ôn: mọi kỹ năng đã dạy đều đạt theo quy tắc."] });

        // The turn is held only once there is something to draft, so a refused request costs nothing.
        var key = string.IsNullOrWhiteSpace(input.RequestKey) ? $"plan-draft:{Guid.NewGuid():N}" : $"plan-draft:{classId:N}:{week:yyyyMMdd}:{input.RequestKey}";
        var reservation = await quota.ReserveAsync(owner, AiTaskKind.Planning, key, 1, ct);
        if (reservation.Outcome == AiReservationOutcome.Denied) return new(null, 403, new() { ["ai"] = [AiQuotaMessages.For(reservation.DeniedReason)] });
        try
        {
            var (content, errors) = PlanRules.Normalize(draft.Content, prepared.Context, true);
            if (errors is not null || content is null) throw new InvalidOperationException("The drafted plan broke the plan rules.");
            if (reservation.ReservationId is { } id && !await quota.CommitAsync(id, ct))
                return new(null, 403, new() { ["ai"] = ["Không thể trừ lượt AI do tài khoản bị hạn chế."] });
            return new(new(content, model, draft.Trace));
        }
        catch (Exception)
        {
            if (reservation.ReservationId is { } id) await quota.ReleaseAsync(id, ct);
            return new(null, 502, new() { ["ai"] = ["Không dựng được dự thảo hợp lệ. Lượt AI đã được hoàn trả; bạn có thể lập kế hoạch thủ công."] });
        }
    }

    private async Task<PlanSession[]?> ClassSessionsAsync(string owner, Guid classId, DateOnly week, CancellationToken ct)
    {
        var list = await sessions.ReadRangeAsync(owner, classId, week, week.AddDays(6), ct);
        return list?.Where(x => x.Status != "cancelled").OrderBy(x => x.Date).ThenBy(x => x.StartTime)
            .Select(x => new PlanSession(x.Date, Math.Clamp((int)(x.EndTime - x.StartTime).TotalMinutes, 5, 240), x.Id)).ToArray();
    }

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
