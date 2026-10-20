using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Domain;
using EducationKeeptrack.Reporting.Application;
using Microsoft.EntityFrameworkCore;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class ReportingRepository(ReportingDatabase db) : IReportingRepository
{
    public Task<MonthlyReportDraft?> DraftAsync(ReportScope scope, CancellationToken ct) => db.Drafts.SingleOrDefaultAsync(x =>
        x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.StudentId == scope.StudentId && x.Month == scope.Month, ct);
    public async Task<T> WithScopeLockAsync<T>(ReportScope scope, Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var key = $"{scope.OwnerId}:{scope.ClassId}:{scope.StudentId}:{scope.Month:yyyy-MM}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 11))", ct);
        var result = await action(); await tx.CommitAsync(ct); return result;
    }
    public void Add(MonthlyReportDraft draft) => db.Drafts.Add(draft);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
