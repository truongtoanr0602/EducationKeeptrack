using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public interface ISkillResultRepository
{
    Task<Assessment?> FindAssessmentAsync(string owner, Guid classId, Guid assessmentId, CancellationToken ct);
    Task<AssessmentSkillSet?> FindSetAsync(string owner, Guid classId, Guid assessmentId, bool track, CancellationToken ct);
    Task<IReadOnlyList<(Guid Id, string Name)>> ListStudentsAsync(Guid assessmentId, CancellationToken ct);
    Task<IReadOnlyList<Skill>> ListSkillsAsync(string owner, Guid classId, CancellationToken ct);
    void Add(IEnumerable<object> entities);
    void Remove(IEnumerable<object> entities);
    Task<bool> SaveAsync(CancellationToken ct);
}

/// <summary>Everything a screen needs to show the skills of one assessment.</summary>
public sealed record SkillResultsData(Assessment Assessment, AssessmentSkillSet? Set, IReadOnlyList<Skill> Skills);

public sealed class SkillResultService(ISkillResultRepository repository, TimeProvider clock)
{
    public async Task<LearningResult<SkillResultsData>> GetAsync(string owner, Guid classId, Guid assessmentId, CancellationToken ct) =>
        await LoadAsync(owner, classId, assessmentId, ct);

    public async Task<LearningResult<SkillResultsData>> SetSkillsAsync(string owner, Guid classId, Guid assessmentId, SkillMapInput input, CancellationToken ct)
    {
        if (await repository.FindAssessmentAsync(owner, classId, assessmentId, ct) is null) return new(null, 404);
        var now = clock.GetUtcNow();
        var set = await repository.FindSetAsync(owner, classId, assessmentId, true, ct);
        var isNew = set is null;
        set ??= new AssessmentSkillSet(owner, classId, assessmentId, now);
        var outcome = set.SetSkills(input, await repository.ListSkillsAsync(owner, classId, ct), await repository.ListStudentsAsync(assessmentId, ct), now);
        if (outcome.Status != 200) return new(null, outcome.Status, outcome.Errors);
        if (isNew && set.Version > 0) repository.Add([set]);
        if (!isNew || set.Version > 0)
        {
            repository.Add(set.TakeAdded());
            repository.Remove(set.TakeRemoved());
            if (!await repository.SaveAsync(ct)) return new(null, 409);
        }
        return await LoadAsync(owner, classId, assessmentId, ct);
    }

    public async Task<LearningResult<SkillResult>> RecordAsync(string owner, Guid classId, Guid assessmentId, Guid studentId, Guid skillId, SkillResultInput input, CancellationToken ct)
    {
        if (await repository.FindAssessmentAsync(owner, classId, assessmentId, ct) is null) return new(null, 404);
        var set = await repository.FindSetAsync(owner, classId, assessmentId, true, ct);
        if (set is null) return new(null, 404);
        var outcome = set.RecordResult(studentId, skillId, input, owner, clock.GetUtcNow());
        if (outcome.Status != 200) return new(null, outcome.Status, outcome.Errors);
        repository.Add(set.TakeAdded());
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        var saved = await repository.FindSetAsync(owner, classId, assessmentId, false, ct);
        return new(saved?.Results.SingleOrDefault(x => x.StudentId == studentId && x.SkillId == skillId));
    }

    private async Task<LearningResult<SkillResultsData>> LoadAsync(string owner, Guid classId, Guid assessmentId, CancellationToken ct)
    {
        var assessment = await repository.FindAssessmentAsync(owner, classId, assessmentId, ct);
        if (assessment is null) return new(null, 404);
        return new(new(assessment, await repository.FindSetAsync(owner, classId, assessmentId, false, ct), await repository.ListSkillsAsync(owner, classId, ct)));
    }
}
