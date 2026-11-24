using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public record ExamPreviewInput(int[]? Counts, int? LessonFrom, int? LessonTo, Guid[]? SkillIds, int? Seed);
public record ExamCreateInput(string? Title, int[]? Counts, int? LessonFrom, int? LessonTo, Guid[]? SkillIds, int? DurationMinutes, Guid[]? QuestionIds);
public record ExamVersionInput(int Version);
public record ExamSwapInput(int Order, int Version);
public record ExamShowInput(bool Show, int Version);
public record GradeInput(decimal? Points, string? Comment);
public sealed record GradedSeat(ExamSeat Seat, decimal MaxScore);

public sealed record ExamPreview(IReadOnlyList<Question> Questions, IReadOnlyList<Skill> Skills, IReadOnlyList<QuestionImage> Images);
public sealed record ExamDetail(Exam Exam, IReadOnlyList<Skill> Skills, IReadOnlyList<QuestionImage> Images);

/// <summary>Builds exams from the question bank by structure and runs them from draft to confirmed results.</summary>
public sealed class ExamService(IQuestionBankRepository repository, IClassroomLearningSource classroom, TimeProvider clock)
{
    public async Task<LearningResult<IReadOnlyList<Exam>>> ListAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        return new(await repository.ListExamsAsync(owner, classId, ct));
    }

    public async Task<LearningResult<IReadOnlyList<ExamBlueprint>>> BlueprintsAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        return new(await repository.ListBlueprintsAsync(owner, classId, ct));
    }

    /// <summary>Saves a structure under its name; saving the same name again replaces it.</summary>
    public async Task<LearningResult<IReadOnlyList<ExamBlueprint>>> SaveBlueprintAsync(string owner, Guid classId, BlueprintInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var errors = ExamRules.ValidateStructure(input.Counts, input.LessonFrom, input.LessonTo, input.SkillIds);
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120) errors["name"] = ["Nhập tên cấu trúc đề (tối đa 120 ký tự)."];
        if (errors.Count > 0) return new(null, 400, errors);
        var list = await repository.ListBlueprintsAsync(owner, classId, ct);
        var existing = list.FirstOrDefault(x => string.Equals(x.Name, input.Name!.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is null) repository.Add([new ExamBlueprint(owner, classId, input, clock.GetUtcNow())]);
        else existing.Apply(input, clock.GetUtcNow());
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return new(await repository.ListBlueprintsAsync(owner, classId, ct));
    }

    /// <summary>Draws a set of questions without saving anything, so the teacher can look before creating the exam.</summary>
    public async Task<LearningResult<ExamPreview>> PreviewAsync(string owner, Guid classId, ExamPreviewInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var scope = await ScopeAsync(owner, classId, input.Counts, input.LessonFrom, input.LessonTo, input.SkillIds, ct);
        if (scope.Errors is not null) return new(null, 400, scope.Errors);
        var (picked, shortfalls) = ExamGenerator.Draw(scope.Candidates!, input.Counts!, input.Seed is { } seed ? new Random(seed) : new Random());
        if (shortfalls.Count > 0) return new(null, 400, new() { ["counts"] = [.. shortfalls.Select(x => Shortfall(x, scope.Candidates!, scope.Skills!))] });
        return new(new(picked, scope.Skills!, await repository.ListImagesAsync(owner, classId, ct)));
    }

    /// <summary>Creates a draft exam from the questions the teacher accepted (as drawn by the preview, possibly swapped).</summary>
    public async Task<LearningResult<ExamDetail>> CreateAsync(string owner, Guid classId, ExamCreateInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var errors = Exam.ValidateHeader(input.Title, input.DurationMinutes);
        var scope = await ScopeAsync(owner, classId, input.Counts, input.LessonFrom, input.LessonTo, input.SkillIds, ct);
        foreach (var (key, value) in scope.Errors ?? []) errors[key] = value;
        if (errors.Count > 0) return new(null, 400, errors);
        var ids = input.QuestionIds ?? [];
        var byId = scope.Candidates!.ToDictionary(x => x.Id);
        if (ids.Distinct().Count() != ids.Length || ids.Any(x => !byId.ContainsKey(x)))
            return new(null, 400, new() { ["questionIds"] = ["Câu hỏi đã thay đổi hoặc không thuộc phạm vi. Hãy sinh đề lại."] });
        var chosen = ids.Select(x => byId[x]).ToList();
        for (var i = 0; i < QuestionLevel.All.Length; i++)
            if (chosen.Count(x => x.Level == QuestionLevel.All[i]) != input.Counts![i])
                return new(null, 400, new() { ["questionIds"] = ["Số câu theo mức không khớp cấu trúc đề. Hãy sinh đề lại."] });
        var ordered = chosen.OrderBy(x => Array.IndexOf(QuestionLevel.All, x.Level)).ToList();
        var exam = new Exam(owner, classId, input.Title!, input.Counts!, new(input.LessonFrom, input.LessonTo, input.SkillIds ?? []), input.DurationMinutes,
            ordered.Select(x => x.Snapshot()), clock.GetUtcNow());
        repository.Add([exam]);
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return await DetailAsync(owner, classId, exam.Id, ct);
    }

    public async Task<LearningResult<ExamDetail>> DetailAsync(string owner, Guid classId, Guid id, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, false, ct);
        if (exam is null) return new(null, 404);
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        return new(new(exam, curriculum?.Skills ?? [], await repository.ListImagesAsync(owner, classId, ct)));
    }

    /// <summary>Replaces one question of a draft by another of the same level and scope that is not in the exam yet.</summary>
    public async Task<LearningResult<ExamDetail>> SwapAsync(string owner, Guid classId, Guid id, ExamSwapInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        var item = exam.Items.SingleOrDefault(x => x.Order == input.Order);
        if (item is null) return new(null, 404);
        var scope = await ScopeAsync(owner, classId, [.. exam.Counts], exam.LessonFrom, exam.LessonTo, [.. exam.SkillIds], ct);
        if (scope.Errors is not null) return new(null, 400, scope.Errors);
        var used = exam.Items.Select(x => x.QuestionId).ToHashSet();
        var options = scope.Candidates!.Where(x => x.Level == item.Level && !used.Contains(x.Id)).ToList();
        if (options.Count == 0) return new(null, 400, new() { ["order"] = ["Ngân hàng không còn câu nào khác cùng mức trong phạm vi để đổi."] });
        var preferred = options.Where(x => x.PrimarySkillId == item.PrimarySkillId).ToList();
        var pool = preferred.Count > 0 ? preferred : options;
        var outcome = exam.Swap(input.Order, pool[Random.Shared.Next(pool.Count)].Snapshot(), input.Version);
        return await SaveAsync(owner, classId, exam, outcome, ct);
    }

    /// <summary>Opens the exam: creates its assessment (with the skill counts of the primary skills) and one seat per student.</summary>
    public async Task<LearningResult<ExamDetail>> OpenAsync(string owner, Guid classId, Guid id, ExamVersionInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        if (exam.Version != input.Version) return new(null, 409);
        if (exam.Status != ExamStatus.Draft) return new(null, 400, new() { ["exam"] = ["Đề đã được mở trước đó."] });
        var today = Today();
        var roster = await classroom.ReadRosterAsync(owner, classId, today, ct);
        if (roster is null) return new(null, 404);
        var students = roster.Students.Select(x => (x.Id, x.Name)).ToList();
        if (students.Count == 0) return new(null, 400, new() { ["exam"] = ["Lớp chưa có học sinh đang học để làm bài."] });
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        var now = clock.GetUtcNow();
        var assessment = new Assessment(owner, classId, exam.Title, today, students, "manual", "Đề kiểm tra online, chấm tự động theo đáp án.");
        var set = new AssessmentSkillSet(owner, classId, assessment.Id, now);
        var counts = exam.Items.GroupBy(x => x.PrimarySkillId).Select(g => new SkillMapItem(g.Key, g.Count())).ToArray();
        var mapped = set.SetSkills(new SkillMapInput(counts, 0), curriculum?.Skills ?? [], students, now);
        if (mapped.Status != 200)
            return new(null, 400, new() { ["exam"] = [.. (mapped.Errors?.Values.SelectMany(x => x) ?? ["Kỹ năng của đề không còn dùng được."]).Select(x => "Không mở được đề: " + x)] });
        var opened = exam.Open(assessment.Id, students, input.Version, now);
        if (opened.Status != 200) return new(null, opened.Status, opened.Errors);
        repository.Add([assessment, set]);
        repository.Add(set.TakeAdded());
        repository.Add(exam.TakeAdded());
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return await DetailAsync(owner, classId, id, ct);
    }

    public async Task<LearningResult<ExamDetail>> CloseAsync(string owner, Guid classId, Guid id, ExamVersionInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        return await SaveAsync(owner, classId, exam, exam.Close(input.Version, clock.GetUtcNow()), ct);
    }

    public async Task<LearningResult<ExamDetail>> ShowResultsAsync(string owner, Guid classId, Guid id, ExamShowInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        return await SaveAsync(owner, classId, exam, exam.SetShowResults(input.Show, input.Version), ct);
    }

    /// <summary>Scores one essay. Returns the seat so the screen can show its new total.</summary>
    public async Task<LearningResult<GradedSeat>> GradeAsync(string owner, Guid classId, Guid id, Guid studentId, int order, GradeInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        var outcome = exam.GradeEssay(studentId, order, input.Points, input.Comment, clock.GetUtcNow());
        if (outcome.Status != 200) return new(null, outcome.Status, outcome.Errors);
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        var seat = exam.Seats.Single(x => x.StudentId == studentId);
        return new(new GradedSeat(seat, seat.MaxScore(exam)));
    }

    public async Task<LearningResult<ExamDetail>> ResetSeatAsync(string owner, Guid classId, Guid id, Guid studentId, ExamVersionInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        return await SaveAsync(owner, classId, exam, exam.ResetSeat(studentId, input.Version), ct);
    }

    /// <summary>
    /// Writes the final results ("Kết quả cuối") the teacher confirms: each submission gets completion and correctness
    /// levels and each primary skill gets its count of right answers, both as confirmed revisions.
    /// </summary>
    public async Task<LearningResult<ExamDetail>> ConfirmAsync(string owner, Guid classId, Guid id, ExamVersionInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamAsync(owner, classId, id, true, ct);
        if (exam is null) return new(null, 404);
        if (exam.Version != input.Version) return new(null, 409);
        if (exam.Status != ExamStatus.Closed || exam.AssessmentId is not { } assessmentId)
            return new(null, 400, new() { ["exam"] = ["Đóng bài trước khi xác nhận kết quả."] });
        var waiting = exam.Seats.Sum(x => x.PendingEssays);
        if (waiting > 0) return new(null, 400, new() { ["exam"] = [$"Còn {waiting} bài tự luận chưa chấm. Chấm xong rồi mới xác nhận kết quả."] });
        var assessment = await repository.FindAssessmentAsync(owner, classId, assessmentId, ct);
        var set = await repository.FindSkillSetAsync(owner, classId, assessmentId, ct);
        if (assessment is null || set is null) return new(null, 400, new() { ["exam"] = ["Không tìm thấy bài đánh giá của đề."] });
        var now = clock.GetUtcNow();
        foreach (var seat in exam.Seats)
        {
            var total = exam.ItemsFor(seat).Count;
            var did = AttemptStatus.IsDone(seat.AttemptStatus) && seat.AnsweredCount > 0;
            if (assessment.Submissions.SingleOrDefault(x => x.StudentId == seat.StudentId) is { } submission)
            {
                var completion = ResultLevels.Nearest(seat.AnsweredCount, total);
                var rightQuestions = seat.CorrectEquivalent(exam);
                var result = !did ? new ResultInput("not_done", null, null, submission.Version, true)
                    : rightQuestions == 0 ? new ResultInput("no_correct", completion, 0, submission.Version, true)
                    : new ResultInput("assessed", completion, ResultLevels.Nearest(rightQuestions, total), submission.Version, true);
                if (submission.Validate(result).Count == 0) submission.Record(result, now);
            }
            var breakdown = seat.SkillBreakdown(exam);
            foreach (var skill in set.Skills)
            {
                var row = set.Results.SingleOrDefault(x => x.StudentId == seat.StudentId && x.SkillId == skill.SkillId);
                if (row is null) continue;
                var correct = breakdown.TryGetValue(skill.SkillId, out var counts) ? counts.CorrectEquivalent : 0;
                var skillInput = did ? new SkillResultInput(SkillResultStatus.Assessed, correct, null, row.Version, true)
                    : new SkillResultInput(SkillResultStatus.NotDone, null, null, row.Version, true);
                set.RecordResult(seat.StudentId, skill.SkillId, skillInput, owner, now);
            }
        }
        repository.Add(set.TakeAdded());
        return await SaveAsync(owner, classId, exam, exam.MarkConfirmed(input.Version, now), ct);
    }

    private sealed record ScopeResult(List<Question>? Candidates, List<Skill>? Skills, Dictionary<string, string[]>? Errors);

    /// <summary>Active questions whose primary skill lies in the scope: the skills of lessons X..Y, or the listed skills.</summary>
    private async Task<ScopeResult> ScopeAsync(string owner, Guid classId, int[]? counts, int? lessonFrom, int? lessonTo, Guid[]? skillIds, CancellationToken ct)
    {
        var errors = ExamRules.ValidateStructure(counts, lessonFrom, lessonTo, skillIds);
        if (errors.Count > 0) return new(null, null, errors);
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        if (curriculum is null) return new(null, null, new() { ["curriculum"] = ["Lớp chưa có chương trình và kỹ năng."] });
        var live = curriculum.GetEffectiveSkills().ToDictionary(x => x.Id);
        HashSet<Guid> scope;
        if (skillIds is { Length: > 0 })
        {
            if (skillIds.Any(x => !live.ContainsKey(x))) return new(null, null, new() { ["scope"] = ["Kỹ năng không còn dùng trong chương trình của lớp."] });
            scope = [.. skillIds];
        }
        else
            scope = curriculum.Lessons.Where(x => !x.Archived && x.No >= lessonFrom && x.No <= lessonTo).SelectMany(x => x.SkillIds).Where(live.ContainsKey).ToHashSet();
        if (scope.Count == 0) return new(null, null, new() { ["scope"] = ["Phạm vi đã chọn chưa gắn kỹ năng nào."] });
        var candidates = (await repository.ListQuestionsAsync(owner, classId, false, ct))
            .Where(x => x.Status == QuestionStatus.Active && scope.Contains(x.PrimarySkillId)).ToList();
        return new(candidates, [.. scope.Select(x => live[x]).OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)], null);
    }

    private static string Shortfall(Shortfall shortfall, IReadOnlyList<Question> candidates, IReadOnlyList<Skill> skills)
    {
        var label = QuestionLevel.Labels[shortfall.Level];
        var thin = skills.OrderBy(s => candidates.Count(q => q.Level == shortfall.Level && q.PrimarySkillId == s.Id)).ThenBy(s => s.Code).Take(5).Select(x => x.Code);
        return $"Mức {label}: cần {shortfall.Needed} câu nhưng ngân hàng chỉ có {shortfall.Available} câu trong phạm vi. Có thể bổ sung câu {label} cho kỹ năng: {string.Join(", ", thin)}.";
    }

    private async Task<LearningResult<ExamDetail>> SaveAsync(string owner, Guid classId, Exam exam, CurriculumOutcome outcome, CancellationToken ct)
    {
        if (outcome.Status != 200) return new(null, outcome.Status, outcome.Errors);
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return await DetailAsync(owner, classId, exam.Id, ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime); // Asia/Ho_Chi_Minh has no DST

    private async Task<bool> OwnsAsync(string owner, Guid classId, CancellationToken ct) =>
        await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is not null;
}
