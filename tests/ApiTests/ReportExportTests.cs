using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed class ReportExportTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Export_final_commit_failure_rolls_back_and_retry_keeps_export_id()
    {
        await using var isolated = new TestApplication { ReportingInterceptor = new FailFinalCommit() };
        await isolated.InitializeAsync();
        var (client,path,_,_,email) = await ReportingFixture.Setup(isolated);
        using(client)
        {
            var preview = await (await TestApplication.Post(client,path+"/previews",new{})).Content.ReadFromJsonAsync<JsonElement>();
            var input = new {previewId=preview.GetProperty("id").GetGuid(),draftVersion=0,idempotencyKey=Guid.NewGuid()};
            await Assert.ThrowsAsync<InvalidOperationException>(() => TestApplication.Post(client,path+"/exports",input));
            var exports = await client.GetFromJsonAsync<JsonElement>(path+"/exports");
            Assert.Equal("failed",exports[0].GetProperty("state").GetString());
            var retry = await (await TestApplication.Post(client,path+"/exports",input)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(exports[0].GetProperty("id").GetGuid(),retry.GetProperty("id").GetGuid());
            Assert.Equal("ready",retry.GetProperty("state").GetString());
        }
    }

    private sealed class FailFinalCommit : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        private int failed;
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult> TransactionCommittingAsync(System.Data.Common.DbTransaction transaction,Microsoft.EntityFrameworkCore.Diagnostics.TransactionEventData eventData,Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result,CancellationToken ct=default)
        {
            if(eventData.Context!.ChangeTracker.Entries<EducationKeeptrack.Reporting.Domain.ReportExport>().Any(x=>x.Entity.State=="ready") && Interlocked.Exchange(ref failed,1)==0)
                throw new InvalidOperationException("injected final commit outage");
            return ValueTask.FromResult(result);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_concurrent_requests_and_expired_lease_never_publish_two_results(bool recoverLease)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var isolated = new TestApplication { DecorateReportStore = s => new PausedStore(s,reached,release) };
        await isolated.InitializeAsync();
        var (client,path,_,_,email) = await ReportingFixture.Setup(isolated);
        using(client)
        {
            var preview = await (await TestApplication.Post(client,path+"/previews",new{})).Content.ReadFromJsonAsync<JsonElement>();
            var input = new {previewId=preview.GetProperty("id").GetGuid(),draftVersion=0,idempotencyKey=Guid.NewGuid()};
            var first = TestApplication.Post(client,path+"/exports",input);
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(45));
                Assert.Equal(HttpStatusCode.Conflict,(await TestApplication.Post(client,path+"/exports",input)).StatusCode);
                if(recoverLease)
                {
                    isolated.Clock.Advance(TimeSpan.FromSeconds(121));
                    await using var reopened = isolated.Reopen(); await reopened.InitializeAsync();
                    using var recovered = reopened.Client(); await TestApplication.Login(recovered,email);
                    Assert.Equal(HttpStatusCode.OK,(await TestApplication.Post(recovered,path+"/exports",input)).StatusCode);
                }
            }
            finally { release.TrySetResult(); }
            Assert.Equal(recoverLease ? HttpStatusCode.Conflict : HttpStatusCode.OK,(await first).StatusCode);
            var exports = await client.GetFromJsonAsync<JsonElement>(path+"/exports");
            Assert.Single(exports.EnumerateArray()); Assert.Equal("ready",exports[0].GetProperty("state").GetString());
            isolated.Clock.Advance(TimeSpan.FromMinutes(16));
            var replay = await (await TestApplication.Post(client,path+"/exports",input)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(exports[0].GetProperty("sha256").GetString(),replay.GetProperty("sha256").GetString());
        }
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("profile")]
    [InlineData("recipient")]
    public async Task Export_source_changed_during_upload_does_not_publish(string change)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var isolated = new TestApplication { DecorateReportStore = s => new PausedStore(s,reached,release) };
        await isolated.InitializeAsync();
        var (client,path,_,studentId,_) = await ReportingFixture.Setup(isolated);
        using(client)
        {
            var preview = await (await TestApplication.Post(client,path+"/previews",new{})).Content.ReadFromJsonAsync<JsonElement>();
            var first = TestApplication.Post(client,path+"/exports",new {previewId=preview.GetProperty("id").GetGuid(),draftVersion=0,idempotencyKey=Guid.NewGuid()});
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(45));
                var response = change switch
                {
                    "draft" => await ReportingFixture.Put(client,path+"/draft",ReportingFixture.Draft()),
                    "profile" => await ReportingFixture.Put(client,$"/api/students/{studentId}",new{name="Tên mới",version=1}),
                    _ => await TestApplication.Post(client,"/api/billing/recipient",new{bankBin="970415",bankName="VietinBank",accountNo="123456789",accountName="NGUYEN VAN AN",version=0})
                };
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            }
            finally { release.TrySetResult(); }
            Assert.Equal(HttpStatusCode.Conflict,(await first).StatusCode);
            var exports = await client.GetFromJsonAsync<JsonElement>(path+"/exports");
            Assert.Equal("failed",exports[0].GetProperty("state").GetString());
        }
    }

    private sealed class PausedStore(EducationKeeptrack.Reporting.Application.IReportObjectStore inner,TaskCompletionSource reached,TaskCompletionSource release) : EducationKeeptrack.Reporting.Application.IReportObjectStore
    {
        private int calls;
        public async Task PutAsync(string key,byte[] pdf,CancellationToken ct)
        {
            if(Interlocked.Increment(ref calls)==1) { reached.TrySetResult(); await release.Task.WaitAsync(ct); }
            await inner.PutAsync(key,pdf,ct);
        }
        public Task<Stream> OpenAsync(string key,CancellationToken ct) => inner.OpenAsync(key,ct);
    }
    [Fact]
    public async Task Export_upload_failure_retries_same_id_and_changed_payload_conflicts()
    {
        await using var faulted = new TestApplication { DecorateReportStore = s => new FailOnceStore(s) };
        await faulted.InitializeAsync();
        var (client,path,_,_,_) = await ReportingFixture.Setup(faulted);
        using (client)
        {
            var preview = await (await TestApplication.Post(client,path+"/previews",new{})).Content.ReadFromJsonAsync<JsonElement>();
            var input = new { previewId=preview.GetProperty("id").GetGuid(), draftVersion=0, idempotencyKey=Guid.NewGuid() };
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await TestApplication.Post(client,path+"/exports",input)).StatusCode);
            var before = await client.GetFromJsonAsync<JsonElement>(path+"/exports");
            var retry = await TestApplication.Post(client,path+"/exports",input);
            Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
            var ready = await retry.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(before[0].GetProperty("id").GetGuid(),ready.GetProperty("id").GetGuid());
            var replay = await (await TestApplication.Post(client,path+"/exports",input)).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(ready.GetProperty("sha256").GetString(),replay.GetProperty("sha256").GetString());
            Assert.Equal(HttpStatusCode.Conflict,(await TestApplication.Post(client,path+"/exports",new {input.previewId,draftVersion=1,input.idempotencyKey})).StatusCode);
            Assert.Single((await client.GetFromJsonAsync<JsonElement>(path+"/exports")).EnumerateArray());
        }
    }

    private sealed class FailOnceStore(EducationKeeptrack.Reporting.Application.IReportObjectStore inner) : EducationKeeptrack.Reporting.Application.IReportObjectStore
    {
        private int calls;
        public Task PutAsync(string key,byte[] pdf,CancellationToken ct) => Interlocked.Increment(ref calls)==1
            ? Task.FromException(new EducationKeeptrack.Reporting.Application.ReportStorageException("injected storage outage")) : inner.PutAsync(key,pdf,ct);
        public Task<Stream> OpenAsync(string key,CancellationToken ct) => inner.OpenAsync(key,ct);
    }
    [Fact]
    public async Task Export_creates_private_pdf_with_vietnamese_confirmed_results_finances_and_fallback()
    {
        var (client,path,_,_,_)=await ReportingFixture.Setup(app);
        using(client)
        {
            Assert.Equal(HttpStatusCode.OK,(await ReportingFixture.Put(client,path+"/draft",ReportingFixture.Draft(emoji:"⭐"))).StatusCode);
            var preview=await (await TestApplication.Post(client,path+"/previews",new{})).Content.ReadFromJsonAsync<JsonElement>();
            var response=await TestApplication.Post(client,path+"/exports",new{previewId=preview.GetProperty("id").GetGuid(),draftVersion=1,idempotencyKey=Guid.NewGuid()});
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var export=await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("ready",export.GetProperty("state").GetString()); Assert.True(export.GetProperty("byteLength").GetInt64()>1000);
            var html=await client.GetStringAsync(path+$"/previews/{preview.GetProperty("id").GetGuid()}/html"); Assert.Contains("Tiến bộ về phân số",WebUtility.HtmlDecode(html)); Assert.Contains("Luyện 10 bài",WebUtility.HtmlDecode(html)); Assert.DoesNotContain("<script",html,StringComparison.OrdinalIgnoreCase);
        }
    }
}
