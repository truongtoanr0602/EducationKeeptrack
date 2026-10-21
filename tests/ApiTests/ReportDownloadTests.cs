using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UglyToad.PdfPig;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using EducationKeeptrack.Identity.Domain;

namespace ApiTests;

public sealed class ReportDownloadTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pdf_keeps_financial_amount_and_manual_or_static_fallback_when_dynamic_qr_fails(bool hasStatic)
    {
        await using var isolated=new TestApplication { QrGenerator=new UnavailableQr() }; await isolated.InitializeAsync();
        var (client,path,classId,studentId,_)=await ReportingFixture.Setup(isolated);
        using(client)
        {
            Assert.Equal(HttpStatusCode.Created,(await TestApplication.Post(client,$"/api/billing/classes/{classId}/rates",new{effectiveFrom="2026-10-01",unitPriceVnd="500000",version=0})).StatusCode);
            await ReportingFixture.Session(client,classId,studentId,"2026-10-02"); await ReportingFixture.Payments(client,classId);
            const string png="iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZWQAAAAASUVORK5CYII=";
            Assert.Equal(HttpStatusCode.OK,(await TestApplication.Post(client,"/api/billing/recipient",new{bankBin="970415",bankName="VietinBank",accountNo="123456789",accountName="NGUYEN VAN AN",staticQrBase64=hasStatic?png:null,version=0})).StatusCode);
            var (url,export)=await ExportAndLink(client,path,0); var bytes=await client.GetByteArrayAsync(url);
            using var pdf=PdfDocument.Open(bytes); var text=string.Join(" ",pdf.GetPages().Select(p=>p.Text));
            Assert.Contains("500.000 đồng",text); Assert.Contains("123456789",text); Assert.Contains("NGUYEN VAN AN",text);
            Assert.Contains(hasStatic?"QR động không khả dụng":"Không có QR",text);
            Assert.Single(export.GetProperty("warnings").EnumerateArray());
            if(hasStatic) Assert.True(pdf.GetPages().Sum(p=>p.NumberOfImages)>0);
        }
    }
    private sealed class UnavailableQr : EducationKeeptrack.Billing.Application.IVietQrGenerator
    {
        public Task<byte[]?> GenerateAsync(EducationKeeptrack.Billing.Application.RecipientView r,string amount,string content,CancellationToken ct) => throw new HttpRequestException("injected QR outage");
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expired_teacher_exports_saved_content_and_block_or_revoke_prevents_download(bool block)
    {
        await using var isolated = new TestApplication(); await isolated.InitializeAsync();
        var (client,path,_,_,email)=await ReportingFixture.Setup(isolated);
        using(client) Assert.Equal(HttpStatusCode.OK,(await ReportingFixture.Put(client,path+"/draft",ReportingFixture.Draft())).StatusCode);
        isolated.Clock.Advance(TimeSpan.FromDays(31));
        using var expired=isolated.Client(); await TestApplication.Login(expired,email);
        Assert.Equal(HttpStatusCode.Forbidden,(await ReportingFixture.Put(expired,path+"/draft",ReportingFixture.Draft(1))).StatusCode);
        var (url,_)=await ExportAndLink(expired,path,1);
        Assert.Equal(HttpStatusCode.OK,(await expired.GetAsync(url)).StatusCode);
        using(var scope=isolated.Services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<Account>>(); var account=(await users.FindByEmailAsync(email))!;
            if(block) { account.IsBlocked=true; Assert.True((await users.UpdateAsync(account)).Succeeded); }
            else Assert.True((await users.UpdateSecurityStampAsync(account)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await expired.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Pdf_long_vietnamese_text_and_allowed_emoji_remain_readable_across_pages()
    {
        var (client,path,_,_,_)=await ReportingFixture.Setup(app);
        using(client)
        {
            var comment="ĐẦU NHẬN XÉT "+string.Concat(Enumerable.Repeat("Tiến bộ về phân số và tư duy. ",280))+" CUỐI NHẬN XÉT";
            var roadmap="ĐẦU LỘ TRÌNH "+string.Concat(Enumerable.Repeat("Luyện bài toán và đọc hiểu. ",280))+" CUỐI LỘ TRÌNH";
            Assert.Equal(HttpStatusCode.OK,(await ReportingFixture.Put(client,path+"/draft",ReportingFixture.Draft(comment:comment,roadmap:roadmap,emoji:"📘"))).StatusCode);
            var (url,_)=await ExportAndLink(client,path,1); var bytes=await client.GetByteArrayAsync(url);
            using var pdf=PdfDocument.Open(bytes); Assert.True(pdf.NumberOfPages>1);
            var text=string.Join(" ",pdf.GetPages().Select(p=>p.Text));
            foreach(var marker in new[]{"ĐẦU NHẬN XÉT","CUỐI NHẬN XÉT","ĐẦU LỘ TRÌNH","CUỐI LỘ TRÌNH"}) Assert.Contains(marker,text);
            var root=new DirectoryInfo(AppContext.BaseDirectory); while(!File.Exists(Path.Combine(root.FullName,"EducationKeeptrack.slnx"))) root=root.Parent!;
            var artifacts=Path.Combine(root.FullName,".local","t11-artifacts"); Directory.CreateDirectory(artifacts); await File.WriteAllBytesAsync(Path.Combine(artifacts,"long-report.pdf"),bytes);
        }
    }

    internal static async Task<(string Url,JsonElement Export)> ExportAndLink(HttpClient client,string path,long version)
    {
        var previewResponse=await TestApplication.Post(client,path+"/previews",new{}); Assert.Equal(HttpStatusCode.OK,previewResponse.StatusCode);
        var preview=await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var response=await TestApplication.Post(client,path+"/exports",new{previewId=preview.GetProperty("id").GetGuid(),draftVersion=version,idempotencyKey=Guid.NewGuid()});
        Assert.True(response.StatusCode==HttpStatusCode.OK,await response.Content.ReadAsStringAsync());
        var export=await response.Content.ReadFromJsonAsync<JsonElement>();
        var link=await TestApplication.Post(client,path+"/exports/"+export.GetProperty("id").GetGuid()+"/download-link",new{}); Assert.Equal(HttpStatusCode.OK,link.StatusCode);
        return ((await link.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!,export);
    }
    [Fact]
    public async Task Download_requires_current_owner_session_and_unexpired_bound_token()
    {
        var (client,path,_,_,_) = await ReportingFixture.Setup(app);
        using(client)
        {
            await ReportingFixture.Put(client,path+"/draft",ReportingFixture.Draft(emoji:"⭐"));
            var preview = await (await TestApplication.Post(client,path+"/previews",new{})).Content.ReadFromJsonAsync<JsonElement>();
            var export = await (await TestApplication.Post(client,path+"/exports",new { previewId=preview.GetProperty("id").GetGuid(),draftVersion=1,idempotencyKey=Guid.NewGuid() })).Content.ReadFromJsonAsync<JsonElement>();
            var exportPath = path+"/exports/"+export.GetProperty("id").GetGuid();
            var response = await TestApplication.Post(client,exportPath+"/download-link",new{});
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
            var file = await client.GetAsync(url); Assert.Equal(HttpStatusCode.OK,file.StatusCode);
            Assert.Equal("application/pdf",file.Content.Headers.ContentType!.MediaType);
            Assert.Contains("no-store",file.Headers.CacheControl!.ToString());
            using var pdf = PdfDocument.Open(await file.Content.ReadAsByteArrayAsync());
            var text = string.Join(" ",pdf.GetPages().Select(p=>p.Text));
            Assert.Contains("Tiến bộ về phân số",text); Assert.Contains("Luyện 10 bài",text);
            Assert.Contains("Tổng cần thu: 0 đồng",text); Assert.DoesNotContain("QR thanh toán",text);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(exportPath+"/download-link",new{})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(exportPath+"/file?token=forged")).StatusCode);
            using var anonymous = app.Client(); Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(url)).StatusCode);
            var (other,_,_,_,_) = await ReportingFixture.Setup(app);
            using(other) Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(url)).StatusCode);
            app.Clock.Advance(TimeSpan.FromMinutes(6));
            Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(url)).StatusCode);
        }
    }
}
