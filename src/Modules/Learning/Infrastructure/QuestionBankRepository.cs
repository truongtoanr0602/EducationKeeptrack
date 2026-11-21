using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class QuestionBankRepository(LearningDatabase db) : IQuestionBankRepository
{
    public Task<ClassCurriculum?> FindCurriculumAsync(string owner, Guid classId, bool track, CancellationToken ct)
    {
        var query = db.Curricula.Include(x => x.Skills).ThenInclude(x => x.Events).Include(x => x.Lessons).AsSplitQuery();
        if (!track) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
    }
    public Task<List<LessonSessionLink>> ListLinksAsync(string owner, Guid classId, CancellationToken ct) =>
        db.LessonSessionLinks.Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
    public Task<List<Question>> ListQuestionsAsync(string owner, Guid classId, bool track, CancellationToken ct)
    {
        var query = db.Questions.Where(x => x.OwnerId == owner && x.ClassId == classId);
        return (track ? query : query.AsNoTracking()).OrderBy(x => x.Code).ToListAsync(ct);
    }
    public Task<Question?> FindQuestionAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        db.Questions.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    public Task<List<QuestionImage>> ListImagesAsync(string owner, Guid classId, CancellationToken ct) =>
        db.QuestionImages.Where(x => x.OwnerId == owner && x.ClassId == classId).OrderBy(x => x.NormalizedName).ToListAsync(ct);
    public Task<QuestionImage?> FindImageAsync(Guid id, CancellationToken ct) => db.QuestionImages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<ExamBlueprint>> ListBlueprintsAsync(string owner, Guid classId, CancellationToken ct) =>
        db.ExamBlueprints.Where(x => x.OwnerId == owner && x.ClassId == classId).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<List<Exam>> ListExamsAsync(string owner, Guid classId, CancellationToken ct) =>
        db.Exams.AsNoTracking().Include(x => x.Seats).AsSplitQuery().Where(x => x.OwnerId == owner && x.ClassId == classId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public Task<Exam?> FindExamAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct)
    {
        var query = db.Exams.Include(x => x.Items).Include(x => x.Seats).AsSplitQuery();
        return (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    }
    public Task<Exam?> FindExamByTokenAsync(string token, bool track, CancellationToken ct)
    {
        var query = db.Exams.Include(x => x.Items).Include(x => x.Seats).AsSplitQuery();
        return (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(x => x.PublicToken == token, ct);
    }
    public Task<Assessment?> FindAssessmentAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        db.Assessments.Include(x => x.Submissions).ThenInclude(x => x.History).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    public Task<AssessmentSkillSet?> FindSkillSetAsync(string owner, Guid classId, Guid assessmentId, CancellationToken ct) =>
        db.AssessmentSkillSets.Include(x => x.Skills).Include(x => x.Results).ThenInclude(x => x.History).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.AssessmentId == assessmentId, ct);
    // Entities with client-generated keys must be added explicitly; EF would treat them as modified otherwise.
    public void Add(IEnumerable<object> entities) => db.AddRange(entities);
    public void Remove(object entity) => db.Remove(entity);
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
    }
}
