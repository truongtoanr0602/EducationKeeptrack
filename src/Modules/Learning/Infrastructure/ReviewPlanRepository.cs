using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class ReviewPlanRepository(LearningDatabase db) : IReviewPlanRepository
{
    public async Task<IReadOnlyList<ReviewPlan>> ListAsync(string owner, Guid classId, CancellationToken ct) =>
        await db.ReviewPlans.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).OrderByDescending(x => x.WeekStart).ToListAsync(ct);
    public Task<ReviewPlan?> FindAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct)
    {
        var query = db.ReviewPlans.Include(x => x.Revisions).AsSplitQuery();
        if (!track) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    }
    public Task<bool> ExistsForWeekAsync(string owner, Guid classId, DateOnly weekStart, CancellationToken ct) =>
        db.ReviewPlans.AnyAsync(x => x.OwnerId == owner && x.ClassId == classId && x.WeekStart == weekStart, ct);
    // Entities with client-generated keys must be added explicitly; EF would treat them as modified otherwise.
    public void Add(IEnumerable<object> entities) => db.AddRange(entities);
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
