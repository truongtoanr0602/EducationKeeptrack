using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

internal static class ReportingFixture
{
    public static async Task Session(HttpClient client, Guid classId, Guid studentId, string date)
    {
        var path=$"/api/classes/{classId}/sessions";
        var created=await TestApplication.Post(client,path,new {date,startTime="18:00:00",endTime="19:00:00",kind="regular"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode); var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK,(await Put(client,$"{path}/{id}",new{status="held",version=1})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Put(client,$"{path}/{id}/attendance/{studentId}",new{status="present",version=0})).StatusCode);
    }
    public static async Task<JsonElement> Payments(HttpClient client, Guid classId)
    {
        var until=DateTime.UtcNow.AddSeconds(20);
        while(true) { var value=await client.GetFromJsonAsync<JsonElement>($"/api/billing/classes/{classId}/payments?month=2026-11");
            if(value.GetProperty("status").GetString()!="pending") return value; Assert.True(DateTime.UtcNow<until,"Billing did not settle"); await Task.Delay(100); }
    }
    public static async Task<(HttpClient Client, string Path, Guid ClassId, Guid StudentId, string Email)> Setup(TestApplication app)
    {
        var email = $"report-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client(); await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Lớp báo cáo", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "Nguyễn An", parentName = "Phụ huynh An", phone = "0901234567" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetGuid(); var studentId = student.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, $"/api/reporting/classes/{classId}/students/{studentId}/months/2026-10", classId, studentId, email);
    }
    public static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); return await client.SendAsync(request);
    }
    public static object Draft(long version = 0, string? displayName = "An báo cáo", string comment = "Tiến bộ về phân số", string roadmap = "Luyện 10 bài", string emoji = "none") =>
        new { displayName, parentDisplayName = "Phụ huynh hiển thị", contactDisplay = "Liên hệ riêng", comment, roadmap, emoji, version };
}
