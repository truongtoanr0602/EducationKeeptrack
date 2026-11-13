using System.Text.Json.Serialization;

namespace EducationKeeptrack.Billing.Domain;

public record ReserveRequestInput(Guid StudentId, DateOnly AbsentFrom, DateOnly AbsentTo, string? Reason, string? EvidenceNote,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal AmountVnd, Guid IdempotencyKey)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (StudentId == Guid.Empty) errors["studentId"] = ["Vui lòng chọn học sinh."];
        if (AbsentFrom == default || AbsentTo == default || AbsentTo < AbsentFrom || AbsentTo.DayNumber - AbsentFrom.DayNumber > 366)
            errors["absent"] = ["Nhập khoảng ngày nghỉ hợp lệ (từ ngày không sau đến ngày, tối đa 366 ngày)."];
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Trim().Length > 1000) errors["reason"] = ["Nhập lý do nghỉ, tối đa 1000 ký tự."];
        if (EvidenceNote?.Length > 1000) errors["evidenceNote"] = ["Ghi chú minh chứng tối đa 1000 ký tự."];
        if (AmountVnd <= 0 || decimal.Truncate(AmountVnd) != AmountVnd || AmountVnd > 999_999_999_999_999_999m)
            errors["amountVnd"] = ["Số tiền đề nghị bảo lưu phải là số nguyên đồng dương, tối đa 18 chữ số."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        return errors;
    }
}

public record ReserveDecisionInput(string Decision, string? Reason, Guid IdempotencyKey, int Version)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Decision is not ("approve" or "reject")) errors["decision"] = ["Chọn duyệt hoặc từ chối."];
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Trim().Length > 1000) errors["reason"] = ["Nhập lý do quyết định, tối đa 1000 ký tự."];
        if (IdempotencyKey == Guid.Empty) errors["idempotencyKey"] = ["Thiếu khóa chống gửi lặp."];
        if (Version < 1) errors["version"] = ["Phiên bản không hợp lệ."];
        return errors;
    }
}

/// <summary>
/// A request, recorded by the teacher on a family's behalf, to hold part of the money a student paid but did not use. No policy decides
/// eligibility (minimum days, evidence, expiry, class transfer, refund): the teacher decides each case with a written reason, and the
/// system only enforces that the money exists, is unused and is not already held by another reserve.
/// </summary>
public sealed class ReserveRequest
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly AbsentFrom { get; private set; }
    public DateOnly AbsentTo { get; private set; }
    public string Reason { get; private set; } = "";
    public string? EvidenceNote { get; private set; }
    public decimal RequestedVnd { get; private set; }
    public string Status { get; private set; } = "pending";
    public int Version { get; private set; } = 1;
    public Guid CreateKey { get; private set; }
    public Guid? DecisionKey { get; private set; }
    public string? DecisionReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public List<ReserveSource> Sources { get; private set; } = [];
    public List<ReserveEvent> Events { get; private set; } = [];
    private ReserveRequest() { }
    public ReserveRequest(string owner, Guid classId, ReserveRequestInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; StudentId = input.StudentId; AbsentFrom = input.AbsentFrom; AbsentTo = input.AbsentTo;
        Reason = input.Reason!.Trim(); EvidenceNote = string.IsNullOrWhiteSpace(input.EvidenceNote) ? null : input.EvidenceNote.Trim();
        RequestedVnd = input.AmountVnd; CreateKey = input.IdempotencyKey; CreatedAt = now;
        Events.Add(new ReserveEvent(Id, 1, "requested", Reason, owner, input.AmountVnd, now));
    }
    public bool Matches(ReserveRequestInput input) => StudentId == input.StudentId && AbsentFrom == input.AbsentFrom && AbsentTo == input.AbsentTo
        && RequestedVnd == input.AmountVnd && Reason == input.Reason?.Trim() && EvidenceNote == (string.IsNullOrWhiteSpace(input.EvidenceNote) ? null : input.EvidenceNote.Trim());

    /// <summary>Moves the money: the caller has already checked that each receipt can give the stated amount.</summary>
    public void Approve(IEnumerable<(Receipt Receipt, decimal Amount)> taken, ReserveDecisionInput input, string actor, DateTimeOffset now)
    {
        foreach (var (receipt, amount) in taken)
        {
            receipt.Reserve(amount);
            Sources.Add(new ReserveSource(Id, receipt.Id, amount));
        }
        Decide("approved", "approved", input, actor, RequestedVnd, now);
    }
    public void Reject(ReserveDecisionInput input, string actor, DateTimeOffset now) => Decide("rejected", "rejected", input, actor, 0, now);

    private void Decide(string status, string action, ReserveDecisionInput input, string actor, decimal amount, DateTimeOffset now)
    {
        Status = status; DecisionKey = input.IdempotencyKey; DecisionReason = input.Reason!.Trim(); DecidedAt = now; Version++;
        Events.Add(new ReserveEvent(Id, Version, action, DecisionReason, actor, amount, now));
    }
    public bool DecisionMatches(ReserveDecisionInput input) => DecisionKey == input.IdempotencyKey && DecisionReason == input.Reason?.Trim()
        && Status == (input.Decision == "approve" ? "approved" : "rejected");
}

/// <summary>The real money a reserve was taken from: which receipt, how much.</summary>
public sealed class ReserveSource
{
    public Guid RequestId { get; private set; }
    public Guid ReceiptId { get; private set; }
    public decimal AmountVnd { get; private set; }
    private ReserveSource() { }
    public ReserveSource(Guid requestId, Guid receiptId, decimal amount) { RequestId = requestId; ReceiptId = receiptId; AmountVnd = amount; }
}

public sealed class ReserveEvent
{
    public Guid RequestId { get; private set; }
    public int Version { get; private set; }
    public string Action { get; private set; } = "";
    public string Reason { get; private set; } = "";
    public string ActorId { get; private set; } = "";
    public decimal AmountVnd { get; private set; }
    public DateTimeOffset At { get; private set; }
    private ReserveEvent() { }
    public ReserveEvent(Guid requestId, int version, string action, string reason, string actor, decimal amount, DateTimeOffset at)
    { RequestId = requestId; Version = version; Action = action; Reason = reason; ActorId = actor; AmountVnd = amount; At = at; }
}
