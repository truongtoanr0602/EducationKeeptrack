using System.Text;
using R2Migrate;

namespace ApiTests;

/// <summary>Rules of the PDF migration tool, on in-memory stores (no network). The R2 rehearsal lives in MigrationRehearsalTests.</summary>
public sealed class MigrationToolTests
{
    private static readonly byte[] PdfA = Encoding.ASCII.GetBytes("%PDF-1.7 export A");
    private static readonly byte[] PdfB = Encoding.ASCII.GetBytes("%PDF-1.7 export B, a bit longer");

    private static ExportRef Ref(string key, byte[] bytes) =>
        new(Guid.NewGuid(), "owner", Guid.NewGuid(), Guid.NewGuid(), "2026-10", key, Migrator.Sha256Hex(bytes).ToLowerInvariant(), bytes.LongLength, "application/pdf");

    private sealed class Fixture
    {
        public InMemoryBlobStore Old = new("old"), New = new("new");
        public ExportRef A = Ref("reports/o/a/1.pdf", PdfA), B = Ref("reports/o/b/1.pdf", PdfB);
        public Fixture() { Old.Set(A.Key, PdfA); Old.Set(B.Key, PdfB); }
        public InMemoryMetadata Metadata(int active = 0) => new([A, B], active);
    }

    [Fact]
    public async Task Inventory_reports_missing_references_orphans_and_size_or_hash_mismatches_without_writing()
    {
        var f = new Fixture();
        f.Old.Set("reports/o/failed/2.pdf", PdfA); // an attempt that never became a ready export
        var clean = await Migrator.InventoryAsync(f.Old, f.Metadata(), deep: true, default);
        Assert.True(clean.Clean);
        Assert.Equal((2, 3), (clean.References, clean.Objects));
        Assert.Equal(["reports/o/failed/2.pdf"], clean.Orphans);
        Assert.Equal(PdfA.LongLength + PdfB.LongLength, clean.ReferencedBytes);

        var empty = await Migrator.InventoryAsync(f.New, f.Metadata(), deep: false, default);
        Assert.False(empty.Clean);
        Assert.Equal(2, empty.MissingObjects.Count);
        Assert.Equal(0, f.New.PutCount);

        f.Old.Set(f.A.Key, Encoding.ASCII.GetBytes("%PDF-1.7 export Z")); // same length, different bytes
        f.Old.Set(f.B.Key, PdfA); // different length
        var shallow = await Migrator.InventoryAsync(f.Old, f.Metadata(), deep: false, default);
        Assert.Equal([f.B.Key], shallow.LengthMismatches);
        Assert.Empty(shallow.HashMismatches);
        var deep = await Migrator.InventoryAsync(f.Old, f.Metadata(), deep: true, default);
        Assert.Equal([f.A.Key], deep.HashMismatches);
        Assert.False(deep.Clean);
    }

    [Fact]
    public async Task Inventory_of_a_store_with_no_exports_is_empty_and_clean()
    {
        var report = await Migrator.InventoryAsync(new InMemoryBlobStore("old"), new InMemoryMetadata([]), deep: true, default);
        Assert.True(report.Clean);
        Assert.Equal((0, 0), (report.References, report.Objects));
    }

    [Fact]
    public async Task Copy_requires_confirmed_paused_writes_and_no_live_export()
    {
        var f = new Fixture();
        var notPaused = await Assert.ThrowsAsync<ToolException>(() => Migrator.CopyAsync(f.Old, f.New, f.Metadata(), false, TimeProvider.System, default));
        Assert.Equal(Migrator.WritesNotPaused, notPaused.Message);
        await Assert.ThrowsAsync<ToolException>(() => Migrator.CopyAsync(f.Old, f.New, f.Metadata(active: 1), true, TimeProvider.System, default));
        Assert.Equal(0, f.New.PutCount);
    }

    [Fact]
    public async Task Copy_verifies_hashes_is_idempotent_and_never_overwrites()
    {
        var f = new Fixture();
        var first = await Migrator.CopyAsync(f.Old, f.New, f.Metadata(), true, TimeProvider.System, default);
        Assert.True(first.Success);
        Assert.Equal((2, 0), (first.Copied, first.AlreadyPresent));
        var again = await Migrator.CopyAsync(f.Old, f.New, f.Metadata(), true, TimeProvider.System, default);
        Assert.True(again.Success);
        Assert.Equal((0, 2), (again.Copied, again.AlreadyPresent));
        Assert.Equal(2, f.New.PutCount); // the rerun wrote nothing

        var wrong = Encoding.ASCII.GetBytes("someone else wrote this");
        f.New.Set(f.A.Key, wrong);
        var conflict = await Migrator.CopyAsync(f.Old, f.New, f.Metadata(), true, TimeProvider.System, default);
        Assert.False(conflict.Success);
        Assert.Equal([f.A.Key], conflict.Conflicts);
        Assert.Equal(wrong, await f.New.GetAsync(f.A.Key, default)); // left exactly as found
    }

    [Fact]
    public async Task Copy_blocks_when_the_source_is_missing_or_fails_its_checksum()
    {
        var f = new Fixture();
        f.Old.Set(f.A.Key, Encoding.ASCII.GetBytes("%PDF-1.7 export Z"));
        var broken = new InMemoryBlobStore("old"); broken.Set(f.A.Key, PdfA); // B is missing entirely
        var corrupt = await Migrator.CopyAsync(f.Old, f.New, f.Metadata(), true, TimeProvider.System, default);
        Assert.False(corrupt.Success);
        Assert.Equal([f.A.Key], corrupt.CorruptAtSource);
        Assert.Equal(1, corrupt.Copied); // the healthy object still moved, the broken one did not
        Assert.Null(await f.New.GetAsync(f.A.Key, default));
        var missing = await Migrator.CopyAsync(broken, new InMemoryBlobStore("new"), f.Metadata(), true, TimeProvider.System, default);
        Assert.Equal([f.B.Key], missing.MissingAtSource);
        Assert.False(missing.Success);
    }

    [Fact]
    public async Task Verify_allows_publishing_only_when_every_reference_is_identical_on_both_sides()
    {
        var f = new Fixture();
        Assert.False((await Migrator.VerifyAsync(f.Old, f.New, f.Metadata(), default)).DestinationComplete);
        await Migrator.CopyAsync(f.Old, f.New, f.Metadata(), true, TimeProvider.System, default);
        f.New.Set("reports/o/new/9.pdf", PdfA);
        var ok = await Migrator.VerifyAsync(f.Old, f.New, f.Metadata(), default);
        Assert.True(ok.RollbackReady);
        Assert.Equal(2, ok.VerifiedBoth);
        Assert.Equal(["reports/o/new/9.pdf"], ok.ExtraInB); // informational: objects not referenced by a ready export
        f.New.Set(f.B.Key, Encoding.ASCII.GetBytes("%PDF-1.7 export B, a bit longer".Replace("longer", "wrong")));
        var bad = await Migrator.VerifyAsync(f.Old, f.New, f.Metadata(), default);
        Assert.False(bad.DestinationComplete);
        Assert.Equal([f.B.Key], bad.MismatchedInB);
    }

    [Fact]
    public async Task Reverse_copy_returns_objects_written_after_cutover_to_the_old_store()
    {
        var f = new Fixture();
        await Migrator.CopyAsync(f.Old, f.New, f.Metadata(), true, TimeProvider.System, default);
        var later = Ref("reports/o/c/1.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 written after the switch"));
        f.New.Set(later.Key, Encoding.ASCII.GetBytes("%PDF-1.7 written after the switch"));
        var metadata = new InMemoryMetadata([f.A, f.B, later]);
        Assert.False((await Migrator.VerifyAsync(f.Old, f.New, metadata, default)).RollbackReady); // old store lacks it
        var back = await Migrator.CopyAsync(f.New, f.Old, metadata, true, TimeProvider.System, default);
        Assert.True(back.Success);
        Assert.Equal((1, 2), (back.Copied, back.AlreadyPresent));
        Assert.True((await Migrator.VerifyAsync(f.Old, f.New, metadata, default)).RollbackReady);
    }

    [Fact]
    public async Task Mixed_state_exports_that_only_exist_on_r2_do_not_block_the_cutover()
    {
        // After #28 an environment holds old exports in SeaweedFS and newer ones only in R2.
        var f = new Fixture();
        var r2Only = Encoding.ASCII.GetBytes("%PDF-1.7 exported after the move to R2");
        var onlyNew = Ref("reports/o/r2/1.pdf", r2Only);
        f.New.Set(onlyNew.Key, r2Only);
        var metadata = new InMemoryMetadata([f.A, f.B, onlyNew]);

        var source = await Migrator.InventoryAsync(f.Old, metadata, deep: true, default, other: f.New);
        Assert.True(source.Clean); // nothing is truly missing
        Assert.Equal([onlyNew.Key], source.OnlyInOther);
        Assert.Empty(source.MissingObjects);
        var without = await Migrator.InventoryAsync(f.Old, metadata, deep: true, default);
        Assert.Equal([onlyNew.Key], without.MissingObjects); // without the other store it cannot tell, so it stays strict

        var copy = await Migrator.CopyAsync(f.Old, f.New, metadata, true, TimeProvider.System, default);
        Assert.True(copy.Success);
        Assert.Equal((2, 1, 0), (copy.Copied, copy.AlreadyPresent, copy.MissingAtSource.Count));

        var verify = await Migrator.VerifyAsync(f.Old, f.New, metadata, default);
        Assert.True(verify.DestinationComplete); // the cutover may proceed
        Assert.False(verify.RollbackReady); // the old store lacks the R2-era export: rollback debt
        Assert.Equal([onlyNew.Key], verify.MissingInA);

        var back = await Migrator.CopyAsync(f.New, f.Old, metadata, true, TimeProvider.System, default);
        Assert.True(back.Success);
        Assert.True((await Migrator.VerifyAsync(f.Old, f.New, metadata, default)).RollbackReady);
    }

    [Fact]
    public async Task Backup_and_restore_round_trip_into_a_scratch_store_and_detect_tampering()
    {
        var f = new Fixture();
        var dir = Path.Combine(Path.GetTempPath(), "ekt-backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var manifest = await Migrator.BackupAsync(f.Old, f.Metadata(), dir, TimeProvider.System, default);
            Assert.Equal(2, manifest.Entries.Count);
            var scratch = new InMemoryBlobStore("scratch");
            var restored = await Migrator.RestoreAsync(dir, scratch, default);
            Assert.True(restored.Success);
            Assert.Equal(2, restored.Restored);
            Assert.True((await Migrator.VerifyAsync(f.Old, scratch, f.Metadata(), default)).RollbackReady);
            File.WriteAllBytes(Path.Combine(dir, "objects", "reports", "o", "a", "1.pdf"), Encoding.ASCII.GetBytes("%PDF-1.7 export Z"));
            var tampered = await Migrator.RestoreAsync(dir, new InMemoryBlobStore("scratch2"), default);
            Assert.False(tampered.Success);
            Assert.Equal([f.A.Key], tampered.Failed);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Backup_refuses_a_reference_whose_bytes_do_not_match_and_unsafe_keys()
    {
        var f = new Fixture();
        f.Old.Set(f.A.Key, Encoding.ASCII.GetBytes("%PDF-1.7 export Z"));
        var dir = Path.Combine(Path.GetTempPath(), "ekt-backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<ToolException>(() => Migrator.BackupAsync(f.Old, f.Metadata(), dir, TimeProvider.System, default));
            var hostile = Ref("../../escape.pdf", PdfA);
            var store = new InMemoryBlobStore("old"); store.Set(hostile.Key, PdfA);
            await Assert.ThrowsAnyAsync<Exception>(() => Migrator.BackupAsync(store, new InMemoryMetadata([hostile]), dir, TimeProvider.System, default));
            Assert.False(File.Exists(Path.Combine(dir, "..", "..", "escape.pdf")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("http://s3:8333", false)]
    [InlineData("https://acct.r2.cloudflarestorage.com/bucket", false)]
    [InlineData("https://user:pw@acct.r2.cloudflarestorage.com", false)]
    public void Destination_must_be_a_plain_https_endpoint(string endpoint, bool allowHttp)
    {
        var settings = new StoreSettings { Endpoint = endpoint, Bucket = "b", AccessKey = "k", SecretKey = "s", AllowHttp = allowHttp };
        Assert.Throws<ToolException>(() => new S3BlobStore("destination", settings));
    }

    [Fact]
    public void Legacy_source_may_use_http_only_with_the_explicit_opt_in()
    {
        var settings = new StoreSettings { Endpoint = "http://s3:8333", Bucket = "ekt-reports", AccessKey = "k", SecretKey = "s", Region = "us-east-1", AllowHttp = true };
        using var store = new S3BlobStore("source", settings);
        Assert.Equal("source", store.Name);
        Assert.Throws<ToolException>(() => new S3BlobStore("source", new StoreSettings { Endpoint = settings.Endpoint, Bucket = "b", AccessKey = "k", SecretKey = "s" }));
    }
}
