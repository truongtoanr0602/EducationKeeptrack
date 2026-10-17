using System.Text.Json.Serialization;
namespace EducationKeeptrack.Billing.Domain;

public record RateInput(DateOnly EffectiveFrom, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal UnitPriceVnd, int Version)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (EffectiveFrom == default) errors["effectiveFrom"] = ["Vui lòng nhập ngày đơn giá bắt đầu áp dụng."];
        if (UnitPriceVnd < 0 || decimal.Truncate(UnitPriceVnd) != UnitPriceVnd || UnitPriceVnd > 999_999_999_999_999_999m)
            errors["unitPriceVnd"] = ["Đơn giá phải là số nguyên đồng không âm, tối đa 18 chữ số."];
        return errors;
    }
}
public sealed class TuitionLedger
{
    public Guid ClassId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public int RateVersion { get; private set; }
    public long Version { get; private set; }
    public bool NeedsRefresh { get; private set; }
    public List<TuitionRate> Rates { get; private set; } = [];
    private TuitionLedger() { }
    public TuitionLedger(string owner, Guid classId) { OwnerId = owner; ClassId = classId; }
    public void AddRate(RateInput input, DateTimeOffset now)
    {
        RateVersion++; Version++; NeedsRefresh = true;
        Rates.Add(new TuitionRate(ClassId, input.EffectiveFrom, input.UnitPriceVnd, RateVersion, now));
    }
    public TuitionRate? RateOn(DateOnly date) => Rates.Where(x => x.EffectiveFrom <= date).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
    public void RecordProjection() => Version++;
    public void Refreshed() => NeedsRefresh = false;
}
public sealed class TuitionRate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ClassId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public decimal UnitPriceVnd { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    private TuitionRate() { }
    public TuitionRate(Guid classId, DateOnly effectiveFrom, decimal price, int version, DateTimeOffset now) { ClassId = classId; EffectiveFrom = effectiveFrom; UnitPriceVnd = price; Version = version; RecordedAt = now; }
}
