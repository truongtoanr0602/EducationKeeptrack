using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Learning;

/// <summary>Teacher routes of lessons, the Excel import, the question bank and exams; shapes follow ui/src/bank-api.ts.</summary>
public static class BankEndpoints
{
    public static void MapBank(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(LearningAccess.Require);

        group.MapGet("/lessons", async (Guid classId, HttpContext http, LessonService service) =>
            Respond(await service.PlanAsync(LearningAccess.Owner(http), classId, http.RequestAborted), PlanView));
        group.MapPost("/lessons", async (Guid classId, LessonInput input, HttpContext http, LessonService service) =>
            Respond(await service.AddAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), PlanView, 201));
        group.MapPut("/lessons/{lessonId:guid}", async (Guid classId, Guid lessonId, LessonInput input, HttpContext http, LessonService service) =>
            Respond(await service.EditAsync(LearningAccess.Owner(http), classId, lessonId, input, http.RequestAborted), PlanView));
        group.MapPut("/lessons/{lessonId:guid}/session", async (Guid classId, Guid lessonId, LessonLinkInput input, HttpContext http, LessonService service) =>
            Respond(await service.LinkAsync(LearningAccess.Owner(http), classId, lessonId, input, http.RequestAborted), PlanView));

        group.MapGet("/import-template", () =>
            Results.File(WorkbookFormat.BuildTemplate(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "mau-chuong-trinh-toan-2-hk1.xlsx"));
        group.MapPost("/imports", async (Guid classId, bool? commit, HttpRequest request, HttpContext http, ImportService service) =>
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Chọn file Excel (.xlsx) theo mẫu để tải lên."] });
            var file = request.Form.Files[0];
            if (file.Length is <= 0 or > 5 * 1024 * 1024)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["File Excel tối đa 5MB."] });
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, http.RequestAborted);
            stream.Position = 0;
            return Respond(await service.ImportAsync(LearningAccess.Owner(http), classId, stream, commit == true, http.RequestAborted), ReportView);
        }).DisableAntiforgery();
        group.MapPost("/copy-from/{sourceClassId:guid}", async (Guid classId, Guid sourceClassId, bool? commit, HttpContext http, ImportService service) =>
            Respond(await service.CopyAsync(LearningAccess.Owner(http), classId, sourceClassId, commit == true, http.RequestAborted), ReportView));

        group.MapGet("/questions", async (Guid classId, HttpContext http, QuestionBankService service) =>
            Respond(await service.GetAsync(LearningAccess.Owner(http), classId, http.RequestAborted), bank => BankView(bank, service)));
        group.MapPost("/questions", async (Guid classId, QuestionInput input, HttpContext http, QuestionBankService service) =>
            Respond(await service.CreateAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), q => QuestionView(q, null), 201));
        group.MapPut("/questions/{id:guid}", async (Guid classId, Guid id, QuestionInput input, HttpContext http, QuestionBankService service) =>
            Respond(await service.EditAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted), q => QuestionView(q, null)));
        group.MapPost("/question-images", async (Guid classId, HttpRequest request, HttpContext http, QuestionBankService service) =>
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Chọn ảnh để tải lên."] });
            var file = request.Form.Files[0];
            await using var stream = file.OpenReadStream();
            var result = await service.UploadImageAsync(LearningAccess.Owner(http), classId, file.FileName, stream, file.ContentType, file.Length, http.RequestAborted);
            return Respond(result, service.View, result.Status == 201 ? 201 : 200);
        }).DisableAntiforgery();

        group.MapGet("/exam-blueprints", async (Guid classId, HttpContext http, ExamService service) =>
            Respond(await service.BlueprintsAsync(LearningAccess.Owner(http), classId, http.RequestAborted), BlueprintsView));
        group.MapPost("/exam-blueprints", async (Guid classId, BlueprintInput input, HttpContext http, ExamService service) =>
            Respond(await service.SaveBlueprintAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), BlueprintsView));
        group.MapGet("/exams", async (Guid classId, HttpContext http, ExamService service) =>
            Respond(await service.ListAsync(LearningAccess.Owner(http), classId, http.RequestAborted), list => list.Select(x => new
            {
                x.Id, x.Title, x.Status, x.CreatedAt, x.OpenedAt, x.ClosedAt, x.ConfirmedAt, x.AssessmentId, x.DurationMinutes,
                questionCount = x.Counts.Sum(), seats = x.Seats.Count, submitted = x.Seats.Count(s => AttemptStatus.IsDone(s.AttemptStatus))
            })));
        group.MapPost("/exams/preview", async (Guid classId, ExamPreviewInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.PreviewAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), preview =>
            {
                var url = Urls(preview.Images, tokens, clock);
                return new { questions = preview.Questions.Select(q => QuestionView(q, url)), skills = preview.Skills.Select(SkillView) };
            }));
        group.MapPost("/exams", async (Guid classId, ExamCreateInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.CreateAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), d => ExamView(d, tokens, clock), 201));
        group.MapGet("/exams/{id:guid}", async (Guid classId, Guid id, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.DetailAsync(LearningAccess.Owner(http), classId, id, http.RequestAborted), d => ExamView(d, tokens, clock)));
        group.MapPost("/exams/{id:guid}/swap", async (Guid classId, Guid id, ExamSwapInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.SwapAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted), d => ExamView(d, tokens, clock)));
        group.MapPost("/exams/{id:guid}/open", async (Guid classId, Guid id, ExamVersionInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.OpenAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted), d => ExamView(d, tokens, clock)));
        group.MapPost("/exams/{id:guid}/close", async (Guid classId, Guid id, ExamVersionInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.CloseAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted), d => ExamView(d, tokens, clock)));
        group.MapPut("/exams/{id:guid}/show-results", async (Guid classId, Guid id, ExamShowInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.ShowResultsAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted), d => ExamView(d, tokens, clock)));
        group.MapPost("/exams/{id:guid}/confirm", async (Guid classId, Guid id, ExamVersionInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.ConfirmAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted), d => ExamView(d, tokens, clock)));
        // Essays are scored by the teacher: the screen reads every essay with its answers, and saves one score at a time.
        group.MapGet("/exams/{id:guid}/grading", async (Guid classId, Guid id, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.DetailAsync(LearningAccess.Owner(http), classId, id, http.RequestAborted), d => GradingView(d, tokens, clock)));
        group.MapPut("/exams/{id:guid}/seats/{studentId:guid}/grades/{order:int}", async (Guid classId, Guid id, Guid studentId, int order, GradeInput input, HttpContext http, ExamService service) =>
            Respond(await service.GradeAsync(LearningAccess.Owner(http), classId, id, studentId, order, input, http.RequestAborted), GradedView));
        group.MapPost("/exams/{id:guid}/seats/{studentId:guid}/reset", async (Guid classId, Guid id, Guid studentId, ExamVersionInput input, HttpContext http, ExamService service, BankTokens tokens, TimeProvider clock) =>
            Respond(await service.ResetSeatAsync(LearningAccess.Owner(http), classId, id, studentId, input, http.RequestAborted), d => ExamView(d, tokens, clock)));

        // Signed links: both the teacher's screens and the student's exam page show question images through them.
        endpoints.MapGet("/api/learning/question-images/{token}", async (string token, HttpContext http, QuestionBankService service) =>
        {
            var (stream, contentType, status) = await service.OpenImageAsync(token, http.RequestAborted);
            if (status != 200 || stream is null) return Results.StatusCode(status);
            http.Response.Headers.CacheControl = "private, max-age=3600";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            // Audio is served with byte ranges (Safari will not play it otherwise); it is at most 5 MB, so it is read whole to be seekable.
            if (!contentType.StartsWith("audio/")) return Results.File(stream, contentType);
            await using (stream)
            {
                var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, http.RequestAborted);
                buffer.Position = 0;
                return Results.File(buffer, contentType, enableRangeProcessing: true);
            }
        });
    }

    internal static IResult Respond<T>(LearningResult<T> result, Func<T, object> view, int success = 200) where T : class => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        401 => Results.Json(new { message = "Phiên làm bài đã hết. Em vào lại bằng tên và mã nhé." }, statusCode: 401),
        403 or 423 => Results.Json(new { message = result.Errors?.Values.FirstOrDefault()?.FirstOrDefault() ?? "Không có quyền thực hiện." }, statusCode: result.Status),
        409 => Results.Conflict(new { message = "Dữ liệu đã thay đổi ở phiên khác. Tải lại rồi thử lại; nội dung đang nhập được giữ nguyên." }),
        _ => Results.Json(view(result.Value!), statusCode: result.Status is 201 ? 201 : success)
    };

    internal static Func<string?, string?> Urls(IEnumerable<QuestionImage> images, BankTokens tokens, TimeProvider clock)
    {
        var byName = images.GroupBy(x => x.NormalizedName).ToDictionary(g => g.Key, g => g.First().Id);
        var expires = clock.GetUtcNow().Add(QuestionBankService.ImageLinkLifetime);
        return name => name is not null && byName.TryGetValue(QuestionImage.NormalizeName(name), out var id)
            ? $"/api/learning/question-images/{Uri.EscapeDataString(tokens.IssueImage(id, expires))}" : null;
    }

    private static object PlanView(LessonPlan plan) => new
    {
        configured = plan.Curriculum is not null,
        version = plan.Curriculum?.Version ?? 0,
        lessons = plan.Slots.Select(slot => new
        {
            slot.Lesson.Id, slot.Lesson.No, slot.Lesson.Topic, slot.Lesson.Title, slot.Lesson.Knowledge, slot.Lesson.Outcomes, slot.Lesson.SkillsText,
            slot.Lesson.Competencies, slot.Lesson.SkillIds,
            skillCodes = slot.Lesson.SkillIds.Select(id => plan.Curriculum!.Skills.FirstOrDefault(s => s.Id == id)?.Code).Where(x => x is not null),
            session = slot.Session is { } s ? new { s.Id, date = s.Date.ToString("yyyy-MM-dd"), startTime = s.StartTime.ToString("HH:mm"), s.Status } : null,
            manual = slot.Manual, taught = slot.Session?.Status == "held"
        }),
        archived = (plan.Curriculum?.Lessons ?? []).Where(x => x.Archived).OrderBy(x => x.No).Select(x => new { x.Id, x.No, x.Title }),
        taughtSkillIds = plan.TaughtSkills,
        sessions = plan.Sessions.Where(x => x.Status != "cancelled").Select(s => new { s.Id, date = s.Date.ToString("yyyy-MM-dd"), startTime = s.StartTime.ToString("HH:mm"), s.Status })
    };

    private static object ReportView(ImportReport report) => new
    {
        report.Committed, report.CurriculumCreated,
        skills = new { added = report.SkillsAdded, updated = report.SkillsUpdated },
        lessons = new { added = report.LessonsAdded, updated = report.LessonsUpdated },
        questions = new { added = report.QuestionsAdded, updated = report.QuestionsUpdated },
        issues = report.Issues.Select(x => new { x.Sheet, x.Row, x.Column, x.Message }),
        report.MissingImages
    };

    private static object BankView(QuestionBank bank, QuestionBankService service)
    {
        var views = bank.Images.ToDictionary(x => x.NormalizedName, service.View);
        string? Url(string? name) => name is not null && views.TryGetValue(QuestionImage.NormalizeName(name), out var view) ? view.Url : null;
        return new
        {
            questions = bank.Questions.Select(q => QuestionView(q, Url)),
            images = views.Values.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
            skills = bank.Skills.Where(x => !x.Archived).OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Select(SkillView),
            levels = QuestionLevel.All.Select(x => new { code = x, label = QuestionLevel.Labels[x] }),
            types = QuestionType.All.Select(x => new { code = x, label = QuestionType.Labels[x] })
        };
    }

    private static object QuestionView(Question q, Func<string?, string?>? url) => new
    {
        q.Id, q.Code, q.Type, q.Level, q.PrimarySkillId, q.SecondarySkillIds, q.LessonNo, q.Stem, q.StemImage, stemImageUrl = url?.Invoke(q.StemImage),
        options = q.Options.Select(o => new { o.Key, o.Text, o.Image, imageUrl = url?.Invoke(o.Image) }),
        q.Answer, archived = q.Status == QuestionStatus.Archived, q.Version, q.UpdatedAt, blanks = q.Snapshot().Blanks,
        q.Points, q.FillMode, q.IgnoreDiacritics, q.SampleAnswer, q.StemHtml, q.AudioFile
    };

    private static object SkillView(Skill s) => new { s.Id, s.Code, s.Name, s.Strand };

    private static object BlueprintsView(IReadOnlyList<ExamBlueprint> list) => list.Select(x => new { x.Id, x.Name, x.Counts, x.LessonFrom, x.LessonTo, x.SkillIds, x.UpdatedAt });

    private static object GradedView(GradedSeat graded) => new
    {
        graded.Seat.StudentId, score = graded.Seat.Score, maxScore = graded.MaxScore, graded.Seat.PendingEssays, correct = graded.Seat.CorrectCount
    };

    /// <summary>Every essay of the exam with the answers of the students who submitted it, and what has been scored.</summary>
    private static object GradingView(ExamDetail detail, BankTokens tokens, TimeProvider clock)
    {
        var exam = detail.Exam;
        var url = Urls(detail.Images, tokens, clock);
        var done = exam.Seats.Where(s => AttemptStatus.IsDone(s.AttemptStatus)).OrderBy(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new
        {
            exam.Id, exam.Title, exam.Status, exam.Version,
            pending = exam.Seats.Sum(s => s.PendingEssays),
            essays = exam.Items.Where(x => x.SeatId is null && x.Snapshot.Type == QuestionType.Essay).OrderBy(x => x.Order).Select(item =>
            {
                var q = item.Snapshot;
                return new
                {
                    item.Order, q.QuestionId, q.Code, q.Stem, stemImageUrl = url(q.StemImage), q.Points, q.SampleAnswer,
                    responses = done.Where(s => s.Answers.ContainsKey(item.Order)).Select(s =>
                    {
                        var grade = s.Grades.GetValueOrDefault(item.Order);
                        return new
                        {
                            s.StudentId, s.StudentName, answer = s.Answers[item.Order].FirstOrDefault() ?? "",
                            points = grade?.Points, comment = grade?.Comment, gradedAt = grade?.GradedAt
                        };
                    }).ToList()
                };
            }).ToList(),
            students = done.Select(s => new { s.StudentId, s.StudentName, score = s.Score, maxScore = s.MaxScore(exam), s.PendingEssays }).ToList()
        };
    }

    private static object ExamView(ExamDetail detail, BankTokens tokens, TimeProvider clock)
    {
        var exam = detail.Exam;
        var url = Urls(detail.Images, tokens, clock);
        var codes = detail.Skills.ToDictionary(x => x.Id, x => x.Code);
        var items = exam.Items.Where(x => x.SeatId is null).OrderBy(x => x.Order).ToList();
        return new
        {
            exam.Id, exam.Title, exam.Kind, exam.Status, exam.DurationMinutes, exam.ShowResults, exam.PublicToken, exam.AssessmentId, exam.Counts,
            exam.LessonFrom, exam.LessonTo, exam.SkillIds, exam.Version, exam.CreatedAt, exam.OpenedAt, exam.ClosedAt, exam.ConfirmedAt,
            items = items.Select(item =>
            {
                var q = item.Snapshot;
                return new
                {
                    item.Order, q.QuestionId, q.Code, q.Type, q.Level, q.Stem, stemImageUrl = url(q.StemImage), blanks = q.Blanks, q.Points, q.FillMode, q.IgnoreDiacritics, q.SampleAnswer,
                    stemHtml = RichStem.Resolve(q.StemHtml, n => url(n)), audioUrl = url(q.AudioFile),
                    options = q.Options.Select(o => new { o.Key, o.Text, o.Image, imageUrl = url(o.Image) }), q.Answer,
                    q.PrimarySkillId, skillCode = codes.GetValueOrDefault(q.PrimarySkillId)
                };
            }),
            skills = items.Select(x => x.PrimarySkillId).Distinct().Select(id => new { id, code = codes.GetValueOrDefault(id), tasks = items.Count(x => x.PrimarySkillId == id) })
                .OrderBy(x => x.code, StringComparer.OrdinalIgnoreCase),
            seats = exam.Seats.OrderBy(x => x.StudentName).Select(seat => new
            {
                seat.StudentId, seat.StudentName, seat.Code, seat.AttemptStatus, answered = seat.AnsweredCount, correct = seat.CorrectCount,
                score = seat.Score, maxScore = seat.MaxScore(exam), seat.PendingEssays,
                total = exam.ItemsFor(seat).Count, seat.StartedAt, seat.SubmittedAt, seat.Deadline, seat.Locked, seat.FailedTries,
                skills = AttemptStatus.IsDone(seat.AttemptStatus)
                    ? seat.SkillBreakdown(exam).Select(x => new { skillId = x.Key, code = codes.GetValueOrDefault(x.Key), x.Value.Tasks, Correct = x.Value.CorrectEquivalent, x.Value.Earned, x.Value.Max, x.Value.Pending })
                        .OrderBy(x => x.code, StringComparer.OrdinalIgnoreCase).ToList()
                    : null
            })
        };
    }
}
