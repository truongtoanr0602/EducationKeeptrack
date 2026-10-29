using System.Security.Cryptography;
using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.Extensions.Options;
using R2Ci;

namespace ApiTests;

public sealed class R2CiStorageTests
{
    [Fact]
    public async Task R2_prefix_cleanup_preserves_other_runs_and_prefixed_downloads_match()
    {
        var config = RunStorage.Configuration();
        var run = Environment.GetEnvironmentVariable("TEST_S3_RUN_PREFIX");
        var state = run is null ? RunState.Create() : new RunState(Guid.ParseExact(run.Split('/')[1], "N"), run);
        var own = $"api/cleanup-{Guid.NewGuid():N}/";
        var other = $"api/control-{Guid.NewGuid():N}/";
        using var storage = new RunStorage(config);
        S3ReportStore Store(string prefix) => new(Options.Create(new ReportingOptions {
            Endpoint = config.Endpoint, Bucket = config.Bucket, AccessKey = config.AccessKey,
            SecretKey = config.SecretKey, KeyPrefix = prefix
        }));
        using var owner = Store(state.Scope(own));
        using var neighbour = Store(state.Scope(other));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var bytes = "%PDF-1.7\nprivate CI fixture"u8.ToArray();
        try
        {
            await owner.PutAsync("api/orphan.pdf", bytes, timeout.Token);
            await owner.PutAsync("browser/orphan.pdf", bytes, timeout.Token);
            await neighbour.PutAsync("browser/keep.pdf", bytes, timeout.Token);
            using (var stream = await owner.OpenAsync("api/orphan.pdf", timeout.Token))
            {
                using var copy = new MemoryStream(); await stream.CopyToAsync(copy, timeout.Token);
                Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(copy.ToArray()));
            }
            var result = await storage.CleanupAsync(state, timeout.Token, own);
            Assert.True(result.Success); Assert.Equal(2, result.Deleted);
            using var kept = await neighbour.OpenAsync("browser/keep.pdf", timeout.Token);
            Assert.Equal(bytes.Length, kept.Length);
            Assert.Equal(0, (await storage.CleanupAsync(state, timeout.Token, own)).Deleted);
        }
        finally
        {
            using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try { await storage.CleanupAsync(state, cleanupTimeout.Token, own); }
            finally { await storage.CleanupAsync(state, cleanupTimeout.Token, other); }
        }
    }
}
