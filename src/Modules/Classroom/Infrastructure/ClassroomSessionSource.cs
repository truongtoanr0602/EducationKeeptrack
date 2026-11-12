using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomSessionSource(ClassroomDatabase db) : IClassroomSessionSource
{
    public async Task<IReadOnlyList<ClassSessionInfo>?> ReadRangeAsync(string owner, Guid classId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!await db.Classes.AnyAsync(x => x.OwnerId == owner && x.Id == classId, ct)) return null;
        return await db.Sessions.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId && x.Date >= from && x.Date <= to)
            .OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id)
            .Select(x => new ClassSessionInfo(x.Id, x.Date, x.StartTime, x.EndTime, x.Status, x.Kind)).ToListAsync(ct);
    }
}
