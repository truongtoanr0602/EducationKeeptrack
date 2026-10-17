using System.Data;
using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomLearningSource(ClassroomDatabase db) : IClassroomLearningSource
{
    public async Task<LearningRosterSource?> ReadRosterAsync(string owner, Guid classId, DateOnly date, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await db.Classes.AnyAsync(x => x.OwnerId == owner && x.Id == classId, ct)) return null;
        var ids = await db.Enrollments.Where(x => x.OwnerId == owner && x.ClassId == classId
            && x.StartDate <= date && (x.EndDate == null || x.EndDate >= date))
            .Select(x => x.StudentId).Distinct().ToArrayAsync(ct);
        var students = await db.Students.AsNoTracking().Where(x => x.OwnerId == owner && ids.Contains(x.Id))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new LearningStudentSource(x.Id, x.Name)).ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(classId, students);
    }
}
