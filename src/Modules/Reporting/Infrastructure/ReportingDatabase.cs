using EducationKeeptrack.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class ReportingDatabase(DbContextOptions<ReportingDatabase> options) : DbContext(options)
{
    public DbSet<MonthlyReportDraft> Drafts => Set<MonthlyReportDraft>();
    public DbSet<ReportPreview> Previews => Set<ReportPreview>();
    public DbSet<ReportExport> Exports => Set<ReportExport>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("reporting");
        var export = model.Entity<ReportExport>(); export.HasKey(x => x.Id); export.Property(x => x.Id).ValueGeneratedNever();
        export.Property(x => x.OwnerId).HasMaxLength(450); export.Property(x => x.Fingerprint).HasMaxLength(64); export.Property(x => x.Sha256).HasMaxLength(64);
        export.Property(x => x.State).HasMaxLength(16); export.Property(x => x.ObjectKey).HasMaxLength(300); export.Property(x => x.ContentType).HasMaxLength(40); export.Property(x => x.TemplateVersion).HasMaxLength(40);
        export.HasIndex(x => new { x.OwnerId, x.IdempotencyKey }).IsUnique(); export.HasIndex(x => new { x.OwnerId, x.ClassId, x.StudentId, x.Month });
        var preview = model.Entity<ReportPreview>(); preview.HasKey(x => x.Id); preview.Property(x => x.Id).ValueGeneratedNever();
        preview.Property(x => x.OwnerId).HasMaxLength(450); preview.HasIndex(x => x.ExpiresAt);
        var draft = model.Entity<MonthlyReportDraft>(); draft.HasKey(x => x.Id); draft.Property(x => x.Id).ValueGeneratedNever();
        draft.Property(x => x.OwnerId).HasMaxLength(450); draft.Property(x => x.Version).IsConcurrencyToken();
        draft.Property(x => x.DisplayName).HasMaxLength(160); draft.Property(x => x.ParentDisplayName).HasMaxLength(160);
        draft.Property(x => x.ContactDisplay).HasMaxLength(500); draft.Property(x => x.Comment).HasMaxLength(10000); draft.Property(x => x.Roadmap).HasMaxLength(10000);
        draft.Property(x => x.Emoji).HasMaxLength(8); draft.HasIndex(x => new { x.OwnerId, x.ClassId, x.StudentId, x.Month }).IsUnique();
    }
}
