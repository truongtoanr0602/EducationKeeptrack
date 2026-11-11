using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public interface IMaterialRepository
{
    Task<IReadOnlyList<Material>> ListAsync(string owner, Guid classId, CancellationToken ct);
    Task<Material?> FindAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct);
    Task<Skill?> FindSkillAsync(string owner, Guid classId, Guid skillId, CancellationToken ct);
    void Add(params object[] entities);
    Task<bool> SaveAsync(CancellationToken ct);
}

public sealed class MaterialService(IMaterialRepository repository, IClassroomLearningSource classroom, TimeProvider clock)
{
    public async Task<LearningResult<IReadOnlyList<Material>>> ListAsync(string owner, Guid classId, CancellationToken ct) =>
        await OwnsAsync(owner, classId, ct) ? new(await repository.ListAsync(owner, classId, ct)) : new(null, 404);

    public async Task<LearningResult<Material>> CreateAsync(string owner, Guid classId, MaterialInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        if (await ValidateAsync(owner, classId, input, ct) is { } errors) return new(null, 400, errors);
        var now = clock.GetUtcNow();
        var material = new Material(owner, classId, input, now);
        repository.Add(material, material.Created(owner, now));
        return await SaveAsync(owner, classId, material.Id, ct);
    }

    public async Task<LearningResult<Material>> EditAsync(string owner, Guid classId, Guid id, MaterialInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var material = await repository.FindAsync(owner, classId, id, true, ct);
        if (material is null) return new(null, 404);
        if (input.Version != material.Version) return new(null, 409);
        if (await ValidateAsync(owner, classId, input, ct) is { } errors) return new(null, 400, errors);
        if (material.Edit(input, owner, clock.GetUtcNow()) is { } revision) repository.Add(revision);
        return await SaveAsync(owner, classId, id, ct);
    }

    public async Task<LearningResult<Material>> ApproveAsync(string owner, Guid classId, Guid id, MaterialVersionInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var material = await repository.FindAsync(owner, classId, id, true, ct);
        if (material is null) return new(null, 404);
        if (input.Version != material.Version) return new(null, 409);
        if (material.Status == MaterialStatus.Approved) return new(null, 400, new() { ["material"] = ["Học liệu đã được duyệt."] });
        var skill = await repository.FindSkillAsync(owner, classId, material.SkillId, ct);
        if (skill is null || skill.Archived) return new(null, 400, new() { ["skillId"] = ["Kỹ năng của học liệu đã ngừng dùng; chọn kỹ năng khác trước khi duyệt."] });
        repository.Add(material.Approve(owner, clock.GetUtcNow()));
        return await SaveAsync(owner, classId, id, ct);
    }

    private async Task<Dictionary<string, string[]>?> ValidateAsync(string owner, Guid classId, MaterialInput input, CancellationToken ct)
    {
        var errors = Material.Validate(input);
        var skill = await repository.FindSkillAsync(owner, classId, input.SkillId, ct);
        if (skill is null || skill.Archived) errors["skillId"] = ["Chọn một kỹ năng đang dùng trong chương trình lớp."];
        return errors.Count > 0 ? errors : null;
    }

    private async Task<LearningResult<Material>> SaveAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        await repository.SaveAsync(ct) ? new(await repository.FindAsync(owner, classId, id, false, ct)) : new(null, 409);

    private async Task<bool> OwnsAsync(string owner, Guid classId, CancellationToken ct) =>
        await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is not null;
}
