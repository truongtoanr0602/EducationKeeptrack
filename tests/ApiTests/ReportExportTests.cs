using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed class ReportExportTests(TestApplication app) : IClassFixture<TestApplication>
{
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
