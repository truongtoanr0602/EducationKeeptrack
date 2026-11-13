using System.Security.Cryptography;
using System.Text.Json;
using EducationKeeptrack.Reporting.Infrastructure;

namespace R2Migrate;

/// <summary>The tool's own usage and safety errors. Only these have their messages printed; anything else prints its type name.</summary>
public sealed class ToolException(string message) : Exception(message);

/// <param name="OnlyInOther">References this store lacks but the other store holds (exports written after #28 live only on R2).</param>
public sealed record InventoryReport(string Store, int References, int Objects, long ReferencedBytes, IReadOnlyList<string> MissingObjects,
    IReadOnlyList<string> OnlyInOther, IReadOnlyList<string> Orphans, IReadOnlyList<string> LengthMismatches, IReadOnlyList<string> HashMismatches, bool Deep, bool Clean);

public sealed record CopyReport(string From, string To, int References, int Copied, int AlreadyPresent, IReadOnlyList<string> MissingAtSource,
    IReadOnlyList<string> CorruptAtSource, IReadOnlyList<string> Conflicts, IReadOnlyList<string> Failed, bool Success);

/// <param name="DestinationComplete">Gate for the cutover: every reference is present and byte-identical in B.</param>
/// <param name="RollbackReady">Gate for rollback and for retiring the old store: every reference is identical in both A and B.</param>
public sealed record VerifyReport(string A, string B, int References, int VerifiedBoth, IReadOnlyList<string> MissingInA, IReadOnlyList<string> MissingInB,
    IReadOnlyList<string> MismatchedInA, IReadOnlyList<string> MismatchedInB, IReadOnlyList<string> ExtraInB, bool DestinationComplete, bool RollbackReady);

public sealed record BackupEntry(string Key, long Length, string Sha256, string ContentType, Guid ExportId, string OwnerId, Guid ClassId, Guid StudentId, string Month);
public sealed record BackupManifest(DateTimeOffset CreatedAt, string Source, IReadOnlyList<BackupEntry> Entries);
public sealed record RestoreReport(int Entries, int Restored, int AlreadyPresent, IReadOnlyList<string> Conflicts, IReadOnlyList<string> Failed, bool Success);

/// <summary>
/// Copies and verifies exported PDFs between two object stores. Rules that never bend: SHA-256 is computed over the real bytes
/// (never the ETag) and compared with the export metadata; nothing is ever deleted or overwritten; every written object is read back.
/// </summary>
public static class Migrator
{
    public const string WritesNotPaused = "PDF writes are not paused. Stop exporting, then pass the writes-paused confirmation.";

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool Matches(ExportRef r, byte[] bytes) =>
        bytes.LongLength == r.Length && string.Equals(Sha256Hex(bytes), r.Sha256, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Read-only. Shallow mode compares listed sizes; deep mode downloads each referenced object and checks SHA-256. With
    /// <paramref name="other"/>, a reference this store lacks but the other holds is reported apart from a truly missing one.
    /// </summary>
    public static async Task<InventoryReport> InventoryAsync(IBlobStore store, IMetadataSource metadata, bool deep, CancellationToken ct, IBlobStore? other = null)
    {
        var refs = await metadata.ReadyExportsAsync(ct);
        var listed = (await store.ListAsync(ct)).ToDictionary(x => x.Key, x => x.Length, StringComparer.Ordinal);
        var elsewhere = other is null ? new HashSet<string>() : (await other.ListAsync(ct)).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var referenced = refs.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        List<string> missing = [], onlyInOther = [], lengths = [], hashes = [];
        foreach (var r in refs)
        {
            if (!listed.TryGetValue(r.Key, out var length)) { (elsewhere.Contains(r.Key) ? onlyInOther : missing).Add(r.Key); continue; }
            var lengthOk = length == r.Length;
            if (!lengthOk) lengths.Add(r.Key);
            if (deep && lengthOk)
            {
                var bytes = await store.GetAsync(r.Key, ct);
                if (bytes is null) missing.Add(r.Key); else if (!Matches(r, bytes)) hashes.Add(r.Key);
            }
        }
        var orphans = listed.Keys.Where(k => !referenced.Contains(k)).Order(StringComparer.Ordinal).ToArray();
        // Orphans (failed attempts, leftovers) are reported but never copied; only missing or corrupt references block a cutover.
        var clean = missing.Count == 0 && lengths.Count == 0 && hashes.Count == 0;
        return new(store.Name, refs.Count, listed.Count, refs.Sum(x => x.Length), missing, onlyInOther, orphans, lengths, hashes, deep, clean);
    }

    /// <summary>
    /// Copies every referenced object that <paramref name="to"/> does not already hold verified. The destination is checked first, so
    /// exports that only ever existed there (written after the switch to R2) need no source object. Refuses to start unless the caller
    /// confirms PDF writes are paused and no export holds a live lease.
    /// </summary>
    public static async Task<CopyReport> CopyAsync(IBlobStore from, IBlobStore to, IMetadataSource metadata, bool writesPaused, TimeProvider clock, CancellationToken ct)
    {
        if (!writesPaused) throw new ToolException(WritesNotPaused);
        if (await metadata.ActiveWritersAsync(clock.GetUtcNow(), ct) > 0)
            throw new ToolException("An export is still being produced (live lease). Wait for it or stop the host, then retry.");
        var refs = await metadata.ReadyExportsAsync(ct);
        int copied = 0, present = 0;
        List<string> missing = [], corrupt = [], conflicts = [], failed = [];
        foreach (var r in refs)
        {
            var existing = await to.GetAsync(r.Key, ct);
            if (existing is not null)
            {
                // Never replace what is there: a verified copy is skipped, anything else is a conflict for a human to resolve.
                if (Matches(r, existing)) present++; else conflicts.Add(r.Key);
                continue;
            }
            var bytes = await from.GetAsync(r.Key, ct);
            if (bytes is null) { missing.Add(r.Key); continue; }
            if (!Matches(r, bytes)) { corrupt.Add(r.Key); continue; }
            await to.PutAsync(r.Key, bytes, string.IsNullOrWhiteSpace(r.ContentType) ? "application/pdf" : r.ContentType, ct);
            var readBack = await to.GetAsync(r.Key, ct);
            if (readBack is not null && Matches(r, readBack)) copied++; else failed.Add(r.Key);
        }
        return new(from.Name, to.Name, refs.Count, copied, present, missing, corrupt, conflicts, failed, missing.Count + corrupt.Count + conflicts.Count + failed.Count == 0);
    }

    /// <summary>Read-only. Two gates: the destination alone decides the cutover; both sides decide rollback and retiring the old store.</summary>
    public static async Task<VerifyReport> VerifyAsync(IBlobStore a, IBlobStore b, IMetadataSource metadata, CancellationToken ct)
    {
        var refs = await metadata.ReadyExportsAsync(ct);
        int both = 0; List<string> missingA = [], missingB = [], badA = [], badB = [];
        foreach (var r in refs)
        {
            var left = await a.GetAsync(r.Key, ct); var right = await b.GetAsync(r.Key, ct);
            bool okA = false, okB = false;
            if (left is null) missingA.Add(r.Key); else if (Matches(r, left)) okA = true; else badA.Add(r.Key);
            if (right is null) missingB.Add(r.Key); else if (Matches(r, right)) okB = true; else badB.Add(r.Key);
            if (okA && okB) both++;
        }
        var referenced = refs.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        var extra = (await b.ListAsync(ct)).Select(x => x.Key).Where(k => !referenced.Contains(k)).Order(StringComparer.Ordinal).ToArray();
        return new(a.Name, b.Name, refs.Count, both, missingA, missingB, badA, badB, extra,
            missingB.Count + badB.Count == 0, missingA.Count + missingB.Count + badA.Count + badB.Count == 0);
    }

    /// <summary>Independent backup: object bytes plus a manifest of the metadata fields, in a local directory. Verified bytes only.</summary>
    public static async Task<BackupManifest> BackupAsync(IBlobStore from, IMetadataSource metadata, string directory, TimeProvider clock, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var entries = new List<BackupEntry>();
        foreach (var r in await metadata.ReadyExportsAsync(ct))
        {
            var bytes = await from.GetAsync(r.Key, ct) ?? throw new ToolException($"Referenced object is missing: {r.Key}");
            if (!Matches(r, bytes)) throw new ToolException($"Referenced object fails SHA-256 or length: {r.Key}");
            var file = LocalPath(directory, r.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes, ct);
            entries.Add(new(r.Key, r.Length, r.Sha256, r.ContentType, r.Id, r.OwnerId, r.ClassId, r.StudentId, r.Month));
        }
        var manifest = new BackupManifest(clock.GetUtcNow(), from.Name, entries);
        await File.WriteAllTextAsync(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);
        return manifest;
    }

    /// <summary>Restores a backup into a store (use a scratch prefix for a rehearsal) and verifies every restored object's SHA-256.</summary>
    public static async Task<RestoreReport> RestoreAsync(string directory, IBlobStore to, CancellationToken ct)
    {
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(Path.Combine(directory, "manifest.json"), ct))
            ?? throw new ToolException("Missing backup manifest.");
        int restored = 0, present = 0; List<string> conflicts = [], failed = [];
        foreach (var e in manifest.Entries)
        {
            var bytes = await File.ReadAllBytesAsync(LocalPath(directory, e.Key), ct);
            bool Ok(byte[] x) => x.LongLength == e.Length && string.Equals(Sha256Hex(x), e.Sha256, StringComparison.OrdinalIgnoreCase);
            if (!Ok(bytes)) { failed.Add(e.Key); continue; }
            var existing = await to.GetAsync(e.Key, ct);
            if (existing is not null) { if (Ok(existing)) present++; else conflicts.Add(e.Key); continue; }
            await to.PutAsync(e.Key, bytes, e.ContentType, ct);
            var readBack = await to.GetAsync(e.Key, ct);
            if (readBack is not null && Ok(readBack)) restored++; else failed.Add(e.Key);
        }
        return new(manifest.Entries.Count, restored, present, conflicts, failed, conflicts.Count + failed.Count == 0);
    }

    private static string LocalPath(string directory, string key)
    {
        _ = ReportObjectPrefix.Apply("", key); // rejects traversal and unusual characters
        return Path.Combine(directory, "objects", key.Replace('/', Path.DirectorySeparatorChar));
    }
}
