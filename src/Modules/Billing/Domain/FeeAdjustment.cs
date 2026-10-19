using System.Globalization;
using System.Text.Json.Serialization;

namespace EducationKeeptrack.Billing.Domain;

public record AdjustmentInput(Guid StudentId, string Month, string Direction,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal AmountVnd,
    string Reason, Guid IdempotencyKey, long? Version)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (StudentId == Guid.Empty) errors["studentId"] = ["Vui lòng chọn học sinh."];
        if (!DateOnly.TryParseExact(Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            errors["month"] = ["Tháng không hợp lệ."];
        if (Direction is not ("increase" or "decrease")) errors["direction"] = ["Chọn tăng hoặc giảm học phí."];
        if (AmountVnd <= 0 || decimal.Truncate(AmountVnd) != AmountVnd || AmountVnd > 999_999_999_999_999_999m)
            errors["amountVnd"] = ["Số tiền phải là số nguyên đồng dương, tối đa 18 chữ số."];
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Length > 1000) errors["reason"] = ["Nhập lý do điều chỉnh, tối đa 1000 ký tự."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        if (Version is null or < 0) errors["version"] = ["Thiếu phiên bản hoặc phiên bản không hợp lệ."];
        return errors;
    }
}

public sealed class FeeAdjustment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public DateOnly Period { get; private set; }
    public string Direction { get; private set; } = "";
    public decimal AmountVnd { get; private set; }
    public string Reason { get; private set; } = "";
    public string RecordedBy { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public long Version { get; private set; }
    [JsonIgnore] public decimal SignedAmount => Direction == "increase" ? AmountVnd : -AmountVnd;
    private FeeAdjustment() { }
    public FeeAdjustment(string owner, Guid classId, AdjustmentInput input, long version, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; StudentId = input.StudentId;
        Period = DateOnly.ParseExact(input.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        Direction = input.Direction; AmountVnd = input.AmountVnd; Reason = input.Reason.Trim();
        RecordedBy = owner; RecordedAt = now; IdempotencyKey = input.IdempotencyKey; Version = version;
    }
    public bool Matches(AdjustmentInput input) => StudentId == input.StudentId
        && Period.ToString("yyyy-MM", CultureInfo.InvariantCulture) == input.Month && Direction == input.Direction
        && AmountVnd == input.AmountVnd && Reason == input.Reason.Trim();
}
