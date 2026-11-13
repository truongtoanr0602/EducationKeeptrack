using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Reporting.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using R2Migrate;

namespace ApiTests;

/// <summary>
/// Cutover and rollback rehearsal with real application exports on the real R2 test bucket. The "old" and "new" stores are two
/// prefixes of that bucket, so this proves the copy, verification, switch-over and rollback logic against the R2 API; it does not
/// exercise SeaweedFS-specific behaviour (plain HTTP, its own path-style quirks).
/// </summary>
public sealed class MigrationRehearsalTests
{
    private static string Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v
        : throw new InvalidOperationException("R2 integration has NOT been verified. Configure TEST_S3_ENDPOINT, TEST_S3_BUCKET, TEST_S3_ACCESS_KEY and TEST_S3_SECRET_KEY.");

    private static string NewPrefix()
    {
        var run = Environment.GetEnvironmentVariable("TEST_S3_RUN_PREFIX");
        return string.IsNullOrEmpty(run) ? $"ekt-tests/{Guid.NewGuid():N}/" : run + $"migration/{Guid.NewGuid():N}/";
    }

    private static S3BlobStore Store(string name, string prefix) => new(name, new StoreSettings
    {
        Endpoint = Env("TEST_S3_ENDPOINT"), Bucket = Env("TEST_S3_BUCKET"), AccessKey = Env("TEST_S3_ACCESS_KEY"), SecretKey = Env("TEST_S3_SECRET_KEY"), KeyPrefix = prefix
    });

    /// <summary>What the application would see after the switch: reads and writes go to whichever store is the target.</summary>
    private sealed class SwitchableStore(IReportObjectStore original) : IReportObjectStore
    {
        public IBlobStore? Target { get; set; }
        public Task PutAsync(string key, byte[] pdf, CancellationToken ct) => Target is null ? original.PutAsync(key, pdf, ct) : Target.PutAsync(key, pdf, "application/pdf", ct);
        public async Task<Stream> OpenAsync(string key, CancellationToken ct)
        {
            if (Target is null) return await original.OpenAsync(key, ct);
            var bytes = await Target.GetAsync(key, ct) ?? throw new ReportStorageException("Không thể tải PDF từ kho riêng tư.");
            return new MemoryStream(bytes);
        }
    }

    private static async Task DeletePrefix(string prefix)
    {
        using var client = new AmazonS3Client(new BasicAWSCredentials(Env("TEST_S3_ACCESS_KEY"), Env("TEST_S3_SECRET_KEY")), new AmazonS3Config
        {
            ServiceURL = Env("TEST_S3_ENDPOINT"), AuthenticationRegion = "auto", ForcePathStyle = true,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED, ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
        while (true)
        {
            var page = await client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Env("TEST_S3_BUCKET"), Prefix = prefix });
            if (page.S3Objects is not { Count: > 0 }) return;
            foreach (var item in page.S3Objects)
            {
                if (!item.Key.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Refusing to clean outside the owned prefix.");
                await client.DeleteObjectAsync(Env("TEST_S3_BUCKET"), item.Key);
            }
        }
    }

    private static async Task<string> DownloadLink(HttpClient client, string path, JsonElement export) =>
        (await (await TestApplication.Post(client, $"{path}/exports/{export.GetProperty("id").GetGuid()}/download-link", new { })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;

    [Fact]
    public async Task Real_exports_move_to_a_new_store_serve_from_it_roll_back_after_new_writes_and_corruption_blocks_publishing()
    {
        SwitchableStore? app = null;
        await using var isolated = new TestApplication { DecorateReportStore = inner => app = new SwitchableStore(inner) };
        await isolated.InitializeAsync();
        var newPrefix = NewPrefix();
        var restorePrefix = NewPrefix();
        using var old = Store("old", isolated.ReportPrefix);
        using var next = Store("new", newPrefix);
        var metadata = new ReportingMetadata(isolated.Connection);
        try
        {
            var (client, path, _, _, email) = await ReportingFixture.Setup(isolated);
            using (client)
            {
                Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft())).StatusCode);
                var (_, first) = await ReportDownloadTests.ExportAndLink(client, path, 1);
                Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft(1, comment: "Bản hai"))).StatusCode);
                var (_, second) = await ReportDownloadTests.ExportAndLink(client, path, 2);

                // 1. Dry-run inventory of the old store: two real references, nothing missing, nothing written.
                var inventory = await Migrator.InventoryAsync(old, metadata, deep: true, default);
                Assert.True(inventory.Clean);
                Assert.Equal(2, inventory.References);
                Assert.Empty(inventory.MissingObjects);
                Assert.Empty(inventory.Orphans);
                Assert.Equal(2, (await Migrator.InventoryAsync(next, metadata, deep: false, default)).MissingObjects.Count);

                // 2. Copy under paused writes; the rerun is idempotent; verification allows publishing.
                await Assert.ThrowsAsync<ToolException>(() => Migrator.CopyAsync(old, next, metadata, false, TimeProvider.System, default));
                var copy = await Migrator.CopyAsync(old, next, metadata, true, isolated.Clock, default);
                Assert.True(copy.Success);
                Assert.Equal(2, copy.Copied);
                var rerun = await Migrator.CopyAsync(old, next, metadata, true, isolated.Clock, default);
                Assert.Equal((0, 2), (rerun.Copied, rerun.AlreadyPresent));
                Assert.True((await Migrator.VerifyAsync(old, next, metadata, default)).RollbackReady);

                // 2b. Independent backup of the old store, restored into a third (scratch) prefix and verified byte for byte.
                var backupDir = Path.Combine(Path.GetTempPath(), "ekt-backup-" + Guid.NewGuid().ToString("N"));
                using var restoredStore = Store("restored", restorePrefix);
                try
                {
                    await Migrator.BackupAsync(old, metadata, backupDir, TimeProvider.System, default);
                    var restore = await Migrator.RestoreAsync(backupDir, restoredStore, default);
                    Assert.True(restore.Success);
                    Assert.Equal(2, restore.Restored);
                    Assert.True((await Migrator.VerifyAsync(old, restoredStore, metadata, default)).RollbackReady);
                }
                finally { if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true); }

                // 3. Switch the application to the new store: the old export downloads with the stored checksum; other owners are refused.
                app!.Target = next;
                var url = await DownloadLink(client, path, first);
                var served = await client.GetByteArrayAsync(url);
                Assert.Equal(first.GetProperty("sha256").GetString(), Migrator.Sha256Hex(served));
                var (other, _, _, _, _) = await ReportingFixture.Setup(isolated);
                using (other) Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);

                // 4. New exports now land in the new store only; the destination stays complete but the old store is behind, so rollback is not ready until the difference is synced back.
                Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft(2, comment: "Sau chuyển"))).StatusCode);
                var (_, third) = await ReportDownloadTests.ExportAndLink(client, path, 3);
                var behind = await Migrator.VerifyAsync(old, next, metadata, default);
                Assert.True(behind.DestinationComplete); Assert.False(behind.RollbackReady);
                Assert.Single(behind.MissingInA);

                // 5. Rollback after new writes: sync the difference back, switch back, and the new export is still downloadable.
                var back = await Migrator.CopyAsync(next, old, metadata, true, isolated.Clock, default);
                Assert.True(back.Success);
                Assert.Equal(1, back.Copied);
                Assert.True((await Migrator.VerifyAsync(old, next, metadata, default)).RollbackReady);
                app.Target = null;
                var rolledBack = await client.GetByteArrayAsync(await DownloadLink(client, path, third));
                Assert.Equal(third.GetProperty("sha256").GetString(), Migrator.Sha256Hex(rolledBack));
                Assert.Equal(second.GetProperty("sha256").GetString(), Migrator.Sha256Hex(await client.GetByteArrayAsync(await DownloadLink(client, path, second))));

                // 6. A corrupted copy blocks publishing and is never silently replaced by a rerun.
                var key = first.GetProperty("id").GetGuid();
                var corruptKey = (await metadata.ReadyExportsAsync(default)).Single(x => x.Id == key).Key;
                await next.PutAsync(corruptKey, Encoding.ASCII.GetBytes("%PDF-corrupt"), "application/pdf", default);
                var blocked = await Migrator.VerifyAsync(old, next, metadata, default);
                Assert.False(blocked.DestinationComplete);
                Assert.Equal([corruptKey], blocked.MismatchedInB);
                var conflict = await Migrator.CopyAsync(old, next, metadata, true, isolated.Clock, default);
                Assert.False(conflict.Success);
                Assert.Equal([corruptKey], conflict.Conflicts);
                Assert.Equal("%PDF-corrupt", Encoding.ASCII.GetString((await next.GetAsync(corruptKey, default))!));

                // 7. A locked account is refused from the old store too (rollback does not widen access).
                using var scope = isolated.Services.CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
                var account = (await users.FindByEmailAsync(email))!;
                account.IsBlocked = true;
                Assert.True((await users.UpdateAsync(account)).Succeeded);
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
            }
        }
        finally { await DeletePrefix(newPrefix); await DeletePrefix(restorePrefix); }
    }
}
