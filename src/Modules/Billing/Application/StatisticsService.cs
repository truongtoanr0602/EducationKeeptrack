using System.Globalization;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Billing.Domain;

namespace EducationKeeptrack.Billing.Application;

public record CashflowReceiptView(string StudentName, ReceiptView Receipt);
public record CashflowView(DateOnly From, DateOnly To, string TimeZone, long Version, string Status,
    string TotalReceivedVnd, string TotalUnallocatedVnd, IReadOnlyList<CashflowReceiptView> Receipts);
public record PeriodReceiptAllocation(Guid ReceiptId, DateOnly ReceivedOn, string AmountVnd, string? Note);
public record PeriodStudentView(Guid StudentId, string Name, string FeesVnd, string AllocatedVnd, string PaidVnd,
    string HeldVnd, string DebtVnd, string Status, IReadOnlyList<PeriodReceiptAllocation> Allocations);
public record PeriodStatisticsView(string Month, long Version, string Status, string TotalFeesVnd,
    string TotalPaidVnd, string TotalHeldVnd, string TotalDebtVnd, IReadOnlyList<PeriodStudentView> Students);

public sealed class StatisticsService(IBillingRepository repository, IClassroomBillingSource classroom)
{
    public async Task<BillingResult<CashflowView>> CashflowAsync(string owner, Guid classId, string from, string to, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return new(null, BillingFailure.NotFound);
        if (!TryDate(from, out var first) || !TryDate(to, out var last) || first > last)
            return new(null, BillingFailure.Invalid, Errors: new() { ["range"] = ["Nhập khoảng ngày thực nhận hợp lệ (yyyy-MM-dd), từ ngày không sau đến ngày."] });
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        var receipts = snapshot.Receipts.Where(x => x.Status == "active" && x.ReceivedOn >= first && x.ReceivedOn <= last)
            .OrderBy(x => x.ReceivedOn).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id).ToArray();
        var names = source.Students.ToDictionary(x => x.Id, x => x.Name);
        return new(new CashflowView(first, last, "Asia/Ho_Chi_Minh", snapshot.Ledger?.Version ?? 0,
            PaymentService.Status(source, snapshot.Ledger, snapshot.Sessions, snapshot.PaymentReviews),
            Money(receipts.Sum(x => x.AmountVnd)), Money(receipts.Sum(x => x.UnallocatedVnd)),
            receipts.Select(x => new CashflowReceiptView(names.GetValueOrDefault(x.StudentId, "Học sinh"), PaymentService.View(x))).ToArray()));
    }

    public async Task<BillingResult<PeriodStatisticsView>> PeriodAsync(string owner, Guid classId, string month, CancellationToken ct)
    {
        var source = await classroom.ReadClassAsync(owner, classId, ct);
        if (source is null) return new(null, BillingFailure.NotFound);
        if (!TryDate(month + "-01", out var period))
            return new(null, BillingFailure.Invalid, Errors: new() { ["month"] = ["Kỳ nghĩa vụ không hợp lệ (yyyy-MM)."] });
        var snapshot = await repository.SnapshotAsync(owner, classId, ct);
        var balances = PeriodBalance.Calculate(snapshot.Sessions, snapshot.Receipts, snapshot.PaymentReviews, snapshot.Adjustments)
            .Where(x => x.Period == period).ToDictionary(x => x.StudentId);
        var students = source.Students.Select(student =>
        {
            var balance = balances.GetValueOrDefault(student.Id) ?? new PeriodBalance(student.Id, period, 0, 0, 0);
            var allocations = snapshot.Receipts.Where(x => x.Status == "active" && x.StudentId == student.Id)
                .OrderBy(x => x.ReceivedOn).ThenBy(x => x.RecordedAt).ThenBy(x => x.Id)
                .SelectMany(x => x.Allocations.Where(a => a.Period == period)
                    .Select(a => new PeriodReceiptAllocation(x.Id, x.ReceivedOn, Money(a.AmountVnd), x.Note))).ToArray();
            return new PeriodStudentView(student.Id, student.Name, Money(balance.Fees), Money(balance.Allocated), Money(balance.Paid),
                Money(balance.Held), Money(balance.Debt), balance.Debt == 0 ? "paid" : balance.Paid > 0 ? "partial" : "unpaid", allocations);
        }).ToArray();
        return new(new PeriodStatisticsView(month, snapshot.Ledger?.Version ?? 0,
            PaymentService.Status(source, snapshot.Ledger, snapshot.Sessions, snapshot.PaymentReviews),
            Money(balances.Values.Sum(x => x.Fees)), Money(balances.Values.Sum(x => x.Paid)),
            Money(balances.Values.Sum(x => x.Held)), Money(balances.Values.Sum(x => x.Debt)), students));
    }

    private static bool TryDate(string value, out DateOnly date) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) && date != default;
    private static string Money(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);
}
