using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTests;

public sealed class ReportingTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Draft_limits_csrf_foreign_scope_and_read_only_expiry()
    {
        await using var isolated = new TestApplication(); await isolated.InitializeAsync();
        var (client, path, classId, studentId, email) = await ReportingFixture.Setup(isolated);
        using (client)
        {
            foreach (var input in new[] { ReportingFixture.Draft(displayName: new string('x',161)), ReportingFixture.Draft(comment:new string('x',10001)), ReportingFixture.Draft(roadmap:new string('x',10001)), ReportingFixture.Draft(emoji:"unknown"),
                new { displayName = "An", parentDisplayName = "", contactDisplay = new string('x',501), comment = "", roadmap = "", emoji = "none", version = 0L } })
                Assert.Equal(HttpStatusCode.BadRequest,(await ReportingFixture.Put(client,path+"/draft",input)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(path+"/draft",ReportingFixture.Draft())).StatusCode);
            using var anonymous = isolated.Client(); Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path+"/draft")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await ReportingFixture.Put(client,path+"/draft",ReportingFixture.Draft())).StatusCode);
            isolated.Clock.Advance(TimeSpan.FromDays(31));
            using var expired = isolated.Client(); await TestApplication.Login(expired,email);
            Assert.Equal(HttpStatusCode.OK,(await expired.GetAsync(path+"/draft")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await ReportingFixture.Put(expired,path+"/draft",ReportingFixture.Draft(1))).StatusCode);
            var saved=await expired.GetFromJsonAsync<JsonElement>(path+"/draft"); Assert.Equal(1,saved.GetProperty("version").GetInt64());
        }
    }
    [Fact]
    public async Task Draft_roundtrip_is_owner_scoped_versioned_and_does_not_change_source_profile()
    {
        var (client, path, classId, studentId, _) = await ReportingFixture.Setup(app);
        using (client)
        {
            var first = await client.GetAsync(path + "/draft"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(0, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt64());
            Assert.Equal(HttpStatusCode.OK, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft())).StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(path + "/draft");
            Assert.Equal(1, saved.GetProperty("version").GetInt64()); Assert.Equal("Tiến bộ về phân số", saved.GetProperty("comment").GetString());
            Assert.Equal("Luyện 10 bài", saved.GetProperty("roadmap").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await ReportingFixture.Put(client, path + "/draft", ReportingFixture.Draft())).StatusCode);
            var source = await client.GetFromJsonAsync<JsonElement>($"/api/students/{studentId}"); Assert.Equal("Nguyễn An", source.GetProperty("name").GetString());
            var (other, _, _, _, _) = await ReportingFixture.Setup(app);
            using (other) Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/draft")).StatusCode);
        }
    }
}
