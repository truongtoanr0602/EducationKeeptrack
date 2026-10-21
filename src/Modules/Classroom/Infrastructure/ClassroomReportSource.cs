using System.Data;
using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomReportSource(ClassroomDatabase db, IClassroomBillingSource billingSource) : IClassroomReportSource
{
    public async Task<ClassroomReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct)
    {
        var financial = await billingSource.ReadClassAsync(scope.OwnerId, scope.ClassId, ct);
        if (financial is null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var classroom = await db.Classes.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == scope.OwnerId && x.Id == scope.ClassId, ct);
        var student = await db.Students.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == scope.OwnerId && x.Id == scope.StudentId, ct);
        if (classroom is null || student is null || !await db.Enrollments.AnyAsync(x => x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.StudentId == scope.StudentId, ct)) return null;
        var end = scope.Month.AddMonths(1);
        var eligible = financial.Sessions.Where(s => s.Attendance.Any(a => a.StudentId == scope.StudentId)).Select(s => s.Id).ToArray();
        var sessions = await db.Sessions.AsNoTracking().Where(x => x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.Date >= scope.Month && x.Date < end && eligible.Contains(x.Id)).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ThenBy(x => x.Id).ToArrayAsync(ct);
        var ids = sessions.Select(x => x.Id).ToArray();
        var marks = await db.Attendance.AsNoTracking().Where(x => x.OwnerId == scope.OwnerId && x.StudentId == scope.StudentId && ids.Contains(x.SessionId)).ToArrayAsync(ct);
        var rows = sessions.Select(s => { var mark = marks.SingleOrDefault(x => x.SessionId == s.Id); return new ReportSession(s.Id, s.Date, s.StartTime, s.Status, mark?.Status ?? "unmarked", mark?.Excused ?? false); }).ToArray();
        var stamp = ReportStamp.Of(new { ClassName = classroom.Name, classroom.Subject, student.Version, StudentName = student.Name, student.ParentName, student.Phone, student.Email,
            Sessions = sessions.Select(x => new { x.Id, x.Version }).ToArray(), Marks = marks.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArray(), Financial = ReportStamp.Financial(financial) });
        await tx.CommitAsync(ct);
        return new(stamp, ReportStamp.Financial(financial), classroom.Name, classroom.Subject, new(student.Name, student.ParentName, student.Phone, student.Email), rows);
    }
}
