using System.Globalization;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Application;

public record MonthMoney(string Month, string FeesVnd, string ReceivedVnd, string DebtVnd, IReadOnlyList<DebtClass> Classes);

// Cross-class money figures for the "Báo cáo" cards: fees and cash received in the month, and the debt
// still open today across all periods. Computed from one scan per class with the same rules as the
// class screens (period balances; active receipts by received date).
public sealed class MonthSummaryService(IBillingRepository repository, IClassroomBillingSource classroom)
{
    public async Task<BillingResult<MonthMoney>> MonthAsync(string owner, string month, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return new(null, BillingFailure.Invalid, Errors: new() { ["month"] = ["Tháng không hợp lệ (yyyy-MM)."] });
        var last = first.AddMonths(1).AddDays(-1);
        decimal fees = 0, received = 0, debt = 0;
        var states = new List<DebtClass>();
        foreach (var item in await classroom.ListClassesAsync(owner, ct))
        {
            var scan = await MoneyScanner.ScanAsync(repository, classroom, owner, item.Id, ct);
            if (scan is null) continue;
            fees += scan.Balances.Where(x => x.Period == first).Sum(x => x.Fees);
            received += scan.Snapshot.Receipts.Where(x => x.Status == "active" && x.ReceivedOn >= first && x.ReceivedOn <= last).Sum(x => x.AmountVnd);
            debt += scan.Balances.Sum(x => x.Debt);
            states.Add(new(item.Id, item.Name, scan.Status, scan.Snapshot.Ledger?.Version ?? 0));
        }
        return new(new MonthMoney(month, Text(fees), Text(received), Text(debt), states));
    }
    private static string Text(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
}
