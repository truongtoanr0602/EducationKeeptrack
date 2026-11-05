using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class PersonalizedLearningRepository(LearningDatabase db)
{
    public Task<ClassLearningProfile?> Profile(string owner, Guid classId, CancellationToken ct) => db.ClassLearningProfiles.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
    public Task<List<SkillObservation>> Observations(string owner, Guid classId, CancellationToken ct) => db.SkillObservations.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).OrderByDescending(x => x.Date).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
    public Task<List<SkillEvidence>> Evidence(string owner, Guid classId, CancellationToken ct) => db.SkillEvidence.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).OrderByDescending(x => x.Date).ThenByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
    public void Add<T>(T entity) where T : class => db.Set<T>().Add(entity);
    public async Task<bool> Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { db.ChangeTracker.Clear(); return false; }
    }
}
