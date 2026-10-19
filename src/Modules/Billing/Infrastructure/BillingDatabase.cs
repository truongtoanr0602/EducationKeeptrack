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
    public DbSet<FeeAdjustment> Adjustments => Set<FeeAdjustment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<PaymentReview> PaymentReviews => Set<PaymentReview>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("billing");
        var adjustment = model.Entity<FeeAdjustment>(); adjustment.HasKey(x => x.Id); adjustment.Property(x => x.Id).ValueGeneratedNever();
        adjustment.Ignore(x => x.SignedAmount);
        adjustment.Property(x => x.OwnerId).HasMaxLength(450); adjustment.Property(x => x.RecordedBy).HasMaxLength(450);
        adjustment.Property(x => x.Reason).HasMaxLength(1000); adjustment.Property(x => x.Direction).HasMaxLength(8);
        adjustment.Property(x => x.AmountVnd).HasPrecision(18, 0);
        adjustment.HasIndex(x => new { x.ClassId, x.IdempotencyKey }).IsUnique();
        adjustment.HasIndex(x => new { x.ClassId, x.StudentId, x.Period });
        adjustment.HasOne<TuitionLedger>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        adjustment.ToTable("Adjustments", table =>
        {
            table.HasCheckConstraint("CK_Adjustment_Amount", "\"AmountVnd\" > 0");
            table.HasCheckConstraint("CK_Adjustment_Direction", "\"Direction\" IN ('increase', 'decrease')");
            table.HasCheckConstraint("CK_Adjustment_Reason", "length(btrim(\"Reason\")) > 0");
        });
        var recipient = model.Entity<PaymentRecipient>(); recipient.HasKey(x => x.OwnerId);
        recipient.Property(x => x.OwnerId).HasMaxLength(450);
        recipient.Property(x => x.BankBin).HasMaxLength(6); recipient.Property(x => x.BankName).HasMaxLength(100);
        recipient.Property(x => x.AccountNo).HasMaxLength(19); recipient.Property(x => x.AccountName).HasMaxLength(50);
        recipient.Property(x => x.Version).IsConcurrencyToken();
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
        var receipt = model.Entity<Receipt>(); receipt.HasKey(x => x.Id); receipt.Property(x => x.Id).ValueGeneratedNever();
        receipt.Property(x => x.OwnerId).HasMaxLength(450); receipt.Property(x => x.Note).HasMaxLength(1000);
        receipt.Property(x => x.AmountVnd).HasPrecision(18, 0); receipt.Property(x => x.UnallocatedVnd).HasPrecision(18, 0);
        receipt.HasIndex(x => new { x.ClassId, x.IdempotencyKey }).IsUnique();
        receipt.HasIndex(x => new { x.ClassId, x.StudentId, x.ReceivedOn });
        receipt.HasOne<TuitionLedger>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        receipt.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        receipt.ToTable("Receipts", table =>
        {
            table.HasCheckConstraint("CK_Receipt_Amount", "\"AmountVnd\" > 0");
            table.HasCheckConstraint("CK_Receipt_Unallocated", "\"UnallocatedVnd\" >= 0 AND \"UnallocatedVnd\" <= \"AmountVnd\"");
        });
        var allocation = model.Entity<ReceiptAllocation>(); allocation.HasKey(x => new { x.ReceiptId, x.Period });
        allocation.Property(x => x.AmountVnd).HasPrecision(18, 0);
        allocation.ToTable("ReceiptAllocation", table => table.HasCheckConstraint("CK_Allocation_Amount", "\"AmountVnd\" > 0"));
        var review = model.Entity<PaymentReview>(); review.HasKey(x => new { x.ClassId, x.StudentId, x.Period });
        review.Property(x => x.OwnerId).HasMaxLength(450); review.Property(x => x.AmountVnd).HasPrecision(28, 0);
        review.HasOne<TuitionLedger>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Restrict);
        review.ToTable("PaymentReviews", table => table.HasCheckConstraint("CK_PaymentReview_Amount", "\"AmountVnd\" > 0"));
    }
}
public sealed class BillingRepository(BillingDatabase db) : IBillingRepository
{
    public async Task<IReadOnlyList<FeeAdjustment>> AdjustmentsAsync(string owner, Guid classId, CancellationToken ct) => await db.Adjustments.Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
    public void Add(FeeAdjustment adjustment) => db.Adjustments.Add(adjustment);
    public async Task<IReadOnlyList<Receipt>> ReceiptsAsync(string owner, Guid classId, CancellationToken ct) => await db.Receipts.Include(x => x.Allocations).Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
    public void Add(Receipt receipt) => db.Receipts.Add(receipt);
    public async Task<IReadOnlyList<PaymentReview>> PaymentReviewsAsync(string owner, Guid classId, CancellationToken ct) => await db.PaymentReviews.Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
    public void Add(PaymentReview review) => db.PaymentReviews.Add(review);
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
        var receipts = await db.Receipts.AsNoTracking().Include(x => x.Allocations).Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
        var reviews = await db.PaymentReviews.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
        var adjustments = await db.Adjustments.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).ToListAsync(ct);
        await transaction.CommitAsync(ct); return new(ledger, charges, receipts, reviews, adjustments);
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
