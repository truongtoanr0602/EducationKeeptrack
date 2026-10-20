using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EducationKeeptrack.Contracts;

public record ReportScope(string OwnerId, Guid ClassId, Guid StudentId, DateOnly Month);
public record ReportProfile(string Name, string? ParentName, string? Phone, string? Email);
public record ReportSession(Guid Id, DateOnly Date, TimeOnly StartTime, string SessionStatus, string AttendanceStatus, bool Excused);
public record ClassroomReportSnapshot(string Stamp, string FinancialSourceStamp, string ClassName, string Subject,
    ReportProfile Profile, IReadOnlyList<ReportSession> Sessions);
public interface IClassroomReportSource { Task<ClassroomReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct); }
public record ReportAssessment(Guid Id, DateOnly Date, string Title, string Status, int? Completion, int? Correctness, bool HasConfirmedResult);
public record LearningReportSnapshot(string Stamp, IReadOnlyList<ReportAssessment> Assessments);
[JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
public record ReportFinance(decimal OldDebtVnd, decimal PeriodFeesVnd, decimal PeriodPaidVnd, decimal PeriodHeldVnd, decimal PeriodDebtVnd, decimal UnallocatedVnd);
[JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
public record ReportFeeLine(Guid SessionId, DateOnly Date, decimal? UnitPriceVnd, decimal? AmountVnd);
[JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
public record ReportAdjustment(Guid Id, string Direction, decimal AmountVnd, string Reason);
[JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
public record ReportAllocation(Guid ReceiptId, DateOnly ReceivedOn, string Month, decimal AmountVnd);
public record ReportRecipient(string BankBin, string BankName, string AccountNo, string AccountName);
public record BillingReportSnapshot(string Stamp, string FinancialSourceStamp, string Status, long Version, long RecipientVersion, ReportFinance Finance,
    IReadOnlyList<ReportFeeLine> Fees, IReadOnlyList<ReportAdjustment> Adjustments, IReadOnlyList<ReportAllocation> Allocations, ReportRecipient? Recipient);
public record ReportPayment(string Kind, byte[]? QrPng, ReportRecipient? Recipient, string AmountVnd, string Content, IReadOnlyList<string> Warnings);
public interface ILearningReportSource { Task<LearningReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct); }
public interface IBillingReportSource
{
    Task<BillingReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct);
    Task<ReportPayment> PaymentAsync(ReportScope scope, BillingReportSnapshot snapshot, CancellationToken ct);
}
public sealed class ReportSourceChangedException : Exception;
public static class ReportStamp
{
    public static string Of<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static string Financial(BillingClassSource source) => Of(source.Sessions.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArray());
}
