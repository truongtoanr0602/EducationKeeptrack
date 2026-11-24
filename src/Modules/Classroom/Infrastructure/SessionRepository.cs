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
    public async Task<IReadOnlyList<(string ClassName, WeeklySchedule Schedule)>> ActiveSchedulesAsync(string owner, DateOnly date, CancellationToken ct)
    {
        var names = await db.Classes.AsNoTracking().Where(x => x.OwnerId == owner).Select(x => new { x.Id, x.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var schedules = await db.WeeklySchedules.AsNoTracking().Include(x => x.Slots).Where(x => x.OwnerId == owner && x.EffectiveFrom <= date).ToListAsync(ct);
        return schedules.Where(x => names.ContainsKey(x.ClassId)).GroupBy(x => x.ClassId).Select(g => (names[g.Key], g.OrderByDescending(x => x.Version).First())).ToArray();
    }
    public async Task<IReadOnlyList<SessionWithClass>> SessionsOnAsync(string owner, DateOnly date, CancellationToken ct) =>
        await (from session in db.Sessions.AsNoTracking()
               join classroom in db.Classes.AsNoTracking() on session.ClassId equals classroom.Id
               where session.OwnerId == owner && classroom.OwnerId == owner && session.Date == date
               orderby session.StartTime, classroom.Name, session.Id
               select new SessionWithClass(session, classroom.Name, classroom.Subject)).ToListAsync(ct);
    public async Task<IReadOnlyList<SessionWithClass>> SessionsBetweenAsync(string owner, DateOnly first, DateOnly last, CancellationToken ct) =>
        await (from session in db.Sessions.AsNoTracking()
               join classroom in db.Classes.AsNoTracking() on session.ClassId equals classroom.Id
               where session.OwnerId == owner && classroom.OwnerId == owner && session.Date >= first && session.Date <= last
               orderby session.Date, session.StartTime, classroom.Name, session.Id
               select new SessionWithClass(session, classroom.Name, classroom.Subject)).ToListAsync(ct);
    public async Task<IReadOnlyList<ScheduleRevision>> ScheduleRevisionsAsync(string owner, DateOnly upTo, CancellationToken ct)
    {
        var classes = await db.Classes.AsNoTracking().Where(x => x.OwnerId == owner).Select(x => new { x.Id, x.Name, x.Subject }).ToDictionaryAsync(x => x.Id, ct);
        var schedules = await db.WeeklySchedules.AsNoTracking().Include(x => x.Slots).Where(x => x.OwnerId == owner && x.EffectiveFrom <= upTo).ToListAsync(ct);
        return schedules.Where(x => classes.ContainsKey(x.ClassId)).Select(x => new ScheduleRevision(x.ClassId, classes[x.ClassId].Name, classes[x.ClassId].Subject, x)).ToArray();
    }
    public async Task<IReadOnlyList<EnrollmentSpan>> EnrollmentSpansAsync(string owner, IReadOnlyCollection<Guid> classIds, DateOnly first, DateOnly last, CancellationToken ct) =>
        await db.Enrollments.AsNoTracking().Where(e => e.OwnerId == owner && classIds.Contains(e.ClassId) && e.StartDate <= last && (e.EndDate == null || e.EndDate >= first))
            .Select(e => new EnrollmentSpan(e.ClassId, e.StudentId, e.StartDate, e.EndDate)).ToListAsync(ct);
    public async Task<IReadOnlyList<SessionStatusRow>> StatusesAsync(string owner, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.Sessions.AsNoTracking().Where(x => x.OwnerId == owner && x.Date >= from && x.Date <= to).Select(x => new SessionStatusRow(x.Id, x.Status)).ToListAsync(ct);
    public async Task<IReadOnlyList<MarkRow>> MarksAsync(string owner, IReadOnlyCollection<Guid> sessionIds, CancellationToken ct) =>
        await db.Attendance.AsNoTracking().Where(x => x.OwnerId == owner && sessionIds.Contains(x.SessionId)).Select(x => new MarkRow(x.SessionId, x.StudentId, x.Status)).ToListAsync(ct);
    public async Task<IReadOnlyList<(Guid ClassId, Guid StudentId)>> EligibleAsync(string owner, IReadOnlyCollection<Guid> classIds, DateOnly date, CancellationToken ct) =>
        (await db.Enrollments.AsNoTracking().Where(e => e.OwnerId == owner && classIds.Contains(e.ClassId) && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date)).Select(e => new { e.ClassId, e.StudentId }).ToListAsync(ct))
            .Select(x => (x.ClassId, x.StudentId)).ToArray();
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
