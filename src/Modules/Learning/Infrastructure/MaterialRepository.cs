using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class MaterialRepository(LearningDatabase db) : IMaterialRepository
{
    public async Task<IReadOnlyList<Material>> ListAsync(string owner, Guid classId, CancellationToken ct) =>
        await db.Materials.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId)
            .OrderBy(x => x.Title).ThenBy(x => x.CreatedAt).ToListAsync(ct);
    public Task<Material?> FindAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct)
    {
        var query = db.Materials.AsQueryable();
        if (!track) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    }
    public Task<Skill?> FindSkillAsync(string owner, Guid classId, Guid skillId, CancellationToken ct) =>
        db.Skills.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == skillId, ct);
    // Entities with client-generated keys must be added explicitly; EF would treat them as modified otherwise.
    public void Add(params object[] entities) => db.AddRange(entities);
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
