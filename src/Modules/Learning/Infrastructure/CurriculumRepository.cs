using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class CurriculumRepository(LearningDatabase db) : ICurriculumRepository
{
    public Task<ClassCurriculum?> FindAsync(string owner, Guid classId, bool track, CancellationToken ct)
    {
        var query = db.Curricula.Include(x => x.Skills).ThenInclude(x => x.Events).AsSplitQuery();
        if (!track) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
    }
    // Entities with client-generated keys must be added explicitly; EF would treat them as modified otherwise.
    public void Add(IEnumerable<object> entities) => db.AddRange(entities);
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
