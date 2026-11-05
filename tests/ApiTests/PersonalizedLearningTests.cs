using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace ApiTests;

public sealed class PersonalizedLearningTests(TestApplication app) : IClassFixture<TestApplication>
{
    private async Task<(HttpClient Client, string Path, string Student, Guid Skill)> Setup()
    {
        var email = $"personal-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán 2", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, $"/api/learning/classes/{classId}", studentId, Guid.NewGuid());
    }
    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private static object Profile(Guid skill, int version = 0, bool taught = true) => new { textbook = "Kết nối tri thức", semester = "Toán 2 - HK1", version, skills = new[] { new { id = skill, title = "Cộng qua 10", taught } } };

    [Fact]
    public async Task Profile_persists_and_rejects_stale_or_foreign_writes()
    {
        var (client, path, _, skill) = await Setup();
        var (other, _, _, _) = await Setup();
        using (client) using (other)
        {
            Assert.Equal(HttpStatusCode.OK, (await Put(client, path + "/profile", Profile(skill))).StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(path + "/profile");
            Assert.Equal("Kết nối tri thức", saved.GetProperty("textbook").GetString());
            Assert.Equal(1, saved.GetProperty("version").GetInt32());
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, path + "/profile", Profile(skill))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/profile")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, path + "/profile", Profile(skill, 1))).StatusCode);
        }
    }

    [Fact]
    public async Task Observations_are_append_only_idempotent_and_do_not_imply_mastery()
    {
        var (client, path, student, skill) = await Setup();
        using (client)
        {
            await Put(client, path + "/profile", Profile(skill));
            var key = Guid.NewGuid();
            var input = new { studentId = student, skillId = skill, date = "2026-10-05", level = "red", note = "Cần gợi ý", requestId = key };
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/observations", input)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/observations", input)).StatusCode);
            var progress = await client.GetFromJsonAsync<JsonElement>(path + "/progress?date=2026-10-06");
            var row = Assert.Single(progress.GetProperty("rows").EnumerateArray());
            Assert.True(row.GetProperty("pendingReview").GetBoolean());
            Assert.Equal("insufficient", row.GetProperty("mastery").GetString());
            Assert.Single(row.GetProperty("observations").EnumerateArray());
            await TestApplication.Post(client, path + "/observations", new { studentId = student, skillId = skill, date = "2026-10-06", level = "green", requestId = Guid.NewGuid() });
            progress = await client.GetFromJsonAsync<JsonElement>(path + "/progress?date=2026-10-06");
            row = progress.GetProperty("rows")[0];
            Assert.False(row.GetProperty("pendingReview").GetBoolean());
            Assert.Equal("insufficient", row.GetProperty("mastery").GetString());
            Assert.Equal(2, row.GetProperty("observations").GetArrayLength());
        }
    }

    [Theory]
    [InlineData(5, 4, 5, 5, "achieved")]
    [InlineData(3, 3, 3, 3, "insufficient")]
    [InlineData(5, 5, 5, 3, "needs_practice")]
    public async Task Mastery_uses_two_confirmed_assessments_with_at_least_five_tasks(int total1, int correct1, int total2, int correct2, string expected)
    {
        var (client, path, student, skill) = await Setup();
        using (client)
        {
            await Put(client, path + "/profile", Profile(skill));
            async Task Record(int total, int correct, string date) => Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, path + "/evidence", new { studentId = student, skillId = skill, date, total, correct, confirmed = true, title = "Bài riêng", requestId = Guid.NewGuid() })).StatusCode);
            await Record(total1, correct1, "2026-10-05");
            var first = await client.GetFromJsonAsync<JsonElement>(path + "/progress?date=2026-10-08");
            Assert.Equal("insufficient", first.GetProperty("rows")[0].GetProperty("mastery").GetString());
            await Record(total2, correct2, "2026-10-06");
            var second = await client.GetFromJsonAsync<JsonElement>(path + "/progress?date=2026-10-08");
            Assert.Equal(expected, second.GetProperty("rows")[0].GetProperty("mastery").GetString());
        }
    }

    [Fact]
    public async Task Invalid_evidence_foreign_student_and_untaught_skills_are_rejected()
    {
        var (client, path, student, skill) = await Setup();
        using (client)
        {
            await Put(client, path + "/profile", Profile(skill));
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path + "/evidence", new { studentId = student, skillId = skill, date = "2026-10-05", total = 5, correct = 6, confirmed = true, title = "Sai", requestId = Guid.NewGuid() })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(client, path + "/observations", new { studentId = Guid.NewGuid(), skillId = skill, date = "2026-10-05", level = "red", requestId = Guid.NewGuid() })).StatusCode);
            await Put(client, path + "/profile", Profile(skill, 1, false));
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path + "/observations", new { studentId = student, skillId = skill, date = "2026-10-05", level = "red", requestId = Guid.NewGuid() })).StatusCode);
        }
    }

    [Fact]
    public async Task Expired_teacher_can_read_but_cannot_change_personalized_data()
    {
        var (client, path, student, skill) = await Setup();
        using (client)
        {
            await Put(client, path + "/profile", Profile(skill));
            var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(client, me.GetProperty("email").GetString()!);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path + "/profile")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(client, path + "/profile", Profile(skill, 1))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(client, path + "/observations", new { studentId = student, skillId = skill, date = "2026-10-05", level = "green", requestId = Guid.NewGuid() })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(client, path + "/plans/generate", new { })).StatusCode);
        }
    }
}
