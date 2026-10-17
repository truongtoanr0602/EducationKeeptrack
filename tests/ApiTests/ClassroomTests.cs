using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

public sealed class ClassroomTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Created_class_survives_new_session_and_is_hidden_from_another_teacher()
    {
        await app.AddTeacher("owner@example.test");
        await app.AddTeacher("other@example.test");
        using var owner = app.Client();
        using var other = app.Client();
        await TestApplication.Login(owner, "owner@example.test");
        await TestApplication.Login(other, "other@example.test");
        var created = await TestApplication.Post(owner, "/api/classes", new { name = "Toán 10", subject = "Toán", description = "Ôn tập", teacherId = "forged" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = item.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/classes/{id}")).StatusCode);
        var csrf = await other.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var edit = new HttpRequestMessage(HttpMethod.Put, $"/api/classes/{id}")
        {
            Content = JsonContent.Create(new { name = "Hacked", subject = "X" })
        };
        edit.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.SendAsync(edit)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/classes", new { name = "No CSRF", subject = "X" })).StatusCode);
        var otherClasses = await other.GetFromJsonAsync<JsonElement>("/api/classes");
        Assert.Equal(0, otherClasses.GetArrayLength());
        using var reload = app.Client();
        await TestApplication.Login(reload, "owner@example.test");
        var classes = await reload.GetFromJsonAsync<JsonElement>("/api/classes");
        Assert.Equal("Toán 10", classes[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("", "Toán", 0)]
    [InlineData("Lớp", "", 0)]
    [InlineData("Lớp", "Toán", 501)]
    public async Task Invalid_class_is_not_persisted(string name, string subject, int descriptionLength)
    {
        var email = $"validation-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        using var client = app.Client(); await TestApplication.Login(client, email);
        var response = await TestApplication.Post(client, "/api/classes", new { name, subject, description = new string('x', descriptionLength) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var classes = await client.GetFromJsonAsync<JsonElement>("/api/classes");
        Assert.Equal(0, classes.GetArrayLength());
    }
}
