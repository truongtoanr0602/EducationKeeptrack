using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
namespace EducationKeeptrack.Learning.Application;

public record JoinInput(Guid StudentId, string? Code);
public record AnswerInput(string[]? Values);

public sealed record ExamEntry(Exam Exam, IReadOnlyList<(Guid StudentId, string DisplayName)> Students);
public sealed record ExamAttempt(Exam Exam, ExamSeat Seat, string? SessionToken, IReadOnlyList<QuestionImage> Images);

/// <summary>
/// The student's side of an exam, reached by link without an account (ADR-0006): pick a name, type the four-digit code,
/// answer, submit. A session token bound to the seat's nonce travels in an HttpOnly cookie; joining again elsewhere
/// ends the older session. Answers are never sent to the student while the teacher has not released the results.
/// </summary>
public sealed class ExamTakeService(IQuestionBankRepository repository, IAccountAccess access, BankTokens tokens, TimeProvider clock)
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    /// <summary>Title and, while the exam is open, the short names to pick from. Draft exams do not exist for students.</summary>
    public async Task<LearningResult<ExamEntry>> EntryAsync(string token, CancellationToken ct)
    {
        var exam = await repository.FindExamByTokenAsync(token, false, ct);
        if (exam is null || exam.Status == ExamStatus.Draft) return new(null, 404);
        var students = exam.Status == ExamStatus.Open
            ? exam.Seats.OrderBy(x => DisplayName(x.StudentName)).Select(x => (x.StudentId, DisplayName(x.StudentName))).ToList()
            : [];
        return new(new(exam, students));
    }

    public async Task<LearningResult<ExamAttempt>> JoinAsync(string token, JoinInput input, CancellationToken ct)
    {
        var exam = await repository.FindExamByTokenAsync(token, true, ct);
        if (exam is null || exam.Status == ExamStatus.Draft) return new(null, 404);
        if (exam.Status == ExamStatus.Open && !(await access.GetAsync(exam.OwnerId, ct)).CanWrite)
            return new(null, 403, new() { ["exam"] = ["Bài kiểm tra tạm thời chưa mở. Em hỏi thầy cô nhé."] });
        var seat = exam.Seats.SingleOrDefault(x => x.StudentId == input.StudentId);
        if (seat is null) return new(null, 400, new() { ["studentId"] = ["Em chọn đúng tên của mình nhé."] });
        var now = clock.GetUtcNow();
        var outcome = seat.Join(exam, input.Code, now);
        if (outcome == JoinOutcome.Ok && exam.Status != ExamStatus.Open && seat.AttemptStatus == AttemptStatus.None)
            return new(null, 400, new() { ["exam"] = ["Bài kiểm tra đã đóng."] });
        seat.ExpireIfDue(exam, now);
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return outcome switch
        {
            JoinOutcome.Locked => new(null, 423, new() { ["code"] = ["Em đã nhập sai mã nhiều lần. Em nhờ thầy cô mở lại nhé."] }),
            JoinOutcome.WrongCode => new(null, 400, new() { ["code"] = [$"Mã chưa đúng. Em còn {ExamRules.MaxFailedCodes - seat.FailedTries} lần thử."] }),
            _ => new(await ViewAsync(exam, seat, tokens.IssueSeat(exam.Id, seat.Id, seat.Nonce, now.Add(SessionLifetime)), ct))
        };
    }

    public async Task<LearningResult<ExamAttempt>> AttemptAsync(string token, string? session, CancellationToken ct)
    {
        var (exam, seat) = await SeatAsync(token, session, ct);
        if (exam is null || seat is null) return new(null, 401);
        if (seat.ExpireIfDue(exam, clock.GetUtcNow()) && !await repository.SaveAsync(ct)) return new(null, 409);
        return new(await ViewAsync(exam, seat, null, ct));
    }

    public async Task<LearningResult<ExamAttempt>> AnswerAsync(string token, string? session, int order, AnswerInput input, CancellationToken ct)
    {
        var (exam, seat) = await SeatAsync(token, session, ct);
        if (exam is null || seat is null) return new(null, 401);
        var outcome = seat.SaveAnswer(exam, order, input.Values, clock.GetUtcNow());
        if (outcome.Status == 400 && seat.AttemptStatus != AttemptStatus.InProgress) await repository.SaveAsync(ct);
        if (outcome.Status != 200) return new(null, outcome.Status, outcome.Errors);
        return await repository.SaveAsync(ct) ? new(await ViewAsync(exam, seat, null, ct)) : new(null, 409);
    }

    public async Task<LearningResult<ExamAttempt>> SubmitAsync(string token, string? session, CancellationToken ct)
    {
        var (exam, seat) = await SeatAsync(token, session, ct);
        if (exam is null || seat is null) return new(null, 401);
        var outcome = seat.SubmitByStudent(exam, clock.GetUtcNow());
        if (outcome.Status != 200) return new(await ViewAsync(exam, seat, null, ct));
        return await repository.SaveAsync(ct) ? new(await ViewAsync(exam, seat, null, ct)) : new(null, 409);
    }

    /// <summary>"Nguyễn Văn An" becomes "An N.": enough to find oneself, not the full name of a minor on a public link.</summary>
    public static string DisplayName(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch { 0 => "?", 1 => parts[0], _ => $"{parts[^1]} {char.ToUpperInvariant(parts[0][0])}." };
    }

    private async Task<ExamAttempt> ViewAsync(Exam exam, ExamSeat seat, string? session, CancellationToken ct) =>
        new(exam, seat, session, await repository.ListImagesAsync(exam.OwnerId, exam.ClassId, ct));

    private async Task<(Exam?, ExamSeat?)> SeatAsync(string token, string? session, CancellationToken ct)
    {
        if (tokens.ValidateSeat(session, clock.GetUtcNow()) is not { } claim) return (null, null);
        var exam = await repository.FindExamByTokenAsync(token, true, ct);
        if (exam is null || exam.Id != claim.ExamId) return (null, null);
        var seat = exam.Seats.SingleOrDefault(x => x.Id == claim.SeatId);
        return seat is null || seat.Nonce.Length == 0 || seat.Nonce != claim.Nonce ? (exam, null) : (exam, seat);
    }
}
