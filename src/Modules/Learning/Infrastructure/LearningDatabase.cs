using EducationKeeptrack.Learning.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class LearningDatabase(DbContextOptions<LearningDatabase> options) : DbContext(options)
{
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ResultRevision> ResultRevisions => Set<ResultRevision>();
    public DbSet<ClassLearningProfile> ClassLearningProfiles => Set<ClassLearningProfile>();
    public DbSet<SkillObservation> SkillObservations => Set<SkillObservation>();
    public DbSet<SkillEvidence> SkillEvidence => Set<SkillEvidence>();
    public DbSet<PersonalizedPlan> PersonalizedPlans => Set<PersonalizedPlan>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("learning");
        var plan = model.Entity<PersonalizedPlan>();
        plan.HasKey(x => x.Id);
        plan.Property(x => x.Id).ValueGeneratedNever();
        plan.Property(x => x.OwnerId).HasMaxLength(450);
        plan.Property(x => x.Title).HasMaxLength(160);
        plan.Property(x => x.Content).HasMaxLength(10000);
        plan.Property(x => x.Status).HasMaxLength(20);
        plan.Property(x => x.Version).IsConcurrencyToken();
        plan.HasIndex(x => new { x.OwnerId, x.ClassId });
        var profile = model.Entity<ClassLearningProfile>();
        profile.HasKey(x => x.Id);
        profile.Property(x => x.Id).ValueGeneratedNever();
        profile.Property(x => x.OwnerId).HasMaxLength(450);
        profile.Property(x => x.Textbook).HasMaxLength(120);
        profile.Property(x => x.Semester).HasMaxLength(120);
        profile.Property(x => x.Version).IsConcurrencyToken();
        profile.HasIndex(x => new { x.OwnerId, x.ClassId }).IsUnique();
        var observation = model.Entity<SkillObservation>();
        observation.HasKey(x => x.Id);
        observation.Property(x => x.Id).ValueGeneratedNever();
        observation.Property(x => x.OwnerId).HasMaxLength(450);
        observation.Property(x => x.Level).HasMaxLength(10);
        observation.Property(x => x.Note).HasMaxLength(500);
        observation.HasIndex(x => new { x.OwnerId, x.ClassId, x.RequestId }).IsUnique();
        observation.HasIndex(x => new { x.OwnerId, x.ClassId, x.StudentId, x.SkillId, x.Date });
        var evidence = model.Entity<SkillEvidence>();
        evidence.HasKey(x => x.Id);
        evidence.Property(x => x.Id).ValueGeneratedNever();
        evidence.Property(x => x.OwnerId).HasMaxLength(450);
        evidence.Property(x => x.Title).HasMaxLength(160);
        evidence.HasIndex(x => new { x.OwnerId, x.ClassId, x.RequestId }).IsUnique();
        evidence.HasIndex(x => new { x.OwnerId, x.ClassId, x.StudentId, x.SkillId, x.Date });
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
