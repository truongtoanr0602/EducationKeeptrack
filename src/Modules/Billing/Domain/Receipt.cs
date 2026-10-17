using System.Text.Json.Serialization;

namespace EducationKeeptrack.Billing.Domain;

public record ReceiptInput(Guid StudentId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal AmountVnd,
    DateOnly ReceivedOn, string? Note, Guid IdempotencyKey, long Version)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (StudentId == Guid.Empty) errors["studentId"] = ["Vui lòng chọn học sinh."];
        if (AmountVnd <= 0 || decimal.Truncate(AmountVnd) != AmountVnd || AmountVnd > 999_999_999_999_999_999m)
            errors["amountVnd"] = ["Số tiền nhận phải là số nguyên đồng dương, tối đa 18 chữ số."];
        if (ReceivedOn == default) errors["receivedOn"] = ["Vui lòng nhập ngày nhận tiền."];
        if (Note?.Length > 1000) errors["note"] = ["Ghi chú tối đa 1000 ký tự."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        if (Version < 0) errors["version"] = ["Phiên bản không hợp lệ."];
        return errors;
    }
}

public sealed class Receipt
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public decimal AmountVnd { get; private set; }
    public DateOnly ReceivedOn { get; private set; }
    public string? Note { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public decimal UnallocatedVnd { get; private set; }
    public List<ReceiptAllocation> Allocations { get; private set; } = [];
    private Receipt() { }
    public Receipt(string owner, Guid classId, ReceiptInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; StudentId = input.StudentId;
        AmountVnd = input.AmountVnd; UnallocatedVnd = input.AmountVnd;
        ReceivedOn = input.ReceivedOn; Note = input.Note?.Trim(); IdempotencyKey = input.IdempotencyKey; RecordedAt = now;
    }
    public bool Matches(ReceiptInput input) => StudentId == input.StudentId && AmountVnd == input.AmountVnd
        && ReceivedOn == input.ReceivedOn && Note == input.Note?.Trim();
    public void Allocate(DateOnly period, decimal debt)
    {
        var amount = Math.Min(UnallocatedVnd, debt);
        if (amount <= 0) return;
        Allocations.Add(new ReceiptAllocation(Id, period, amount)); UnallocatedVnd -= amount;
    }
}

public sealed class ReceiptAllocation
{
    public Guid ReceiptId { get; private set; }
    public DateOnly Period { get; private set; }
    public decimal AmountVnd { get; private set; }
    private ReceiptAllocation() { }
    public ReceiptAllocation(Guid receiptId, DateOnly period, decimal amount) { ReceiptId = receiptId; Period = period; AmountVnd = amount; }
}
