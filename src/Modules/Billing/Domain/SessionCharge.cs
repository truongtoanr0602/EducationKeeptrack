using EducationKeeptrack.Contracts;
namespace EducationKeeptrack.Billing.Domain;

public sealed class SessionCharge
{
    public Guid SessionId { get; private set; }
    public Guid ClassId { get; private set; }
    public string OwnerId { get; private set; } = "";
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public string Status { get; private set; } = "";
    public string Kind { get; private set; } = "";
    public int SourceVersion { get; private set; }
    public Guid? RateId { get; private set; }
    public decimal? UnitPriceVnd { get; private set; }
    public List<StudentCharge> Fees { get; private set; } = [];
    private SessionCharge() { }
    public SessionCharge(string owner, Guid classId, Guid sessionId) { OwnerId = owner; ClassId = classId; SessionId = sessionId; }
    public bool Apply(BillingSessionSource source, TuitionRate? rate)
    {
        if (source.Version < SourceVersion) return false;
        var priceAdded = UnitPriceVnd is null && source.Status == "held" && rate is not null;
        if (source.Version == SourceVersion && !priceAdded) return false;
        if (priceAdded) { UnitPriceVnd = rate!.UnitPriceVnd; RateId = rate.Id; }
        Date = source.Date; StartTime = source.StartTime; Status = source.Status; Kind = source.Kind; SourceVersion = source.Version;
        foreach (var mark in source.Attendance)
        {
            var fee = Fees.SingleOrDefault(x => x.StudentId == mark.StudentId);
            if (fee is null) { fee = new StudentCharge(SessionId, mark.StudentId); Fees.Add(fee); }
            fee.Apply(mark, Status == "held" && mark.Status == "present" ? UnitPriceVnd : 0m);
        }
        return true;
    }
}
public sealed class StudentCharge
{
    public Guid SessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public string AttendanceStatus { get; private set; } = "unmarked";
    public int AttendanceVersion { get; private set; }
    public decimal? AmountVnd { get; private set; }
    private StudentCharge() { }
    public StudentCharge(Guid sessionId, Guid studentId) { SessionId = sessionId; StudentId = studentId; }
    public void Apply(BillingAttendanceSource source, decimal? amount) { AttendanceStatus = source.Status; AttendanceVersion = source.Version; AmountVnd = amount; }
}
public sealed class BillingInbox
{
    public Guid EventId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    private BillingInbox() { }
    public BillingInbox(Guid eventId, DateTimeOffset now) { EventId = eventId; ProcessedAt = now; }
}
public sealed class BillingOutbox
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public long Version { get; private set; }
    public string Kind { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    private BillingOutbox() { }
    public BillingOutbox(TuitionLedger ledger, string kind, DateTimeOffset now) { OwnerId = ledger.OwnerId; ClassId = ledger.ClassId; Version = ledger.Version; Kind = kind; RecordedAt = now; }
    public void Acknowledge(DateTimeOffset now) => DispatchedAt = now;
    public TuitionChange View() => new(Id, OwnerId, ClassId, Version, Kind, RecordedAt);
}
