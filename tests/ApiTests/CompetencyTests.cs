using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static ApiTests.LearningHttp;
namespace ApiTests;

public sealed class CompetencyTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static string Learning(string classId, string suffix) => $"/api/learning/classes/{classId}{suffix}";
    private static object Result(string status, int? correct, int version, bool confirm) => new { status, correct, note = (string?)null, version, confirm };

    private sealed record Fixture(HttpClient Client, string ClassId, string Email, string StudentId, Dictionary<string, string> Skills, int CurriculumVersion);

    private async Task<Fixture> Setup(params string[] codes)
    {
        var (client, classId, email) = await Teacher(app, "competency");
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        var (skills, version) = await Seed(client, classId, codes);
        return new(client, classId, email, studentId, skills, version);
    }

    /// <summary>One assessment measuring the given skills with 5 tasks each, with the student's results for them.</summary>
    private static async Task Assess(Fixture f, string date, bool confirm, params (string Skill, int Correct)[] results)
    {
        var created = await Json(await TestApplication.Post(f.Client, Learning(f.ClassId, "/assessments"), new { title = $"Bài {date}", date, defaultMethod = "manual" }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString()!;
        await Json(await Put(f.Client, Learning(f.ClassId, $"/assessments/{id}/skills"), new { skills = results.Select(x => new { skillId = f.Skills[x.Skill], taskCount = 5 }).ToArray(), version = 0 }), HttpStatusCode.OK);
        foreach (var (skill, correct) in results)
            await Json(await Put(f.Client, Learning(f.ClassId, $"/assessments/{id}/skill-results/{f.StudentId}/{f.Skills[skill]}"), Result("assessed", correct, 1, confirm)), HttpStatusCode.OK);
    }

    private static async Task<JsonElement> Profile(Fixture f) => await Json(await f.Client.GetAsync(Learning(f.ClassId, "/competency")), HttpStatusCode.OK);
    private static JsonElement SkillOf(JsonElement profile, string code) =>
        profile.GetProperty("students")[0].GetProperty("skills").EnumerateArray().Single(x => x.GetProperty("code").GetString() == code);

    private static async Task TeachAsync(Fixture f, string code, int version) =>
        await Json(await Put(f.Client, Learning(f.ClassId, $"/curriculum/skills/{f.Skills[code]}"), SkillBody(code, version, taught: true)), HttpStatusCode.OK);

    [Fact]
    public async Task Class_without_curriculum_has_an_empty_unconfigured_profile()
    {
        var (client, classId, _) = await Teacher(app, "competency-empty");
        using (client)
        {
            var profile = await Json(await client.GetAsync(Learning(classId, "/competency")), HttpStatusCode.OK);
            Assert.False(profile.GetProperty("configured").GetBoolean());
            Assert.Equal(JsonValueKind.Null, profile.GetProperty("rule").ValueKind);
            Assert.Equal(0, profile.GetProperty("students").GetArrayLength());
        }
    }

    [Fact]
    public async Task Confirmed_results_decide_mastery_and_each_conclusion_shows_its_evidence()
    {
        var f = await Setup("A1", "A2");
        using (f.Client)
        {
            await TeachAsync(f, "A1", f.CurriculumVersion);
            await Assess(f, "2026-10-05", true, ("A1", 4), ("A2", 5));
            var one = await Profile(f);
            Assert.Equal("insufficient", SkillOf(one, "A1").GetProperty("state").GetString());
            Assert.Equal(1, SkillOf(one, "A1").GetProperty("missingAttempts").GetInt32());

            await Assess(f, "2026-10-12", true, ("A1", 5), ("A2", 5));
            var profile = await Profile(f);
            Assert.True(profile.GetProperty("configured").GetBoolean());
            Assert.Equal(80, profile.GetProperty("rule").GetProperty("threshold").GetInt32());
            var a1 = SkillOf(profile, "A1");
            Assert.Equal("mastered", a1.GetProperty("state").GetString());
            var evidence = a1.GetProperty("evidence").EnumerateArray().ToArray();
            Assert.Equal(["2026-10-12", "2026-10-05"], evidence.Select(x => x.GetProperty("date").GetString()!).ToArray());
            Assert.All(evidence, x => { Assert.True(x.GetProperty("counted").GetBoolean()); Assert.True(x.GetProperty("meetsThreshold").GetBoolean()); Assert.Equal(5, x.GetProperty("taskCount").GetInt32()); });
            // A2 has the same results but was never marked as taught.
            Assert.Equal("not_taught", SkillOf(profile, "A2").GetProperty("state").GetString());
            Assert.Equal(2, SkillOf(profile, "A2").GetProperty("evidence").GetArrayLength());
        }
    }

    [Fact]
    public async Task Drafts_are_not_evidence_and_a_weak_attempt_means_not_mastered()
    {
        var f = await Setup("A1");
        using (f.Client)
        {
            await TeachAsync(f, "A1", f.CurriculumVersion);
            await Assess(f, "2026-10-05", true, ("A1", 5));
            await Assess(f, "2026-10-12", false, ("A1", 5));
            var draftOnly = await Profile(f);
            Assert.Equal("insufficient", SkillOf(draftOnly, "A1").GetProperty("state").GetString());
            Assert.Equal(1, SkillOf(draftOnly, "A1").GetProperty("evidence").GetArrayLength());

            await Assess(f, "2026-10-19", true, ("A1", 2));
            var weak = SkillOf(await Profile(f), "A1");
            Assert.Equal("not_mastered", weak.GetProperty("state").GetString());
            Assert.Equal(2, weak.GetProperty("evidence").EnumerateArray().Count(x => x.GetProperty("counted").GetBoolean()));
        }
    }

    [Fact]
    public async Task Changing_the_rule_changes_the_conclusion_and_reports_the_rule_version()
    {
        var f = await Setup("A1");
        using (f.Client)
        {
            await TeachAsync(f, "A1", f.CurriculumVersion);
            await Assess(f, "2026-10-05", true, ("A1", 4));
            await Assess(f, "2026-10-12", true, ("A1", 4));
            Assert.Equal("mastered", SkillOf(await Profile(f), "A1").GetProperty("state").GetString());

            var current = await Json(await f.Client.GetAsync(Learning(f.ClassId, "/curriculum")), HttpStatusCode.OK);
            await Json(await Put(f.Client, Learning(f.ClassId, "/curriculum"), new
            {
                subject = "Toán", grade = "Lớp 2", term = "Học kỳ 1", textbook = "Kết nối tri thức",
                masteryThreshold = 90, minTasks = 5, requiredAttempts = 2, version = current.GetProperty("version").GetInt32()
            }), HttpStatusCode.OK);
            var profile = await Profile(f);
            Assert.Equal(2, profile.GetProperty("rule").GetProperty("version").GetInt32());
            Assert.Equal("not_mastered", SkillOf(profile, "A1").GetProperty("state").GetString());
        }
    }

    [Fact]
    public async Task Archived_and_draft_skills_are_left_out_of_the_profile()
    {
        var f = await Setup("A1", "A2");
        using (f.Client)
        {
            var archived = await Json(await Put(f.Client, Learning(f.ClassId, $"/curriculum/skills/{f.Skills["A2"]}"), SkillBody("A2", f.CurriculumVersion, archived: true)), HttpStatusCode.OK);
            await Json(await Post(f.Client, Learning(f.ClassId, "/curriculum/skills"), new { code = "D1", name = "Nháp", strand = "", taught = false, archived = false, status = "draft", version = archived.GetProperty("version").GetInt32() }), HttpStatusCode.Created);
            var skills = (await Profile(f)).GetProperty("students")[0].GetProperty("skills").EnumerateArray().Select(x => x.GetProperty("code").GetString()!).ToArray();
            Assert.Equal(["A1"], skills);
        }
    }

    [Fact]
    public async Task Another_teacher_gets_nothing_and_an_expired_teacher_can_still_read()
    {
        var f = await Setup("A1");
        using (f.Client)
        {
            var (other, _, _) = await Teacher(app, "competency-other");
            using (other) Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Learning(f.ClassId, "/competency"))).StatusCode);
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(f.Client, f.Email);
            Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(Learning(f.ClassId, "/competency"))).StatusCode);
        }
    }
}
