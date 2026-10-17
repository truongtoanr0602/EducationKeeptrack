using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public interface ILearningRepository
{
    Task<IReadOnlyList<Assessment>> ListAsync(string owner, Guid classId, CancellationToken ct);
    Task<Assessment?> FindAsync(string owner, Guid classId, Guid id, CancellationToken ct);
    Task<Submission?> FindSubmissionAsync(string owner, Guid classId, Guid id, Guid studentId, CancellationToken ct);
    void Add(Assessment assessment);
    Task<bool> SaveAsync(CancellationToken ct);
}

public record LearningResult<T>(T? Value, int Status = 200, Dictionary<string, string[]>? Errors = null) where T : class;
public record AssessmentInput(string? Title, DateOnly Date, string DefaultMethod);
public record MethodInput(string Method, int Version);

public sealed class LearningService(ILearningRepository repository, IClassroomLearningSource classroom, TimeProvider clock)
{
    public async Task<LearningResult<Assessment>> CreateAsync(string owner, Guid classId, AssessmentInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 160 || input.Date == default || input.DefaultMethod != "manual")
            return new(null, 400, new() { ["assessment"] = ["Nhập tên (tối đa 160 ký tự), ngày và cách tự chấm. AI chưa khả dụng."] });
        var roster = await classroom.ReadRosterAsync(owner, classId, input.Date, ct);
        if (roster is null) return new(null, 404);
        var assessment = new Assessment(owner, classId, input.Title, input.Date, roster.Students.Select(x => (x.Id, x.Name)));
        repository.Add(assessment);
        await repository.SaveAsync(ct);
        return new(assessment, 201);
    }
    public async Task<LearningResult<IReadOnlyList<Assessment>>> ListAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is null) return new(null, 404);
        return new(await repository.ListAsync(owner, classId, ct));
    }
    public async Task<LearningResult<Assessment>> FindAsync(string owner, Guid classId, Guid id, CancellationToken ct)
    {
        var assessment = await repository.FindAsync(owner, classId, id, ct);
        return new(assessment, assessment is null ? 404 : 200);
    }
    public async Task<LearningResult<Submission>> RecordAsync(string owner, Guid classId, Guid id, Guid studentId, ResultInput input, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        if (input.Version != submission.Version) return new(null, 409);
        var errors = submission.Validate(input);
        if (errors.Count > 0) return new(null, 400, errors);
        submission.Record(input, clock.GetUtcNow());
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }
    public async Task<LearningResult<Submission>> ChangeMethodAsync(string owner, Guid classId, Guid id, Guid studentId, MethodInput input, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        if (input.Version != submission.Version || submission.History.Any(x => x.Action != "method_changed")) return new(null, 409);
        if (input.Method != "manual") return new(null, 400, new() { ["method"] = ["AI chưa khả dụng. Vui lòng chọn tự chấm."] });
        submission.RecordMethod(input.Method, clock.GetUtcNow());
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }
}
