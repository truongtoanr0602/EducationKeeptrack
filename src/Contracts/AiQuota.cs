namespace EducationKeeptrack.Contracts;

// Turn types tracked separately per usage period (ADR-0003).
public enum AiTaskKind { Grading = 1, Comment = 2 }

public enum AiReservationOutcome { Reserved, Duplicate, Denied }

// DeniedReason: blocked, unverified, pending, rejected, expired, ai_disabled, no_quota, exhausted.
public record AiReservation(AiReservationOutcome Outcome, Guid? ReservationId, string? DeniedReason, string? Status);

// The only way an AI feature may spend quota. ServiceAccess owns the ledger.
public interface IAiQuota
{
    // Atomically holds `units` turns before the provider is called. The same request key returns the
    // existing reservation instead of holding again.
    Task<AiReservation> ReserveAsync(string accountId, AiTaskKind kind, string requestKey, int units, CancellationToken cancellationToken);

    // Called when a structurally valid result arrives. Deducts at most once. Returns false when the result
    // must not be applied (released, or cancelled because the account was locked).
    Task<bool> CommitAsync(Guid reservationId, CancellationToken cancellationToken);

    // Provider error, invalid output or cancellation: returns held turns to the same period.
    Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);
}
