using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public interface ICurriculumRepository
{
    Task<ClassCurriculum?> FindAsync(string owner, Guid classId, bool track, CancellationToken ct);
    void Add(IEnumerable<object> entities);
    Task<bool> SaveAsync(CancellationToken ct);
}

public sealed class CurriculumService(ICurriculumRepository repository, IClassroomLearningSource classroom, TimeProvider clock)
{
    /// <summary>Returns the curriculum, or a null value with status 200 when the class has none yet.</summary>
    public async Task<LearningResult<ClassCurriculum>> GetAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        return new(await repository.FindAsync(owner, classId, false, ct));
    }

    public async Task<LearningResult<ClassCurriculum>> ConfigureAsync(string owner, Guid classId, CurriculumInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var existing = await repository.FindAsync(owner, classId, true, ct);
        var now = clock.GetUtcNow();
        if (existing is null)
        {
            var errors = ClassCurriculum.Validate(input);
            if (errors.Count > 0) return new(null, 400, errors);
            repository.Add([new ClassCurriculum(owner, classId, input, now)]);
        }
        else
        {
            var outcome = existing.Configure(input, now);
            if (outcome.Status != 200) return Failed(outcome);
        }
        return await SaveAndReloadAsync(owner, classId, ct);
    }

    public Task<LearningResult<ClassCurriculum>> AddSkillAsync(string owner, Guid classId, SkillInput input, CancellationToken ct) =>
        MutateAsync(owner, classId, (c, now) => c.AddSkill(input, owner, now), ct);
    public Task<LearningResult<ClassCurriculum>> EditSkillAsync(string owner, Guid classId, Guid skillId, SkillInput input, CancellationToken ct) =>
        MutateAsync(owner, classId, (c, now) => c.EditSkill(skillId, input, owner, now), ct);

    private async Task<LearningResult<ClassCurriculum>> MutateAsync(string owner, Guid classId, Func<ClassCurriculum, DateTimeOffset, CurriculumOutcome> operation, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var curriculum = await repository.FindAsync(owner, classId, true, ct);
        if (curriculum is null) return new(null, 400, new() { ["curriculum"] = ["Cấu hình môn, học kỳ và bộ sách của lớp trước khi thêm kỹ năng."] });
        var outcome = operation(curriculum, clock.GetUtcNow());
        if (outcome.Status != 200) return Failed(outcome);
        repository.Add(curriculum.TakeAdded());
        return await SaveAndReloadAsync(owner, classId, ct);
    }

    private async Task<LearningResult<ClassCurriculum>> SaveAndReloadAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await repository.SaveAsync(ct)) return new(null, 409);
        return new(await repository.FindAsync(owner, classId, false, ct));
    }

    private static LearningResult<ClassCurriculum> Failed(CurriculumOutcome outcome) => new(null, outcome.Status, outcome.Errors);

    private async Task<bool> OwnsAsync(string owner, Guid classId, CancellationToken ct) =>
        await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is not null;
}
