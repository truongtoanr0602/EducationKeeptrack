using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Classroom.Application;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomDatabase(DbContextOptions<ClassroomDatabase> options) : DbContext(options)
{
    public DbSet<TeacherClass> Classes => Set<TeacherClass>();
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
