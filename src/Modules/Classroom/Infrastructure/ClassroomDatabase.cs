using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Classroom.Application;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomDatabase(DbContextOptions<ClassroomDatabase> options) : DbContext(options)
{
    public DbSet<TeacherClass> Classes => Set<TeacherClass>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("classroom");
        var entity = model.Entity<TeacherClass>();
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.OwnerId);
        entity.Property(x => x.OwnerId).HasMaxLength(450);
        entity.Property(x => x.Name).HasMaxLength(ClassInput.NameLimit);
        entity.Property(x => x.Subject).HasMaxLength(ClassInput.SubjectLimit);
        entity.Property(x => x.Description).HasMaxLength(ClassInput.DescriptionLimit);
        var student = model.Entity<Student>();
        student.HasKey(x => x.Id);
        student.HasIndex(x => x.OwnerId);
        student.Property(x => x.OwnerId).HasMaxLength(450);
        student.Property(x => x.Name).HasMaxLength(120);
        student.Property(x => x.ParentName).HasMaxLength(120);
        student.Property(x => x.Phone).HasMaxLength(30);
        student.Property(x => x.Email).HasMaxLength(254);
        student.Property(x => x.Notes).HasMaxLength(1000);
        student.Property(x => x.Version).IsConcurrencyToken();
        var enrollment = model.Entity<Enrollment>();
        enrollment.HasKey(x => x.Id);
        enrollment.Property(x => x.OwnerId).HasMaxLength(450);
        enrollment.Property(x => x.Status).HasMaxLength(20);
        enrollment.Property(x => x.Version).IsConcurrencyToken();
        enrollment.HasMany(x => x.History).WithOne().HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        var change = model.Entity<EnrollmentChange>();
        change.HasKey(x => x.Id);
        change.Property(x => x.Id).ValueGeneratedNever();
        change.Property(x => x.Status).HasMaxLength(20);
        change.HasIndex(x => new { x.EnrollmentId, x.Version }).IsUnique();
        enrollment.HasIndex(x => new { x.ClassId, x.StudentId }).IsUnique().HasFilter("\"Status\" <> 'stopped'");
        enrollment.HasOne<TeacherClass>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        enrollment.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClassRepository(ClassroomDatabase db) : IClassRepository
{
    public async Task<IReadOnlyList<TeacherClass>> ListAsync(string owner, CancellationToken ct) =>
        await db.Classes.AsNoTracking().Where(x => x.OwnerId == owner).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<TeacherClass?> FindAsync(string owner, Guid id, CancellationToken ct) =>
        db.Classes.SingleOrDefaultAsync(x => x.OwnerId == owner && x.Id == id, ct);
    public async Task SaveAsync(TeacherClass item, CancellationToken ct)
    {
        if (db.Entry(item).State == EntityState.Detached) db.Classes.Add(item);
        await db.SaveChangesAsync(ct);
    }
}
