using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class SkillResultRepository(LearningDatabase db) : ISkillResultRepository
{
    public Task<Assessment?> FindAssessmentAsync(string owner, Guid classId, Guid assessmentId, CancellationToken ct) =>
        db.Assessments.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == assessmentId, ct);
    public Task<AssessmentSkillSet?> FindSetAsync(string owner, Guid classId, Guid assessmentId, bool track, CancellationToken ct)
    {
        var query = db.AssessmentSkillSets.Include(x => x.Skills).Include(x => x.Results).ThenInclude(x => x.History).AsSplitQuery();
        if (!track) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.AssessmentId == assessmentId, ct);
    }
    public async Task<IReadOnlyList<(Guid Id, string Name)>> ListStudentsAsync(Guid assessmentId, CancellationToken ct) =>
        (await db.Submissions.AsNoTracking().Where(x => x.AssessmentId == assessmentId).OrderBy(x => x.StudentName).ThenBy(x => x.StudentId)
            .Select(x => new { x.StudentId, x.StudentName }).ToListAsync(ct)).Select(x => (x.StudentId, x.StudentName)).ToList();
    public async Task<IReadOnlyList<Skill>> ListSkillsAsync(string owner, Guid classId, CancellationToken ct) =>
        await db.Skills.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
    // Entities with client-generated keys must be added explicitly; EF would treat them as modified otherwise.
    public void Add(IEnumerable<object> entities) => db.AddRange(entities);
    public void Remove(IEnumerable<object> entities) => db.RemoveRange(entities);
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
