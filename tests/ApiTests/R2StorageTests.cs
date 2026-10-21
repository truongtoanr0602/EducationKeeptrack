using System.Text;

namespace ApiTests;

public sealed class R2StorageTests
{
    [Fact]
    public async Task Preprovisioned_private_bucket_supports_put_get_head_and_delete_without_bucket_admin()
    {
        await using var storage = new ReportTestStorage();
        using var store = (IDisposable)storage.CreateStore();
        var objects = (EducationKeeptrack.Reporting.Application.IReportObjectStore)store;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var bytes = Encoding.UTF8.GetBytes("R2 round trip: Báo cáo tháng — 21/10/2026");
        const string key = "reports/round-trip.pdf";
        await objects.PutAsync(key, bytes, timeout.Token);
        await using var downloaded = await objects.OpenAsync(key, timeout.Token);
        using var result = new MemoryStream();
        await downloaded.CopyToAsync(result, timeout.Token);
        Assert.Equal(bytes, result.ToArray());
        await storage.VerifyObjectAsync(key, bytes.Length, timeout.Token);
    }

    [Fact]
    public async Task Cleaning_one_run_keeps_another_runs_object_in_the_shared_bucket()
    {
        await using var first = new ReportTestStorage();
        await using var second = new ReportTestStorage();
        var firstStore = first.CreateStore();
        var secondStore = second.CreateStore();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        const string key = "reports/same-logical-key.pdf";
        await firstStore.PutAsync(key, [1, 2, 3], timeout.Token);
        await secondStore.PutAsync(key, [4, 5, 6], timeout.Token);

        await first.DisposeAsync();

        await using var remaining = await secondStore.OpenAsync(key, timeout.Token);
        using var result = new MemoryStream();
        await remaining.CopyToAsync(result, timeout.Token);
        Assert.Equal(new byte[] { 4, 5, 6 }, result.ToArray());
    }
}
