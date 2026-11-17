using System.Globalization;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Application;

public record MonthMoney(string Month, string FeesVnd, string ReceivedVnd, string DebtVnd, IReadOnlyList<DebtClass> Classes);

// Cross-class money figures for the "Báo cáo" cards. Each figure comes from the per-class service that
// already owns it, so the totals match the class screens: fees and cash received in the month, and the
// debt still open today across all periods.
public sealed class MonthSummaryService(StatisticsService statistics, PaymentService payments, IClassroomBillingSource classroom)
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
            var period = await statistics.PeriodAsync(owner, item.Id, month, ct);
            var cash = await statistics.CashflowAsync(owner, item.Id, first.ToString("yyyy-MM-dd"), last.ToString("yyyy-MM-dd"), ct);
            var open = await payments.ReportAsync(owner, item.Id, month, ct);
            if (period.Failure is not null || cash.Failure is not null || open.Failure is not null) return new(null, BillingFailure.Conflict, "Không thể tổng hợp số liệu của một lớp.");
            fees += Parse(period.Value!.TotalFeesVnd); received += Parse(cash.Value!.TotalReceivedVnd); debt += Parse(open.Value!.TotalDebtVnd);
            states.Add(new(item.Id, item.Name, open.Value.Status, open.Value.Version));
        }
        return new(new MonthMoney(month, Text(fees), Text(received), Text(debt), states));
    }
    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
    private static string Text(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
}
