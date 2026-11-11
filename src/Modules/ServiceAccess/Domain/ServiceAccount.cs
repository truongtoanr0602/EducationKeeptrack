namespace EducationKeeptrack.ServiceAccess.Domain;

// One row per teacher; locked FOR UPDATE to serialise grants, renewals and quota changes.
public sealed class ServiceAccount
{
    public required string AccountId { get; set; }
    public bool AiEnabled { get; set; }
    public uint Version { get; set; }
}

public enum PaymentRequestStatus { Submitted = 1, Approved = 2, Rejected = 3 }

// Service-fee reconciliation request sent by a teacher; never a proof of payment and never tuition.
public sealed class PaymentRequest
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string TransactionCode { get; set; }
    public string Note { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; }
    public PaymentRequestStatus Status { get; set; } = PaymentRequestStatus.Submitted;
    public string? VerificationNote { get; set; }
    public string? DecisionReason { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? PeriodId { get; set; }
    public uint Version { get; set; }
}

// Usage period [StartsAt, EndsAt) with the conditions snapshotted when it was granted.
public sealed class ServicePeriod
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Label { get; set; } = "";
    public Guid? SourceRequestId { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? Reason { get; set; }
    // Quota snapshot per turn type; null means not granted (AI requests of that type are refused).
    public int? GradingQuota { get; set; }
    public int? CommentQuota { get; set; }
    public int? PlanningQuota { get; set; }
    public uint Version { get; set; }
    public bool Contains(DateTimeOffset moment) => StartsAt <= moment && moment < EndsAt;
    public int? QuotaFor(EducationKeeptrack.Contracts.AiTaskKind kind) => kind switch
    {
        EducationKeeptrack.Contracts.AiTaskKind.Grading => GradingQuota,
        EducationKeeptrack.Contracts.AiTaskKind.Comment => CommentQuota,
        EducationKeeptrack.Contracts.AiTaskKind.Planning => PlanningQuota,
        _ => null
    };
}

public enum ReservationStatus { Held = 1, Consumed = 2, Released = 3, Cancelled = 4 }

// Quota ledger row: belongs to the period that accepted it, even if the result arrives after the boundary.
public sealed class QuotaReservation
{
    public Guid Id { get; set; }
    public required string AccountId { get; set; }
    public Guid PeriodId { get; set; }
    public EducationKeeptrack.Contracts.AiTaskKind Kind { get; set; }
    public required string RequestKey { get; set; }
    public int Units { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Held;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SettledAt { get; set; }
}

public sealed class PaymentInstructions
{
    public int Id { get; set; } = 1;
    public string BankDetails { get; set; } = "";
    public string Contact { get; set; } = "";
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public uint Version { get; set; }
}

public sealed class HistoryEntry
{
    public Guid Id { get; set; }
    public string? AccountId { get; set; }
    public required string ActorId { get; set; }
    public required string ActorEmail { get; set; }
    public required string Action { get; set; }
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class Moment
{
    // PostgreSQL stores microseconds; compare and persist at that precision.
    public static DateTimeOffset Of(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % 10, TimeSpan.Zero);
}
