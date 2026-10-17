using System.Data;
using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomBillingSource(IServiceScopeFactory scopes) : IClassroomBillingSource
{
    public Task<BillingClassSource?> ReadClassAsync(string owner, Guid classId, CancellationToken ct) => ReadAsync(owner, classId, null, ct);
    public async Task<BillingSessionSource?> ReadSessionAsync(string owner, Guid classId, Guid sessionId, CancellationToken ct) =>
        (await ReadAsync(owner, classId, sessionId, ct))?.Sessions.SingleOrDefault();

    private async Task<BillingClassSource?> ReadAsync(string owner, Guid classId, Guid? sessionId, CancellationToken ct)
    {
        // A separate scope avoids nesting transactions inside the Classroom outbox transaction.
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomDatabase>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var classroom = await db.Classes.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.Id == classId, ct);
        if (classroom is null) return null;
        var sessions = await db.Sessions.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId && (sessionId == null || x.Id == sessionId)).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToListAsync(ct);
        var ids = sessions.Select(x => x.Id).ToArray();
        var marks = await db.Attendance.AsNoTracking().Where(x => x.OwnerId == owner && ids.Contains(x.SessionId)).ToListAsync(ct);
        var enrollments = await db.Enrollments.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
        var studentIds = enrollments.Select(x => x.StudentId).Concat(marks.Select(x => x.StudentId)).Distinct().ToArray();
        var students = await db.Students.AsNoTracking().Where(x => x.OwnerId == owner && studentIds.Contains(x.Id)).OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new BillingStudentSource(x.Id, x.Name)).ToListAsync(ct);
        var result = sessions.Select(session =>
        {
            var eligible = enrollments.Where(x => x.StartDate <= session.Date && (x.EndDate == null || x.EndDate >= session.Date)).Select(x => x.StudentId)
                .Concat(marks.Where(x => x.SessionId == session.Id).Select(x => x.StudentId)).Distinct();
            var attendance = eligible.Where(id => students.Any(x => x.Id == id)).Select(id =>
            {
                var mark = marks.SingleOrDefault(x => x.SessionId == session.Id && x.StudentId == id);
                return new BillingAttendanceSource(id, mark?.Status ?? "unmarked", mark?.Version ?? 0);
            }).ToArray();
            return new BillingSessionSource(session.Id, session.Date, session.StartTime, session.Status, session.Kind, session.Version, attendance);
        }).ToArray();
        await transaction.CommitAsync(ct);
        return new(classId, classroom.Name, students, result);
    }
}
