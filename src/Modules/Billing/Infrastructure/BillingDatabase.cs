using System.Data;
using EducationKeeptrack.Billing.Application;
using EducationKeeptrack.Billing.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Billing.Infrastructure;

public sealed class BillingDatabase(DbContextOptions<BillingDatabase> options) : DbContext(options)
{
    public DbSet<TuitionLedger> Ledgers => Set<TuitionLedger>();
    public DbSet<SessionCharge> Charges => Set<SessionCharge>();
    public DbSet<BillingInbox> Inbox => Set<BillingInbox>();
    public DbSet<BillingOutbox> Outbox => Set<BillingOutbox>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("billing");
        var ledger = model.Entity<TuitionLedger>(); ledger.HasKey(x => x.ClassId); ledger.Property(x => x.ClassId).ValueGeneratedNever();
        ledger.Property(x => x.OwnerId).HasMaxLength(450); ledger.Property(x => x.Version).IsConcurrencyToken(); ledger.HasIndex(x => x.NeedsRefresh);
        ledger.HasMany(x => x.Rates).WithOne().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        var rate = model.Entity<TuitionRate>(); rate.HasKey(x => x.Id); rate.Property(x => x.Id).ValueGeneratedNever(); rate.Property(x => x.UnitPriceVnd).HasPrecision(18, 0);
        rate.HasIndex(x => new { x.ClassId, x.EffectiveFrom }).IsUnique(); rate.HasIndex(x => new { x.ClassId, x.Version }).IsUnique();
        var charge = model.Entity<SessionCharge>(); charge.HasKey(x => x.SessionId); charge.Property(x => x.SessionId).ValueGeneratedNever();
        charge.Property(x => x.OwnerId).HasMaxLength(450); charge.Property(x => x.Status).HasMaxLength(20); charge.Property(x => x.Kind).HasMaxLength(20); charge.Property(x => x.UnitPriceVnd).HasPrecision(18, 0);
        charge.HasIndex(x => new { x.ClassId, x.Date }); charge.HasOne<TuitionLedger>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        charge.HasOne<TuitionRate>().WithMany().HasForeignKey(x => x.RateId).OnDelete(DeleteBehavior.Restrict);
        charge.HasMany(x => x.Fees).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        var fee = model.Entity<StudentCharge>(); fee.HasKey(x => new { x.SessionId, x.StudentId }); fee.Property(x => x.AmountVnd).HasPrecision(18, 0); fee.Property(x => x.AttendanceStatus).HasMaxLength(20);
        var inbox = model.Entity<BillingInbox>(); inbox.HasKey(x => x.EventId); inbox.Property(x => x.EventId).ValueGeneratedNever();
        var outbox = model.Entity<BillingOutbox>(); outbox.HasKey(x => x.Id); outbox.Property(x => x.OwnerId).HasMaxLength(450); outbox.Property(x => x.Kind).HasMaxLength(40);
        outbox.HasIndex(x => new { x.ClassId, x.Version }).IsUnique(); outbox.HasIndex(x => x.DispatchedAt);
    }
}
public sealed class BillingRepository(BillingDatabase db) : IBillingRepository
{
    public Task<TuitionLedger?> LedgerAsync(string owner, Guid classId, CancellationToken ct) => db.Ledgers.Include(x => x.Rates).SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
    public async Task<IReadOnlyList<SessionCharge>> ChargesAsync(string owner, Guid classId, CancellationToken ct) => await db.Charges.Include(x => x.Fees).Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
    public Task<bool> ProcessedAsync(Guid eventId, CancellationToken ct) => db.Inbox.AnyAsync(x => x.EventId == eventId, ct);
    public void Add(TuitionLedger ledger) => db.Ledgers.Add(ledger);
    public void Add(SessionCharge charge) => db.Charges.Add(charge);
    public void Add(BillingInbox inbox) => db.Inbox.Add(inbox);
    public void Add(BillingOutbox change) => db.Outbox.Add(change);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
    public async Task<BillingSnapshot> SnapshotAsync(string owner, Guid classId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var ledger = await db.Ledgers.AsNoTracking().Include(x => x.Rates).SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
        var charges = await db.Charges.AsNoTracking().Include(x => x.Fees).Where(x => x.OwnerId == owner && x.ClassId == classId).OrderBy(x => x.Date).ThenBy(x => x.StartTime).ToListAsync(ct);
        await transaction.CommitAsync(ct); return new(ledger, charges);
    }
    public async Task<BillingResult<T>> WithClassLockAsync<T>(Guid classId, Func<Task<BillingResult<T>>> action, CancellationToken ct) where T : class
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({classId.ToString()}, 3))", ct);
        var result = await action();
        if (result.Failure is null) await transaction.CommitAsync(ct);
        return result;
    }
}
