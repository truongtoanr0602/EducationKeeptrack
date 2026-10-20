using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed class ReportPreviewTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Preview_finance_does_not_collect_future_period_and_retains_source_version()
    {
        var (client,path,classId,studentId,_)=await ReportingFixture.Setup(app);
        using(client)
        {
            var billing=$"/api/billing/classes/{classId}";
            foreach(var (month,amount,version) in new[]{("10","500000",0),("11","900000",1),("12","300000",2)})
            {
                Assert.Equal(HttpStatusCode.Created,(await TestApplication.Post(client,billing+"/rates",new{effectiveFrom=$"2026-{month}-01",unitPriceVnd=amount,version})).StatusCode);
                await ReportingFixture.Session(client,classId,studentId,$"2026-{month}-02"); await ReportingFixture.Payments(client,classId);
            }
            var payments=await ReportingFixture.Payments(client,classId);
            Assert.Equal(HttpStatusCode.Created,(await TestApplication.Post(client,billing+"/payments",new{studentId,amountVnd="700000",receivedOn="2026-11-05",idempotencyKey=Guid.NewGuid(),version=payments.GetProperty("version").GetInt64()})).StatusCode);
            var response=await TestApplication.Post(client,path.Replace("2026-10","2026-11")+"/previews",new{}); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var preview=await response.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal("700000",preview.GetProperty("totalDueVnd").GetString());
            var finance=preview.GetProperty("sources").GetProperty("billing").GetProperty("finance");
            Assert.Equal("0",finance.GetProperty("oldDebtVnd").GetString()); Assert.Equal("900000",finance.GetProperty("periodFeesVnd").GetString());
            Assert.Equal("200000",finance.GetProperty("periodPaidVnd").GetString()); Assert.Equal("700000",finance.GetProperty("periodDebtVnd").GetString());
        }
    }
    [Fact]
    public async Task Preview_rejects_changed_sources_pending_fees_and_missing_rate()
    {
        var (client,path,classId,studentId,_)=await ReportingFixture.Setup(app);
        using(client)
        {
            await ReportingFixture.Session(client,classId,studentId,"2026-10-03");
            Assert.Equal(HttpStatusCode.Conflict,(await TestApplication.Post(client,path+"/previews",new{})).StatusCode);
            await ReportingFixture.Payments(client,classId);
            Assert.Equal(HttpStatusCode.Conflict,(await TestApplication.Post(client,path+"/previews",new{})).StatusCode);
        }
    }
    [Fact]
    public async Task Preview_changes_when_new_assessment_profile_or_recipient_changes()
    {
        var (client,path,classId,studentId,_)=await ReportingFixture.Setup(app);
        using(client)
        {
            async Task<string> Stamp() { var r=await TestApplication.Post(client,path+"/previews",new{}); Assert.Equal(HttpStatusCode.OK,r.StatusCode);return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sourceStamp").GetString()!; }
            var first=await Stamp(); Assert.Equal(first,await Stamp());
            await TestApplication.Post(client,$"/api/learning/classes/{classId}/assessments",new{title="Bài mới",date="2026-10-06",defaultMethod="manual"});
            var second=await Stamp();Assert.NotEqual(first,second);
            await ReportingFixture.Put(client,$"/api/students/{studentId}",new{name="An đổi tên",version=1});var third=await Stamp();Assert.NotEqual(second,third);
            await TestApplication.Post(client,"/api/billing/recipient",new{bankBin="970415",bankName="VietinBank",accountNo="123456789",accountName="NGUYEN VAN AN",version=0});Assert.NotEqual(third,await Stamp());
        }
    }
    [Fact]
    public async Task Preview_uses_confirmed_results_and_exact_month_obligations()
    {
        var (client,path,classId,studentId,_) = await ReportingFixture.Setup(app);
        using(client)
        {
            var learning=$"/api/learning/classes/{classId}/assessments";
            var assessment=await (await TestApplication.Post(client,learning,new { title="Phân số",date="2026-10-05",defaultMethod="manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var id=assessment.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK,(await ReportingFixture.Put(client,$"{learning}/{id}/submissions/{studentId}",new { status="assessed",completion=75,correctness=50,version=1,confirm=true })).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await ReportingFixture.Put(client,$"{learning}/{id}/submissions/{studentId}",new { status="assessed",completion=25,correctness=25,version=2,confirm=false })).StatusCode);
            var response=await TestApplication.Post(client,path+"/previews",new {}); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var preview=await response.Content.ReadFromJsonAsync<JsonElement>();
            var sources=preview.GetProperty("sources");
            var result=Assert.Single(sources.GetProperty("learning").GetProperty("assessments").EnumerateArray());
            Assert.Equal(75,result.GetProperty("completion").GetInt32()); Assert.Equal(50,result.GetProperty("correctness").GetInt32());
            Assert.Equal("0",preview.GetProperty("totalDueVnd").GetString());
            Assert.True(preview.GetProperty("isExportable").GetBoolean());
        }
    }
}
