using System.Globalization;
using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Application;

public record DebtItem(Guid ClassId, string ClassName, string Subject, Guid StudentId, string Name, string TotalDebtVnd, string CurrentDebtVnd, string OldDebtVnd, long Version);
public record DebtClass(Guid ClassId, string ClassName, string Status, long Version);
public record DebtsView(string Month, string TotalDebtVnd, int Count, int Page, int PageSize, IReadOnlyList<DebtClass> Classes, IReadOnlyList<DebtItem> Items);

// Cross-class "Hóa đơn" read: every student of the owner who still owes tuition, largest debt first.
// It reuses the per-class payment report so the figures can never disagree with the class screen.
public sealed class DebtService(PaymentService payments, IClassroomBillingSource classroom)
{
    public const int MaxPageSize = 100;
    public async Task<BillingResult<DebtsView>> ListAsync(string owner, string month, Guid? classId, int page, int pageSize, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
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
            var report = await payments.ReportAsync(owner, item.Id, month, ct);
            if (report.Failure is not null) return new(null, report.Failure, report.Message, report.Errors);
            states.Add(new(item.Id, item.Name, report.Value!.Status, report.Value.Version));
            items.AddRange(report.Value.Students.Where(s => Parse(s.TotalDebtVnd) > 0)
                .Select(s => new DebtItem(item.Id, item.Name, item.Subject, s.StudentId, s.Name, s.TotalDebtVnd, s.CurrentDebtVnd, s.OldDebtVnd, report.Value.Version)));
        }
        var ordered = items.OrderByDescending(x => Parse(x.TotalDebtVnd)).ThenBy(x => x.Name).ThenBy(x => x.StudentId).ToArray();
        var total = ordered.Sum(x => Parse(x.TotalDebtVnd));
        return new(new DebtsView(month, total.ToString("0", CultureInfo.InvariantCulture), ordered.Length, page, pageSize, states,
            ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray()));
    }
    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
