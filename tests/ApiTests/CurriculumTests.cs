using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace ApiTests;

public sealed class CurriculumTests(TestApplication app) : IClassFixture<TestApplication>
{
    private async Task<(HttpClient Client, string ClassId, string Email)> Setup()
    {
        var email = $"curriculum-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        return (client, await NewClass(client, "Toán 2"), email);
    }
    private static async Task<string> NewClass(HttpClient client, string name) =>
        (await (await TestApplication.Post(client, "/api/classes", new { name, subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

    private static string Path(string classId, string suffix = "") => $"/api/learning/classes/{classId}/curriculum{suffix}";

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private static Task<HttpResponseMessage> Put(HttpClient client, string path, object body) => Send(client, HttpMethod.Put, path, body);
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => Send(client, HttpMethod.Post, path, body);

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // The bodies below are what ui/src/Personalization.tsx sends.
    private static object Config(int version = 0, string textbook = "Kết nối tri thức", int threshold = 80, int minTasks = 5, int attempts = 2) =>
        new { subject = "Toán", grade = "Lớp 2", term = "Học kỳ 1", textbook, masteryThreshold = threshold, minTasks, requiredAttempts = attempts, version };
    private static object Skill(string code, int version, bool taught = false, bool archived = false, string name = "", string strand = "Số và phép tính") =>
        new { code, name = name == "" ? $"Kỹ năng {code}" : name, strand, taught, archived, version };
    private static JsonElement Find(JsonElement curriculum, string code) =>
        curriculum.GetProperty("skills").EnumerateArray().Single(x => x.GetProperty("code").GetString() == code);
    private static string[] History(JsonElement skill) => skill.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray();

    [Fact]
    public async Task Class_without_curriculum_returns_defaults_the_screen_can_render()
    {
        var (client, classId, _) = await Setup();
        using (client)
        {
            var empty = await Json(await client.GetAsync(Path(classId)), HttpStatusCode.OK);
            Assert.False(empty.GetProperty("configured").GetBoolean());
            Assert.Equal(0, empty.GetProperty("version").GetInt32());
            Assert.Equal(80, empty.GetProperty("rule").GetProperty("threshold").GetInt32());
            Assert.Equal(5, empty.GetProperty("rule").GetProperty("minTasks").GetInt32());
            Assert.Equal(2, empty.GetProperty("rule").GetProperty("requiredAttempts").GetInt32());
            Assert.Equal(0, empty.GetProperty("skills").GetArrayLength());
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Path(classId, "/skills"), Skill("A1", 0))).StatusCode);
        }
    }

    [Fact]
    public async Task Saving_configures_the_class_and_versions_change_only_when_something_changes()
    {
        var (client, classId, _) = await Setup();
        using (client)
        {
            var first = await Json(await Put(client, Path(classId), Config()), HttpStatusCode.OK);
            Assert.True(first.GetProperty("configured").GetBoolean());
            Assert.Equal("Lớp 2", first.GetProperty("grade").GetString());
            Assert.Equal(1, first.GetProperty("version").GetInt32());
            Assert.Equal(1, first.GetProperty("rule").GetProperty("version").GetInt32());

            var same = await Json(await Put(client, Path(classId), Config(1)), HttpStatusCode.OK);
            Assert.Equal(1, same.GetProperty("version").GetInt32());

            var book = await Json(await Put(client, Path(classId), Config(1, "Chân trời sáng tạo")), HttpStatusCode.OK);
            Assert.Equal(2, book.GetProperty("version").GetInt32());
            Assert.Equal(1, book.GetProperty("rule").GetProperty("version").GetInt32());

            var rule = await Json(await Put(client, Path(classId), Config(2, "Chân trời sáng tạo", 75, 4, 3)), HttpStatusCode.OK);
            Assert.Equal(3, rule.GetProperty("version").GetInt32());
            Assert.Equal(2, rule.GetProperty("rule").GetProperty("version").GetInt32());
            Assert.Equal(75, rule.GetProperty("rule").GetProperty("threshold").GetInt32());

            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, Path(classId), Config(1, "Cánh diều"))).StatusCode);
            Assert.Equal("Chân trời sáng tạo", (await Json(await client.GetAsync(Path(classId)), HttpStatusCode.OK)).GetProperty("textbook").GetString());
        }
    }

    [Fact]
    public async Task Invalid_configuration_is_rejected_with_a_message_per_field()
    {
        var (client, classId, _) = await Setup();
        using (client)
        {
            var problem = await Json(await Put(client, Path(classId), new { subject = "", grade = " ", term = "", textbook = "", masteryThreshold = 0, minTasks = 99, requiredAttempts = 9, version = 0 }), HttpStatusCode.BadRequest);
            foreach (var field in new[] { "subject", "grade", "term", "textbook", "masteryThreshold", "minTasks", "requiredAttempts" })
                Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), field);
            Assert.False((await Json(await client.GetAsync(Path(classId)), HttpStatusCode.OK)).GetProperty("configured").GetBoolean());
        }
    }

    [Fact]
    public async Task Two_classes_keep_their_own_textbook_rule_and_skills()
    {
        var (client, first, _) = await Setup();
        using (client)
        {
            var second = await NewClass(client, "Toán 2B");
            await Json(await Put(client, Path(first), Config(0, "Kết nối tri thức")), HttpStatusCode.OK);
            await Json(await Put(client, Path(second), Config(0, "Chân trời sáng tạo", 70, 3, 2)), HttpStatusCode.OK);
            await Json(await Post(client, Path(first, "/skills"), Skill("A1", 1)), HttpStatusCode.Created);
            var one = await Json(await client.GetAsync(Path(first)), HttpStatusCode.OK);
            var two = await Json(await client.GetAsync(Path(second)), HttpStatusCode.OK);
            Assert.Equal("Kết nối tri thức", one.GetProperty("textbook").GetString());
            Assert.Equal("Chân trời sáng tạo", two.GetProperty("textbook").GetString());
            Assert.Equal(80, one.GetProperty("rule").GetProperty("threshold").GetInt32());
            Assert.Equal(70, two.GetProperty("rule").GetProperty("threshold").GetInt32());
            Assert.Equal(1, one.GetProperty("skills").GetArrayLength());
            Assert.Equal(0, two.GetProperty("skills").GetArrayLength());
        }
    }

    [Fact]
    public async Task Skills_default_to_approved_and_record_who_taught_what_and_when()
    {
        var (client, classId, _) = await Setup();
        using (client)
        {
            await Json(await Put(client, Path(classId), Config()), HttpStatusCode.OK);
            var created = await Json(await Post(client, Path(classId, "/skills"), Skill("CQ10", 1)), HttpStatusCode.Created);
            var skill = Find(created, "CQ10");
            Assert.Equal("approved", skill.GetProperty("status").GetString());
            Assert.False(skill.GetProperty("taught").GetBoolean());
            Assert.Equal(JsonValueKind.Null, skill.GetProperty("taughtOn").ValueKind);
            Assert.Equal(2, created.GetProperty("version").GetInt32());
            var id = skill.GetProperty("id").GetString()!;

            var taught = await Json(await Put(client, Path(classId, $"/skills/{id}"), Skill("CQ10", 2, taught: true)), HttpStatusCode.OK);
            skill = Find(taught, "CQ10");
            Assert.True(skill.GetProperty("taught").GetBoolean());
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", skill.GetProperty("taughtOn").GetString()!);

            var again = await Json(await Put(client, Path(classId, $"/skills/{id}"), Skill("CQ10", 3, taught: true)), HttpStatusCode.OK);
            Assert.Equal(3, again.GetProperty("version").GetInt32());

            var untaught = await Json(await Put(client, Path(classId, $"/skills/{id}"), Skill("CQ10", 3)), HttpStatusCode.OK);
            Assert.False(Find(untaught, "CQ10").GetProperty("taught").GetBoolean());
            Assert.Equal(["created", "taught", "untaught"], History(Find(untaught, "CQ10")));
        }
    }

    [Fact]
    public async Task Stale_versions_cannot_overwrite_and_archived_skills_free_their_code()
    {
        var (client, classId, _) = await Setup();
        using (client)
        {
            await Json(await Put(client, Path(classId), Config()), HttpStatusCode.OK);
            var created = await Json(await Post(client, Path(classId, "/skills"), Skill("A1", 1)), HttpStatusCode.Created);
            var id = Find(created, "A1").GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Path(classId, "/skills"), Skill("a1", 2))).StatusCode);

            var renamed = await Json(await Put(client, Path(classId, $"/skills/{id}"), Skill("A1", 2, name: "Đã đổi tên")), HttpStatusCode.OK);
            Assert.Equal("Đã đổi tên", Find(renamed, "A1").GetProperty("name").GetString());
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, Path(classId, $"/skills/{id}"), Skill("A1", 2, name: "Bản cũ"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Post(client, Path(classId, "/skills"), Skill("B1", 2))).StatusCode);

            var archived = await Json(await Put(client, Path(classId, $"/skills/{id}"), Skill("A1", 3, name: "Đã đổi tên", archived: true)), HttpStatusCode.OK);
            Assert.True(Find(archived, "A1").GetProperty("archived").GetBoolean());
            var reused = await Json(await Post(client, Path(classId, "/skills"), Skill("A1", 4)), HttpStatusCode.Created);
            Assert.Equal(2, reused.GetProperty("skills").GetArrayLength());

            // Restoring the old skill would give two live skills the same code.
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Path(classId, $"/skills/{id}"), Skill("A1", 5, name: "Đã đổi tên"))).StatusCode);
            var history = History(archived.GetProperty("skills").EnumerateArray().Single());
            Assert.Equal(["created", "edited", "archived"], history);
        }
    }

    [Fact]
    public async Task Invalid_skill_input_and_unknown_skill_are_rejected()
    {
        var (client, classId, _) = await Setup();
        using (client)
        {
            await Json(await Put(client, Path(classId), Config()), HttpStatusCode.OK);
            var problem = await Json(await Post(client, Path(classId, "/skills"), new { code = "", name = "", strand = "", taught = false, archived = false, version = 1 }), HttpStatusCode.BadRequest);
            foreach (var field in new[] { "code", "name" }) Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), field);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Path(classId, "/skills"), new { code = "X", name = "Y", status = "bogus", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(client, Path(classId, $"/skills/{Guid.NewGuid()}"), Skill("A1", 1))).StatusCode);
            var withoutConfig = await NewClass(client, "Chưa cấu hình");
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Path(withoutConfig, "/skills"), Skill("A1", 0))).StatusCode);
        }
    }

    [Fact]
    public async Task Another_teacher_cannot_read_or_change_the_curriculum_of_a_class_they_do_not_own()
    {
        var (owner, classId, _) = await Setup();
        using (owner)
        {
            await Json(await Put(owner, Path(classId), Config()), HttpStatusCode.OK);
            var created = await Json(await Post(owner, Path(classId, "/skills"), Skill("A1", 1)), HttpStatusCode.Created);
            var id = Find(created, "A1").GetProperty("id").GetString()!;
            var (other, _, _) = await Setup();
            using (other)
            {
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Path(classId))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Put(other, Path(classId), Config())).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Post(other, Path(classId, "/skills"), Skill("B1", 2))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Put(other, Path(classId, $"/skills/{id}"), Skill("A1", 2, archived: true))).StatusCode);
            }
            Assert.False(Find(await Json(await owner.GetAsync(Path(classId)), HttpStatusCode.OK), "A1").GetProperty("archived").GetBoolean());
        }
    }

    [Fact]
    public async Task Expired_teacher_reads_curriculum_but_cannot_write_and_blocked_teacher_loses_access()
    {
        var (client, classId, email) = await Setup();
        using (client)
        {
            await Json(await Put(client, Path(classId), Config()), HttpStatusCode.OK);
            await Json(await Post(client, Path(classId, "/skills"), Skill("A1", 1)), HttpStatusCode.Created);
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(client, email);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Path(classId))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(client, Path(classId), Config(2))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, Path(classId, "/skills"), Skill("A2", 2))).StatusCode);
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var account = (await users.FindByEmailAsync(email))!;
            account.IsBlocked = true;
            Assert.True((await users.UpdateAsync(account)).Succeeded);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Path(classId))).StatusCode);
        }
    }
}
