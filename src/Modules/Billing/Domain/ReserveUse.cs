using System.Globalization;
using System.Text.Json.Serialization;

namespace EducationKeeptrack.Billing.Domain;

public record ReserveUseInput(Guid StudentId, string? Month,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal AmountVnd, DateOnly? UsedOn, Guid IdempotencyKey, long Version)
{
    public static bool TryPeriod(string? month, out DateOnly period) =>
        DateOnly.TryParseExact((month ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out period);

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (StudentId == Guid.Empty) errors["studentId"] = ["Vui lòng chọn học sinh."];
        if (!TryPeriod(Month, out _)) errors["month"] = ["Chọn kỳ học phí cần khấu trừ (yyyy-MM)."];
        if (AmountVnd <= 0 || decimal.Truncate(AmountVnd) != AmountVnd || AmountVnd > 999_999_999_999_999_999m)
            errors["amountVnd"] = ["Số tiền khấu trừ phải là số nguyên đồng dương, tối đa 18 chữ số."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        if (Version < 0) errors["version"] = ["Phiên bản không hợp lệ."];
        return errors;
    }
}

/// <summary>Reverses a use (<c>reason</c> required) or cancels an approved reserve (also needs the request <c>version</c>).</summary>
public record ReserveCorrectionInput(string? Reason, Guid IdempotencyKey, int Version = 0)
{
    public Dictionary<string, string[]> Validate(bool needsVersion)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Trim().Length > 1000) errors["reason"] = ["Nhập lý do, tối đa 1000 ký tự."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        if (needsVersion && Version < 1) errors["version"] = ["Phiên bản không hợp lệ."];
        return errors;
    }
}

/// <summary>
/// Reserve money applied to one period's remaining fee obligation. It lowers that period's debt but is not money received: no receipt
/// is created, so cashflow is untouched, and the held money stays on its source receipts. A use can be reversed with a reason.
/// </summary>
public sealed class ReserveUse
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly Period { get; private set; }
    public decimal AmountVnd { get; private set; }
    public DateOnly UsedOn { get; private set; }
    public Guid UseKey { get; private set; }
    public string Status { get; private set; } = "active";
    public Guid? ReversalKey { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public List<ReserveUseSource> Sources { get; private set; } = [];
    private ReserveUse() { }
    public ReserveUse(string owner, Guid classId, Guid studentId, DateOnly period, DateOnly usedOn, Guid key, IEnumerable<(Guid RequestId, decimal Amount)> taken, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; StudentId = studentId; Period = period; UsedOn = usedOn; UseKey = key; CreatedAt = now;
        foreach (var (requestId, amount) in taken) Sources.Add(new ReserveUseSource(Id, requestId, amount));
        AmountVnd = Sources.Sum(x => x.AmountVnd);
    }
    public bool Matches(ReserveUseInput input) => StudentId == input.StudentId && AmountVnd == input.AmountVnd
        && ReserveUseInput.TryPeriod(input.Month, out var period) && Period == period;
    public void Reverse(ReserveCorrectionInput input, DateTimeOffset now) { Status = "reversed"; ReversalKey = input.IdempotencyKey; ReversalReason = input.Reason!.Trim(); ReversedAt = now; }
    public bool ReversalMatches(ReserveCorrectionInput input) => Status == "reversed" && ReversalKey == input.IdempotencyKey && ReversalReason == input.Reason?.Trim();
}

/// <summary>Which approved reserve a use drew from, so a reversal gives the money back to the right one.</summary>
public sealed class ReserveUseSource
{
    public Guid UseId { get; private set; }
    public Guid RequestId { get; private set; }
    public decimal AmountVnd { get; private set; }
    private ReserveUseSource() { }
    public ReserveUseSource(Guid useId, Guid requestId, decimal amount) { UseId = useId; RequestId = requestId; AmountVnd = amount; }
}

public record ReserveMovements(decimal Opening, decimal Approved, decimal Used, decimal Cancelled, decimal Carried);

/// <summary>The month-by-month reserve ledger shown in the app and on the PDF: opening, newly approved, used and carried forward.</summary>
public static class ReserveLedger
{
    private static DateOnly VietnamDay(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(TimeSpan.FromHours(7)).DateTime);

    public static ReserveMovements For(Guid studentId, DateOnly month, IEnumerable<ReserveRequest> requests, IEnumerable<ReserveUse> uses)
    {
        var next = month.AddMonths(1);
        var events = new List<(DateOnly Day, decimal Delta, string Kind)>();
        foreach (var e in requests.Where(x => x.StudentId == studentId).SelectMany(x => x.Events))
        {
            if (e.Action == "approved") events.Add((VietnamDay(e.At), e.AmountVnd, "approved"));
            else if (e.Action == "cancelled") events.Add((VietnamDay(e.At), -e.AmountVnd, "cancelled"));
        }
        foreach (var use in uses.Where(x => x.StudentId == studentId && x.Status == "active")) events.Add((use.UsedOn, -use.AmountVnd, "used"));
        var opening = events.Where(x => x.Day < month).Sum(x => x.Delta);
        var inMonth = events.Where(x => x.Day >= month && x.Day < next).ToArray();
        var approved = inMonth.Where(x => x.Kind == "approved").Sum(x => x.Delta);
        var used = -inMonth.Where(x => x.Kind == "used").Sum(x => x.Delta);
        var cancelled = -inMonth.Where(x => x.Kind == "cancelled").Sum(x => x.Delta);
        return new(opening, approved, used, cancelled, opening + approved - used - cancelled);
    }
}
