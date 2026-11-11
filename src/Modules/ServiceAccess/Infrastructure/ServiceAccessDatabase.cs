using EducationKeeptrack.ServiceAccess.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Infrastructure;

public sealed class ServiceAccessDatabase(DbContextOptions<ServiceAccessDatabase> options) : DbContext(options)
{
    public DbSet<ServiceAccount> Accounts => Set<ServiceAccount>();
    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
    public DbSet<ServicePeriod> Periods => Set<ServicePeriod>();
    public DbSet<PaymentInstructions> Instructions => Set<PaymentInstructions>();
    public DbSet<HistoryEntry> History => Set<HistoryEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("service_access");
        model.Entity<ServiceAccount>(e =>
        {
            e.ToTable("accounts").HasKey(x => x.AccountId);
            e.Property(x => x.AccountId).HasMaxLength(450);
            e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<PaymentRequest>(e =>
        {
            e.ToTable("payment_requests").HasKey(x => x.Id);
            e.Property(x => x.AccountId).HasMaxLength(450);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.TransactionCode).HasMaxLength(100);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.VerificationNote).HasMaxLength(500);
            e.Property(x => x.DecisionReason).HasMaxLength(500);
            e.Property(x => x.DecidedBy).HasMaxLength(256);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.AccountId, x.IdempotencyKey }).IsUnique();
            // At most one open request per teacher.
            e.HasIndex(x => x.AccountId).IsUnique().HasFilter("\"Status\" = 1").HasDatabaseName("IX_payment_requests_open");
            e.HasOne<ServiceAccount>().WithMany().HasForeignKey(x => x.AccountId);
        });
        model.Entity<ServicePeriod>(e =>
        {
            e.ToTable("periods", t => t.HasCheckConstraint("CK_periods_range", "\"EndsAt\" > \"StartsAt\"")).HasKey(x => x.Id);
            e.Property(x => x.AccountId).HasMaxLength(450);
            e.Property(x => x.Label).HasMaxLength(100);
            e.Property(x => x.CreatedBy).HasMaxLength(256);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.AccountId, x.StartsAt });
            e.HasIndex(x => x.SourceRequestId).IsUnique();
            e.HasOne<ServiceAccount>().WithMany().HasForeignKey(x => x.AccountId);
        });
        model.Entity<PaymentInstructions>(e =>
        {
            e.ToTable("payment_instructions").HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.BankDetails).HasMaxLength(1000);
            e.Property(x => x.Contact).HasMaxLength(500);
            e.Property(x => x.UpdatedBy).HasMaxLength(256);
            e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<HistoryEntry>(e =>
        {
            e.ToTable("history").HasKey(x => x.Id);
            e.Property(x => x.AccountId).HasMaxLength(450);
            e.Property(x => x.ActorId).HasMaxLength(450);
            e.Property(x => x.ActorEmail).HasMaxLength(256);
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.HasIndex(x => new { x.AccountId, x.At });
        });
    }
}
