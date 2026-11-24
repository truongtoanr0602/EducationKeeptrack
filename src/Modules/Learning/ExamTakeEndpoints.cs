using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Learning;

/// <summary>
/// The student's exam routes (ADR-0006): no account, reached by the exam link. The session lives in an HttpOnly
/// cookie limited to these routes. Shapes follow ui/src/exam-take-api.ts.
/// </summary>
public static class ExamTakeEndpoints
{
    public const string Cookie = "ekt.exam", Path = "/api/exam-take", RateLimitPolicy = "exam-take";

    public static void MapExamTake(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Path + "/{token}").AllowAnonymous().RequireRateLimiting(RateLimitPolicy);
        group.MapGet("/", async (string token, HttpContext http, ExamTakeService service) =>
            BankEndpoints.Respond(await service.EntryAsync(token, http.RequestAborted), entry => new
            {
                entry.Exam.Title, entry.Exam.Status, entry.Exam.DurationMinutes, questionCount = entry.Exam.Items.Count(x => x.SeatId is null),
                students = entry.Students.Select(x => new { id = x.StudentId, name = x.DisplayName })
            }));
        group.MapPost("/join", async (string token, JoinInput input, HttpContext http, ExamTakeService service, BankTokens tokens, TimeProvider clock) =>
        {
            var result = await service.JoinAsync(token, input, http.RequestAborted);
            if (result.Value?.SessionToken is { } session)
                http.Response.Cookies.Append(Cookie, session, new CookieOptions
                {
                    HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = Path,
                    MaxAge = ExamTakeService.SessionLifetime, IsEssential = true
                });
            return BankEndpoints.Respond(result, a => View(a, tokens, clock));
        });
        group.MapGet("/attempt", async (string token, HttpContext http, ExamTakeService service, BankTokens tokens, TimeProvider clock) =>
            BankEndpoints.Respond(await service.AttemptAsync(token, http.Request.Cookies[Cookie], http.RequestAborted), a => View(a, tokens, clock)));
        group.MapPut("/attempt/answers/{order:int}", async (string token, int order, AnswerInput input, HttpContext http, ExamTakeService service, BankTokens tokens, TimeProvider clock) =>
            BankEndpoints.Respond(await service.AnswerAsync(token, http.Request.Cookies[Cookie], order, input, http.RequestAborted), a => new
            {
                a.Seat.AttemptStatus, answered = a.Seat.AnsweredCount, deadline = a.Seat.Deadline
            }));
        group.MapPost("/attempt/submit", async (string token, HttpContext http, ExamTakeService service, BankTokens tokens, TimeProvider clock) =>
            BankEndpoints.Respond(await service.SubmitAsync(token, http.Request.Cookies[Cookie], http.RequestAborted), a => View(a, tokens, clock)));
    }

    /// <summary>
    /// What the student sees. Answer keys appear only after the teacher closed the exam and released results;
    /// before that a running attempt gets its questions in its own order, a finished one only a thank-you.
    /// </summary>
    private static object View(ExamAttempt attempt, BankTokens tokens, TimeProvider clock)
    {
        var (exam, seat) = (attempt.Exam, attempt.Seat);
        var url = BankEndpoints.Urls(attempt.Images, tokens, clock);
        var running = seat.AttemptStatus == AttemptStatus.InProgress && exam.Status == ExamStatus.Open;
        var presented = seat.PresentedItems(exam);
        var answers = seat.Answers;
        var released = AttemptStatus.IsDone(seat.AttemptStatus) && exam.ShowResults && exam.Status is ExamStatus.Closed or ExamStatus.Confirmed;
        return new
        {
            exam.Title, examStatus = exam.Status, seat.AttemptStatus, studentName = ExamTakeService.DisplayName(seat.StudentName),
            seat.Deadline, now = clock.GetUtcNow(), total = presented.Count, answered = seat.AnsweredCount,
            items = running ? presented.Select((item, index) =>
            {
                var q = item.Snapshot;
                return new
                {
                    item.Order, number = index + 1, q.Type, q.Stem, stemImageUrl = url(q.StemImage), q.Blanks, q.Points, q.FillMode,
                    stemHtml = RichStem.Resolve(q.StemHtml, n => url(n)), audioUrl = url(q.AudioFile),
                    // A multiple-choice question says how many options to tick; the keys themselves stay on the server.
                    choose = q.Type == QuestionType.MultiChoice && q.Answer.Length == 1 ? q.Answer[0].Length : (int?)null,
                    options = seat.PresentedOptions(item).Select(o => new { o.Key, o.Text, imageUrl = url(o.Image) })
                };
            }).ToList() : null,
            answers = running ? answers.ToDictionary(x => x.Key.ToString(), x => x.Value) : null,
            result = released ? new
            {
                correct = seat.CorrectCount,
                total = presented.Count,
                score = seat.Score, maxScore = seat.MaxScore(exam), seat.PendingEssays,
                items = presented.Select((item, index) =>
                {
                    var q = item.Snapshot;
                    var given = answers.GetValueOrDefault(item.Order);
                    var grade = seat.Grades.GetValueOrDefault(item.Order);
                    var essay = q.Type == QuestionType.Essay;
                    return new
                    {
                        number = index + 1, q.Type, q.Stem, stemImageUrl = url(q.StemImage), stemHtml = RichStem.Resolve(q.StemHtml, n => url(n)), audioUrl = url(q.AudioFile),
                        given, correct = essay ? grade is not null && grade.Points == q.Points : q.IsCorrect(given), points = q.Points,
                        score = essay ? grade?.Points ?? 0 : q.Score(given), graded = !essay || grade is not null, comment = grade?.Comment,
                        answer = q.Type is QuestionType.SingleChoice or QuestionType.MultiChoice
                            ? q.Options.Where(o => q.Answer.Length > 0 && q.Answer[0].Contains(o.Key, StringComparer.OrdinalIgnoreCase))
                                .Select(o => o.Text.Length > 0 ? $"{o.Key}. {o.Text}" : o.Key).ToArray()
                            : q.Type == QuestionType.Essay
                                ? (q.SampleAnswer is { Length: > 0 } sample ? new[] { sample } : [])
                                : q.Answer.Select(x => x.FirstOrDefault() ?? "").ToArray(),
                        options = q.Options.Select(o => new { o.Key, o.Text, imageUrl = url(o.Image) })
                    };
                }).ToList()
            } : null
        };
    }
}
