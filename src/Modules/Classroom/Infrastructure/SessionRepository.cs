using EducationKeeptrack.Classroom.Application;
using EducationKeeptrack.Classroom.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class SessionRepository(ClassroomDatabase db) : ISessionRepository
{
    public Task<bool> ClassExistsAsync(string owner, Guid classId, CancellationToken ct) => db.Classes.AnyAsync(x => x.Id == classId && x.OwnerId == owner, ct);
    public async Task<IReadOnlyList<WeeklySchedule>> SchedulesAsync(string owner, Guid classId, CancellationToken ct) =>
        await db.WeeklySchedules.AsNoTracking().Include(x => x.Slots).Where(x => x.OwnerId == owner && x.ClassId == classId).OrderBy(x => x.Version).ToListAsync(ct);
    public async Task<IReadOnlyList<ClassSession>> SessionsAsync(string owner, Guid classId, SessionRange range, CancellationToken ct) =>
        await db.Sessions.Where(x => x.OwnerId == owner && x.ClassId == classId && x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ToListAsync(ct);
    public Task<ClassSession?> FindSessionAsync(string owner, Guid classId, Guid id, CancellationToken ct) => db.Sessions.SingleOrDefaultAsync(x => x.Id == id && x.ClassId == classId && x.OwnerId == owner, ct);
    public Task<bool> HasReplacementAsync(string owner, Guid classId, Guid sourceId, CancellationToken ct) => db.Sessions.AnyAsync(x => x.OwnerId == owner && x.ClassId == classId && x.ReplacesSessionId == sourceId && x.Status != "cancelled", ct);
    public async Task<IReadOnlyList<Student>> EligibleStudentsAsync(string owner, Guid classId, DateOnly date, CancellationToken ct) =>
        await db.Students.AsNoTracking().Where(s => s.OwnerId == owner && db.Enrollments.Any(e => e.StudentId == s.Id && e.OwnerId == owner && e.ClassId == classId && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date))).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    public void Add(WeeklySchedule schedule) => db.WeeklySchedules.Add(schedule);
    public void Add(ClassSession session) => db.Sessions.Add(session);
    public void Add(Attendance attendance) => db.Attendance.Add(attendance);
    public void Add(SessionChange change) => db.SessionChanges.Add(change);
    public async Task<IReadOnlyList<AttendanceWithStudent>> AttendanceAsync(string owner, Guid sessionId, CancellationToken ct) =>
        await (from attendance in db.Attendance
               join student in db.Students on attendance.StudentId equals student.Id
               where attendance.OwnerId == owner && attendance.SessionId == sessionId && student.OwnerId == owner
               orderby student.Name, student.Id
               select new AttendanceWithStudent(attendance, student)).ToListAsync(ct);
    public async Task<IReadOnlyList<SessionChange>> ChangesAsync(string owner, Guid sessionId, CancellationToken ct) =>
        await db.SessionChanges.AsNoTracking().Where(x => x.OwnerId == owner && x.SessionId == sessionId).OrderBy(x => x.SessionVersion).ToListAsync(ct);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task<ClassroomResult<T>> WithClassLockAsync<T>(Guid classId, Func<Task<ClassroomResult<T>>> action, CancellationToken ct) where T : class
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({classId.ToString()}, 1))", ct);
        var result = await action();
        if (result.Failure is null) await transaction.CommitAsync(ct);
        return result;
    }
}
