using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Application;

// One class read once: the Classroom snapshot, the Billing snapshot and the period balances computed from
// them. The cross-class screens (debts, month summary) share this instead of calling every per-class report,
// which would read the same class several times.
internal sealed record ClassMoneyScan(BillingClassSource Source, BillingSnapshot Snapshot, PeriodBalance[] Balances, string Status);

internal static class MoneyScanner
{
    public static async Task<ClassMoneyScan?> ScanAsync(IBillingRepository repository, IClassroomBillingSource classroom, string owner, Guid classId, CancellationToken ct)
    {
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        // A class that has never been priced or paid has no money to report; skip the heavier Classroom read.
        if (snapshot.Ledger is null && snapshot.Sessions.Count == 0 && snapshot.Receipts.Count == 0 && snapshot.Adjustments.Count == 0) return null;
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return null;
        var balances = PeriodBalance.Calculate(snapshot.Sessions, snapshot.Receipts, snapshot.PaymentReviews, snapshot.Adjustments, snapshot.ReserveUses);
        return new(source, snapshot, balances, PaymentService.Status(source, snapshot.Ledger, snapshot.Sessions, snapshot.PaymentReviews));
    }
}
