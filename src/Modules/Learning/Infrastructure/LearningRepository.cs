using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class LearningRepository(LearningDatabase db) : ILearningRepository
{
    public async Task<IReadOnlyList<Assessment>> ListAsync(string owner, Guid classId, CancellationToken ct) =>
        await db.Assessments.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId)
            .OrderByDescending(x => x.Date).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<Assessment?> FindAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        db.Assessments.AsNoTracking().Include(x => x.Submissions).ThenInclude(x => x.History)
            .SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    public Task<Submission?> FindSubmissionAsync(string owner, Guid classId, Guid id, Guid studentId, CancellationToken ct) =>
        db.Submissions.Include(x => x.History).SingleOrDefaultAsync(x => x.AssessmentId == id && x.StudentId == studentId
            && db.Assessments.Any(a => a.Id == id && a.OwnerId == owner && a.ClassId == classId), ct);
    public void Add(Assessment assessment) => db.Assessments.Add(assessment);
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        // EF may insert the revision before updating the versioned submission.
        // SaveChanges rolls back both operations when the competing revision won.
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_ResultRevision_SubmissionId_Version" }) { return false; }
    }
}
