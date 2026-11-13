using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace R2Migrate;

/// <summary>A ready export: the only kind of row that references a stored PDF.</summary>
public sealed record ExportRef(Guid Id, string OwnerId, Guid ClassId, Guid StudentId, string Month, string Key, string Sha256, long Length, string ContentType);

public interface IMetadataSource
{
    Task<IReadOnlyList<ExportRef>> ReadyExportsAsync(CancellationToken ct);
    /// <summary>Exports that are being produced right now (live lease); PDF writes are not paused while this is above zero.</summary>
    Task<int> ActiveWritersAsync(DateTimeOffset now, CancellationToken ct);
}

public sealed class InMemoryMetadata(IEnumerable<ExportRef> refs, int activeWriters = 0) : IMetadataSource
{
    public Task<IReadOnlyList<ExportRef>> ReadyExportsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ExportRef>>(refs.ToArray());
    public Task<int> ActiveWritersAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(activeWriters);
}

/// <summary>Reads the Reporting schema read-only through its own context; failed or pending attempts are not references.</summary>
public sealed class ReportingMetadata(string connection) : IMetadataSource
{
    private ReportingDatabase Open() => new(new DbContextOptionsBuilder<ReportingDatabase>()
        .UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "reporting"))
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options);

    public async Task<IReadOnlyList<ExportRef>> ReadyExportsAsync(CancellationToken ct)
    {
        await using var db = Open();
        var rows = await db.Exports.Where(x => x.State == "ready" && x.ObjectKey != null).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        return rows.Select(x => new ExportRef(x.Id, x.OwnerId, x.ClassId, x.StudentId, x.Month.ToString("yyyy-MM"), x.ObjectKey!, x.Sha256 ?? "", x.ByteLength, x.ContentType)).ToArray();
    }

    public async Task<int> ActiveWritersAsync(DateTimeOffset now, CancellationToken ct)
    {
        await using var db = Open();
        return await db.Exports.CountAsync(x => x.State == "pending" && x.LeaseUntil > now, ct);
    }
}
