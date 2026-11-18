using System.Globalization;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Application;

public record DebtItem(Guid ClassId, string ClassName, string Subject, Guid StudentId, string Name, string TotalDebtVnd, string CurrentDebtVnd, string OldDebtVnd, long Version);
public record DebtClass(Guid ClassId, string ClassName, string Status, long Version);
public record DebtsView(string Month, string TotalDebtVnd, int Count, int Page, int PageSize, IReadOnlyList<DebtClass> Classes, IReadOnlyList<DebtItem> Items);

// Cross-class "Hóa đơn" read: every student of the owner who still owes tuition, largest debt first.
// Debt comes from the same PeriodBalance rules as the per-class payment report, so the figures agree.
public sealed class DebtService(IBillingRepository repository, IClassroomBillingSource classroom)
{
    public const int MaxPageSize = 100;
    public async Task<BillingResult<DebtsView>> ListAsync(string owner, string month, Guid? classId, int page, int pageSize, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)) errors["month"] = ["Tháng không hợp lệ."];
        if (page < 1) errors["page"] = ["Trang phải từ 1."];
        if (pageSize is < 1 or > MaxPageSize) errors["pageSize"] = [$"Kích thước trang từ 1 đến {MaxPageSize}."];
        if (errors.Count > 0) return new(null, BillingFailure.Invalid, Errors: errors);
        var classes = await classroom.ListClassesAsync(owner, ct);
        if (classId is { } only)
        {
            classes = classes.Where(x => x.Id == only).ToArray();
            if (classes.Count == 0) return new(null, BillingFailure.NotFound);
        }
        var items = new List<DebtItem>();
        var states = new List<DebtClass>();
        foreach (var item in classes)
        {
            var scan = await MoneyScanner.ScanAsync(repository, classroom, owner, item.Id, ct);
            if (scan is null) continue;
            var version = scan.Snapshot.Ledger?.Version ?? 0;
            states.Add(new(item.Id, item.Name, scan.Status, version));
            foreach (var student in scan.Source.Students)
            {
                var own = scan.Balances.Where(x => x.StudentId == student.Id).ToArray();
                var total = own.Sum(x => x.Debt);
                if (total <= 0) continue;
                items.Add(new(item.Id, item.Name, item.Subject, student.Id, student.Name, Text(total),
                    Text(own.Where(x => x.Period == first).Sum(x => x.Debt)), Text(own.Where(x => x.Period < first).Sum(x => x.Debt)), version));
            }
        }
        var ordered = items.OrderByDescending(x => Parse(x.TotalDebtVnd)).ThenBy(x => x.Name).ThenBy(x => x.StudentId).ToArray();
        return new(new DebtsView(month, Text(ordered.Sum(x => Parse(x.TotalDebtVnd))), ordered.Length, page, pageSize, states,
            ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray()));
    }
    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
    private static string Text(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
}
