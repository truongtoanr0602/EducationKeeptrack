using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class LearningDatabase(DbContextOptions<LearningDatabase> options) : DbContext(options)
{
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ResultRevision> ResultRevisions => Set<ResultRevision>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("learning");
        var assessment = model.Entity<Assessment>();
        assessment.HasKey(x => x.Id);
        assessment.Property(x => x.Id).ValueGeneratedNever();
        assessment.Property(x => x.OwnerId).HasMaxLength(450);
        assessment.Property(x => x.Title).HasMaxLength(160);
        assessment.Property(x => x.DefaultMethod).HasMaxLength(20);
        assessment.HasIndex(x => new { x.OwnerId, x.ClassId });
        assessment.HasMany(x => x.Submissions).WithOne().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        var submission = model.Entity<Submission>();
        submission.HasKey(x => x.Id);
        submission.Property(x => x.Id).ValueGeneratedNever();
        submission.Property(x => x.StudentName).HasMaxLength(120);
        submission.Property(x => x.Status).HasMaxLength(20);
        submission.Property(x => x.Method).HasMaxLength(20);
        submission.Property(x => x.Version).IsConcurrencyToken();
        submission.HasIndex(x => new { x.AssessmentId, x.StudentId }).IsUnique();
        submission.HasMany(x => x.History).WithOne().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        var revision = model.Entity<ResultRevision>();
        revision.ToTable("ResultRevision");
        revision.HasKey(x => x.Id);
        revision.Property(x => x.Id).ValueGeneratedNever();
        revision.Property(x => x.Status).HasMaxLength(20);
        revision.Property(x => x.Method).HasMaxLength(20);
        revision.Property(x => x.Action).HasMaxLength(20);
        revision.HasIndex(x => new { x.SubmissionId, x.Version }).IsUnique();
        revision.HasIndex(x => x.DispatchedAt);
        revision.Ignore(x => x.IncludedInStatistics);
    }
}
