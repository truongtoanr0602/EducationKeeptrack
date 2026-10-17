using EducationKeeptrack.Classroom.Application;
using EducationKeeptrack.Classroom.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class RosterRepository(ClassroomDatabase db) : IRosterRepository
{
    public async Task<IReadOnlyList<Student>> ListStudentsAsync(string owner, CancellationToken ct) =>
        await db.Students.AsNoTracking().Where(x => x.OwnerId == owner).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<Student?> FindStudentAsync(string owner, Guid id, CancellationToken ct) =>
        db.Students.SingleOrDefaultAsync(x => x.OwnerId == owner && x.Id == id, ct);
    public Task<bool> ClassExistsAsync(string owner, Guid classId, CancellationToken ct) =>
        db.Classes.AnyAsync(x => x.OwnerId == owner && x.Id == classId, ct);
    public async Task<IReadOnlyList<EnrollmentWithStudent>> ListEnrollmentsAsync(string owner, Guid classId, CancellationToken ct) =>
        await (from enrollment in db.Enrollments.AsNoTracking().Include(x => x.History)
               join student in db.Students.AsNoTracking() on enrollment.StudentId equals student.Id
               where enrollment.ClassId == classId && enrollment.OwnerId == owner && student.OwnerId == owner
               orderby student.Name, enrollment.StartDate, enrollment.Id
               select new EnrollmentWithStudent(enrollment, student)).ToListAsync(ct);
    public Task<Enrollment?> FindEnrollmentAsync(string owner, Guid classId, Guid id, CancellationToken ct) =>
        db.Enrollments.Include(x => x.History).SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    public Task<bool> HasOverlappingEnrollmentAsync(string owner, Guid classId, Guid studentId, DateOnly startDate, CancellationToken ct) =>
        db.Enrollments.AnyAsync(x => x.OwnerId == owner && x.ClassId == classId && x.StudentId == studentId && (x.EndDate == null || x.EndDate >= startDate), ct);
    public void Add(Student student) => db.Students.Add(student);
    public void Add(Enrollment enrollment) => db.Enrollments.Add(enrollment);
    public async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Enrollments_ClassId_StudentId" or "IX_EnrollmentChange_EnrollmentId_Version" }) { return false; }
    }
    public async Task<ClassroomResult<EnrollmentView>> WithEnrollmentLockAsync(Guid studentId, Func<Task<ClassroomResult<EnrollmentView>>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize creation for this student's periods across concurrent requests/processes.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({studentId.ToString()}, 0))", ct);
        var result = await action();
        if (result.Failure is null) await transaction.CommitAsync(ct);
        return result;
    }
}
