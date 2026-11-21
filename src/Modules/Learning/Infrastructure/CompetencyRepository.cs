using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class CompetencyRepository(LearningDatabase db) : ICompetencyRepository
{
    public async Task<IReadOnlyList<StudentSkillResult>> ListConfirmedAsync(string owner, Guid classId, CancellationToken ct)
    {
        var rows = await db.SkillResults.AsNoTracking().Include(x => x.History).AsSplitQuery()
            .Where(x => x.OwnerId == owner && x.ClassId == classId && x.History.Any(h => h.Action == "confirmed")).ToListAsync(ct);
        var assessments = await db.Assessments.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId)
            .Select(x => new { x.Id, x.Title, x.Date }).ToDictionaryAsync(x => x.Id, ct);
        // Assessments opened from an online exam are graded by the answer key; the rule pools them (ADR-0007).
        var fromExams = (await db.Exams.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId && x.AssessmentId != null)
            .Select(x => x.AssessmentId!.Value).ToListAsync(ct)).ToHashSet();
        var list = new List<StudentSkillResult>();
        foreach (var row in rows)
        {
            if (row.GetFinalResult() is not { } final || !assessments.TryGetValue(row.AssessmentId, out var assessment)) continue;
            list.Add(new(row.StudentId, row.SkillId, new(row.AssessmentId, assessment.Title, assessment.Date, final.Status, final.TaskCount, final.Correct,
                fromExams.Contains(row.AssessmentId) ? ResultSource.Exam : ResultSource.Manual)));
        }
        return list;
    }
}
