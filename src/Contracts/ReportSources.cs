using System.Security.Cryptography;
using System.Text.Json;

namespace EducationKeeptrack.Contracts;

public record ReportScope(string OwnerId, Guid ClassId, Guid StudentId, DateOnly Month);
public record ReportProfile(string Name, string? ParentName, string? Phone, string? Email);
public record ReportSession(Guid Id, DateOnly Date, TimeOnly StartTime, string SessionStatus, string AttendanceStatus, bool Excused);
public record ClassroomReportSnapshot(string Stamp, string FinancialSourceStamp, string ClassName, string Subject,
    ReportProfile Profile, IReadOnlyList<ReportSession> Sessions);
public interface IClassroomReportSource { Task<ClassroomReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct); }
public static class ReportStamp
{
    public static string Of<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public static string Financial(BillingClassSource source) => Of(source.Sessions.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }).ToArray());
}
