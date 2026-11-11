using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Application;

public sealed record QuotaBalance(string Kind, int? Granted, int Held, int Used, int Remaining);

// Hold before sending, deduct once on a valid result, return on error/cancel. Every change happens under
// the per-teacher row lock, so concurrent requests cannot exceed the quota and Admin lock/disable cannot race.
public sealed class AiQuotaLedger(ServiceAccessDatabase db, IAccountAccess access, IAccountState accounts, TimeProvider clock) : IAiQuota
{
    public static readonly AiTaskKind[] Kinds = Enum.GetValues<AiTaskKind>();

    public async Task<AiReservation> ReserveAsync(string accountId, AiTaskKind kind, string requestKey, int units, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 100) throw new ArgumentException("Request key must be 1-100 characters.", nameof(requestKey));
        if (units < 1) throw new ArgumentOutOfRangeException(nameof(units));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await db.LockAsync(accountId, ct);
        var existing = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId && x.Kind == kind && x.RequestKey == requestKey, ct);
        if (existing is not null) return new(AiReservationOutcome.Duplicate, existing.Id, null, Name(existing.Status));
        var status = await access.GetAsync(accountId, ct);
        if (status.Status != "active") return Denied(status.Status);
        if (!account.AiEnabled) return Denied("ai_disabled");
        var now = clock.GetUtcNow();
        var period = await db.Periods.AsNoTracking().SingleAsync(x => x.AccountId == accountId && x.StartsAt <= now && x.EndsAt > now, ct);
        var quota = period.QuotaFor(kind);
        if (quota is null) return Denied("no_quota");
        var spent = await Spent(period.Id, kind, ct);
        if (spent + units > quota) return Denied("exhausted");
        var reservation = new QuotaReservation
        {
            Id = Guid.NewGuid(), AccountId = accountId, PeriodId = period.Id, Kind = kind, RequestKey = requestKey,
            Units = units, CreatedAt = now
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(AiReservationOutcome.Reserved, reservation.Id, null, Name(ReservationStatus.Held));
    }

    public async Task<bool> CommitAsync(Guid reservationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservation = await LockedReservation(reservationId, ct);
        if (reservation is null) return false;
        if (reservation.Status == ReservationStatus.Consumed) return true;
        if (reservation.Status != ReservationStatus.Held) return false;
        // Expiry or AI being switched off lets accepted work finish; a locked account voids it.
        var state = await accounts.GetAsync(reservation.AccountId, ct);
        reservation.Status = !state.Exists || state.IsBlocked ? ReservationStatus.Cancelled : ReservationStatus.Consumed;
        reservation.SettledAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return reservation.Status == ReservationStatus.Consumed;
    }

    public async Task ReleaseAsync(Guid reservationId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reservation = await LockedReservation(reservationId, ct);
        if (reservation is not { Status: ReservationStatus.Held }) return;
        reservation.Status = ReservationStatus.Released;
        reservation.SettledAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // Caller holds the account lock (Admin lock flow).
    public Task CancelHeldAsync(string accountId, CancellationToken ct) =>
        db.Reservations.Where(x => x.AccountId == accountId && x.Status == ReservationStatus.Held)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.Status, ReservationStatus.Cancelled).SetProperty(r => r.SettledAt, clock.GetUtcNow()), ct);

    public async Task<IReadOnlyList<QuotaBalance>> BalancesAsync(ServicePeriod? period, CancellationToken ct)
    {
        var rows = period is null ? [] : await db.Reservations.AsNoTracking()
            .Where(x => x.PeriodId == period.Id && (x.Status == ReservationStatus.Held || x.Status == ReservationStatus.Consumed))
            .GroupBy(x => new { x.Kind, x.Status }).Select(g => new { g.Key.Kind, g.Key.Status, Units = g.Sum(x => x.Units) }).ToListAsync(ct);
        return Kinds.Select(kind =>
        {
            var granted = period?.QuotaFor(kind);
            var held = rows.Where(r => r.Kind == kind && r.Status == ReservationStatus.Held).Sum(r => r.Units);
            var used = rows.Where(r => r.Kind == kind && r.Status == ReservationStatus.Consumed).Sum(r => r.Units);
            return new QuotaBalance(KindName(kind), granted, held, used, granted is null ? 0 : Math.Max(0, granted.Value - held - used));
        }).ToList();
    }

    public Task<int> Spent(Guid periodId, AiTaskKind kind, CancellationToken ct) =>
        db.Reservations.Where(x => x.PeriodId == periodId && x.Kind == kind && (x.Status == ReservationStatus.Held || x.Status == ReservationStatus.Consumed))
            .SumAsync(x => x.Units, ct);

    public static string KindName(AiTaskKind kind) => kind.ToString().ToLowerInvariant();

    private async Task<QuotaReservation?> LockedReservation(Guid id, CancellationToken ct)
    {
        var accountId = await db.Reservations.Where(x => x.Id == id).Select(x => x.AccountId).SingleOrDefaultAsync(ct);
        if (accountId is null) return null;
        await db.LockAsync(accountId, ct);
        return await db.Reservations.SingleAsync(x => x.Id == id, ct);
    }

    private static AiReservation Denied(string reason) => new(AiReservationOutcome.Denied, null, reason, null);
    private static string Name(ReservationStatus status) => status.ToString().ToLowerInvariant();
}
