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
    public void Add(ReportPreview preview) => db.Previews.Add(preview);
    public void Add(ReportExport export) => db.Exports.Add(export);
    public Task<long> DraftVersionAsync(ReportScope scope, CancellationToken ct) => db.Drafts.AsNoTracking().Where(x => x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.StudentId == scope.StudentId && x.Month == scope.Month).Select(x => x.Version).SingleOrDefaultAsync(ct);
    public Task<ReportExport?> ExportByKeyAsync(string owner, Guid key, CancellationToken ct) => db.Exports.SingleOrDefaultAsync(x => x.OwnerId == owner && x.IdempotencyKey == key, ct);
    public Task<ReportExport?> ExportAsync(ReportScope scope, Guid id, CancellationToken ct) => db.Exports.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.StudentId == scope.StudentId && x.Month == scope.Month, ct);
    public async Task<IReadOnlyList<ReportExport>> ExportsAsync(ReportScope scope, CancellationToken ct) => await db.Exports.AsNoTracking().Where(x => x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.StudentId == scope.StudentId && x.Month == scope.Month).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
    public Task<ReportPreview?> PreviewAsync(ReportScope scope, Guid id, CancellationToken ct) => db.Previews.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.StudentId == scope.StudentId && x.Month == scope.Month, ct);
    public void Add(MonthlyReportDraft draft) => db.Drafts.Add(draft);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
