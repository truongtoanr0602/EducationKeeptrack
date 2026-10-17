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
    public void Hold(decimal excess) => AmountVnd = Math.Max(AmountVnd, excess);
}

public record PeriodBalance(Guid StudentId, DateOnly Period, decimal Fees, decimal Allocated, decimal Held)
{
    public decimal Paid => Math.Max(0, Allocated - Held);
    public decimal Debt => Math.Max(0, Fees - Paid);

    public static PeriodBalance[] Calculate(IReadOnlyList<SessionCharge> charges, IReadOnlyList<Receipt> receipts, IReadOnlyList<PaymentReview> reviews)
    {
        var fees = charges.SelectMany(s => s.Fees.Select(f => new { Key = (f.StudentId, Period: new DateOnly(s.Date.Year, s.Date.Month, 1)), Amount = f.AmountVnd ?? 0 }))
            .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(f => f.Amount));
        var allocated = receipts.SelectMany(r => r.Allocations.Select(a => new { Key = (r.StudentId, a.Period), a.AmountVnd }))
            .GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(a => a.AmountVnd));
        var held = reviews.ToDictionary(x => (x.StudentId, x.Period), x => x.AmountVnd);
        return fees.Keys.Union(allocated.Keys).Union(held.Keys).OrderBy(x => x.Period).ThenBy(x => x.StudentId)
            .Select(key => new PeriodBalance(key.StudentId, key.Period, fees.GetValueOrDefault(key), allocated.GetValueOrDefault(key), held.GetValueOrDefault(key))).ToArray();
    }
}
