namespace EducationKeeptrack.Billing.Domain;

// A fee reduction cannot silently turn a historical allocation into reusable credit.
// This hold is durable and only an explicit financial reconciliation may release it.
public sealed class PaymentReview
{
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly Period { get; private set; }
    public string OwnerId { get; private set; } = "";
    public decimal AmountVnd { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    private PaymentReview() { }
    public PaymentReview(string owner, Guid classId, Guid studentId, DateOnly period, decimal amount, DateTimeOffset now)
    { OwnerId = owner; ClassId = classId; StudentId = studentId; Period = period; AmountVnd = amount; RecordedAt = now; }
    public void ReduceTo(decimal amount) => AmountVnd = Math.Min(AmountVnd, amount);
    public void Hold(decimal excess) => AmountVnd = Math.Max(AmountVnd, excess);

    // Existing holds never shrink: a later fee increase must not reuse held money.
    public static IReadOnlyList<PaymentReview> HoldExcess(TuitionLedger ledger, IReadOnlyList<PeriodBalance> balances,
        IReadOnlyList<PaymentReview> reviews, DateTimeOffset now)
    {
        var created = new List<PaymentReview>();
        foreach (var balance in balances)
        {
            var excess = balance.Allocated - balance.Fees;
            if (excess <= 0) continue;
            var review = reviews.SingleOrDefault(x => x.StudentId == balance.StudentId && x.Period == balance.Period);
            if (review is null) created.Add(new PaymentReview(ledger.OwnerId, ledger.ClassId, balance.StudentId, balance.Period, excess, now));
            else review.Hold(excess);
        }
        return created;
    }
}

public record PeriodBalance(Guid StudentId, DateOnly Period, decimal Fees, decimal Allocated, decimal Held)
{
    public decimal Paid => Math.Max(0, Allocated - Held);
    public decimal Debt => Math.Max(0, Fees - Paid);

    public static PeriodBalance[] Calculate(IReadOnlyList<SessionCharge> charges, IReadOnlyList<Receipt> receipts, IReadOnlyList<PaymentReview> reviews, IReadOnlyList<FeeAdjustment> adjustments)
    {
        var fees = charges.SelectMany(s => s.Fees.Select(f => new { Key = (f.StudentId, Period: new DateOnly(s.Date.Year, s.Date.Month, 1)), Amount = f.AmountVnd ?? 0 }))
            .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(f => f.Amount));
        foreach (var adjustment in adjustments)
        {
            var key = (adjustment.StudentId, adjustment.Period);
            fees[key] = fees.GetValueOrDefault(key) + adjustment.SignedAmount;
        }
        var allocated = receipts.Where(r => r.Status == "active").SelectMany(r => r.Allocations.Select(a => new { Key = (r.StudentId, a.Period), a.AmountVnd }))
            .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(a => a.AmountVnd));
        var held = reviews.ToDictionary(x => (x.StudentId, x.Period), x => x.AmountVnd);
        return fees.Keys.Union(allocated.Keys).Union(held.Keys).OrderBy(x => x.Period).ThenBy(x => x.StudentId)
            .Select(key => new PeriodBalance(key.StudentId, key.Period, Math.Max(0, fees.GetValueOrDefault(key)), allocated.GetValueOrDefault(key), held.GetValueOrDefault(key))).ToArray();
    }
}
