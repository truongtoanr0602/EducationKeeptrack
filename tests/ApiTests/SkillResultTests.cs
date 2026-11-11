using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using static ApiTests.LearningHttp;
namespace ApiTests;

public sealed class SkillResultTests(TestApplication app) : IClassFixture<TestApplication>
{
    private sealed record Fixture(HttpClient Client, string ClassId, string Email, string AssessmentId, string[] Students, Dictionary<string, string> Skills, int CurriculumVersion);

    private static string Base(string classId, string assessmentId, string suffix = "") => $"/api/learning/classes/{classId}/assessments/{assessmentId}{suffix}";
    private static object Map(int version, params (string Skill, int Tasks)[] items) => new { skills = items.Select(x => new { skillId = x.Skill, taskCount = x.Tasks }).ToArray(), version };
    private static object Result(string status, int? correct, int version, bool confirm, string? note = null) => new { status, correct, note, version, confirm };

    private async Task<Fixture> Setup(params string[] codes)
    {
        var (client, classId, email) = await Teacher(app, "skill-result");
        var students = new List<string>();
        foreach (var name in new[] { "An", "Bình" })
        {
            var student = await (await TestApplication.Post(client, "/api/students", new { name })).Content.ReadFromJsonAsync<JsonElement>();
            var studentId = student.GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
            students.Add(studentId);
        }
        var (skills, version) = await Seed(client, classId, codes.Length == 0 ? ["A1", "A2"] : codes);
        var assessment = await Json(await TestApplication.Post(client, $"/api/learning/classes/{classId}/assessments", new { title = "Cộng trừ", date = "2026-10-05", defaultMethod = "manual" }), HttpStatusCode.Created);
        return new(client, classId, email, assessment.GetProperty("id").GetString()!, students.ToArray(), skills, version);
    }

    [Fact]
    public async Task Mapping_skills_creates_an_unassessed_row_for_every_student_and_skill()
    {
        var f = await Setup();
        using (f.Client)
        {
            var empty = await Json(await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills")), HttpStatusCode.OK);
            Assert.Equal(0, empty.GetProperty("version").GetInt32());
            Assert.Equal("Cộng trừ", empty.GetProperty("title").GetString());
            Assert.Equal("2026-10-05", empty.GetProperty("date").GetString());
            Assert.Equal(0, empty.GetProperty("skills").GetArrayLength());

            var saved = await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5), (f.Skills["A2"], 8))), HttpStatusCode.OK);
            Assert.Equal(1, saved.GetProperty("version").GetInt32());
            Assert.Equal(["A1", "A2"], saved.GetProperty("skills").EnumerateArray().Select(x => x.GetProperty("code").GetString()!).ToArray());
            Assert.All(saved.GetProperty("skills").EnumerateArray(), x => Assert.False(x.GetProperty("locked").GetBoolean()));
            var rows = saved.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(4, rows.Length);
            Assert.All(rows, row =>
            {
                Assert.Equal("unassessed", row.GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, row.GetProperty("correct").ValueKind);
                Assert.Equal(JsonValueKind.Null, row.GetProperty("finalResult").ValueKind);
                Assert.Equal(1, row.GetProperty("version").GetInt32());
            });
            Assert.Equal(f.Students.Order(), rows.Select(x => x.GetProperty("studentId").GetString()!).Distinct().Order());
            Assert.Equal(saved.GetRawText(), (await f.Client.GetStringAsync(Base(f.ClassId, f.AssessmentId, "/skills"))));
        }
    }

    [Fact]
    public async Task A_draft_never_replaces_the_confirmed_result_and_stale_edits_are_refused()
    {
        var f = await Setup();
        using (f.Client)
        {
            await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            var path = Base(f.ClassId, f.AssessmentId, $"/skill-results/{f.Students[0]}/{f.Skills["A1"]}");

            var draft = await Json(await Put(f.Client, path, Result("assessed", 3, 1, false, "Quên nhớ")), HttpStatusCode.OK);
            Assert.Equal(3, draft.GetProperty("correct").GetInt32());
            Assert.Equal(2, draft.GetProperty("version").GetInt32());
            Assert.Equal(JsonValueKind.Null, draft.GetProperty("finalResult").ValueKind);

            var confirmed = await Json(await Put(f.Client, path, Result("assessed", 3, 2, true, "Quên nhớ")), HttpStatusCode.OK);
            Assert.Equal(3, confirmed.GetProperty("finalResult").GetProperty("correct").GetInt32());
            Assert.Equal(5, confirmed.GetProperty("finalResult").GetProperty("taskCount").GetInt32());
            Assert.Equal("confirmed", confirmed.GetProperty("finalResult").GetProperty("action").GetString());

            var later = await Json(await Put(f.Client, path, Result("assessed", 5, 3, false)), HttpStatusCode.OK);
            Assert.Equal(5, later.GetProperty("correct").GetInt32());
            Assert.Equal(3, later.GetProperty("finalResult").GetProperty("correct").GetInt32());
            Assert.Equal(["edited", "confirmed", "edited"], later.GetProperty("history").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray());

            Assert.Equal(HttpStatusCode.Conflict, (await Put(f.Client, path, Result("assessed", 4, 3, true))).StatusCode);
            var saved = await Json(await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills")), HttpStatusCode.OK);
            Assert.True(saved.GetProperty("skills")[0].GetProperty("locked").GetBoolean());
            var other = saved.GetProperty("results").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == f.Students[1]);
            Assert.Equal("unassessed", other.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Skill_counts_are_never_derived_from_the_two_overall_indicators()
    {
        var f = await Setup();
        using (f.Client)
        {
            await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            var submission = $"/api/learning/classes/{f.ClassId}/assessments/{f.AssessmentId}/submissions/{f.Students[0]}";
            Assert.Equal(HttpStatusCode.OK, (await Put(f.Client, submission, new { status = "assessed", completion = 100, correctness = 100, version = 1, confirm = true })).StatusCode);
            var saved = await Json(await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills")), HttpStatusCode.OK);
            var row = saved.GetProperty("results").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == f.Students[0]);
            Assert.Equal("unassessed", row.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("correct").ValueKind);
            Assert.Equal(JsonValueKind.Null, row.GetProperty("finalResult").ValueKind);
        }
    }

    [Fact]
    public async Task Not_done_unassigned_and_unassessed_stay_distinct_and_never_become_zero()
    {
        var f = await Setup();
        using (f.Client)
        {
            await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            var path = (string student) => Base(f.ClassId, f.AssessmentId, $"/skill-results/{student}/{f.Skills["A1"]}");
            var notDone = await Json(await Put(f.Client, path(f.Students[0]), Result("not_done", 4, 1, true)), HttpStatusCode.OK);
            Assert.Equal("not_done", notDone.GetProperty("finalResult").GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, notDone.GetProperty("correct").ValueKind);
            var unassigned = await Json(await Put(f.Client, path(f.Students[1]), Result("unassigned", null, 1, true)), HttpStatusCode.OK);
            Assert.Equal("unassigned", unassigned.GetProperty("finalResult").GetProperty("status").GetString());
            var zero = await Json(await Put(f.Client, path(f.Students[0]), Result("assessed", 0, 2, true)), HttpStatusCode.OK);
            Assert.Equal(0, zero.GetProperty("finalResult").GetProperty("correct").GetInt32());
        }
    }

    [Fact]
    public async Task Invalid_results_are_rejected_with_a_message()
    {
        var f = await Setup();
        using (f.Client)
        {
            await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            var path = Base(f.ClassId, f.AssessmentId, $"/skill-results/{f.Students[0]}/{f.Skills["A1"]}");
            Assert.True((await Json(await Put(f.Client, path, Result("assessed", 6, 1, true)), HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("correct", out _));
            Assert.True((await Json(await Put(f.Client, path, Result("assessed", -1, 1, true)), HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("correct", out _));
            Assert.True((await Json(await Put(f.Client, path, Result("assessed", null, 1, true)), HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("correct", out _));
            Assert.True((await Json(await Put(f.Client, path, Result("bogus", null, 1, true)), HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("status", out _));
            Assert.True((await Json(await Put(f.Client, path, Result("unassessed", null, 1, true)), HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("status", out _));
            Assert.True((await Json(await Put(f.Client, path, Result("not_done", null, 1, false, new string('x', 501))), HttpStatusCode.BadRequest)).GetProperty("errors").TryGetProperty("note", out _));
            Assert.Equal(HttpStatusCode.NotFound, (await Put(f.Client, Base(f.ClassId, f.AssessmentId, $"/skill-results/{Guid.NewGuid()}/{f.Skills["A1"]}"), Result("not_done", null, 1, true))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(f.Client, Base(f.ClassId, f.AssessmentId, $"/skill-results/{f.Students[0]}/{f.Skills["A2"]}"), Result("not_done", null, 1, true))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync(Base(f.ClassId, Guid.NewGuid().ToString(), "/skills"))).StatusCode);
            var saved = await Json(await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills")), HttpStatusCode.OK);
            Assert.False(saved.GetProperty("skills")[0].GetProperty("locked").GetBoolean());
        }
    }

    [Fact]
    public async Task A_skill_with_results_is_locked_while_an_untouched_one_can_change_or_leave()
    {
        var f = await Setup("A1", "A2", "A3");
        using (f.Client)
        {
            var saved = await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5), (f.Skills["A2"], 5))), HttpStatusCode.OK);
            var result = Base(f.ClassId, f.AssessmentId, $"/skill-results/{f.Students[0]}/{f.Skills["A1"]}");
            await Json(await Put(f.Client, result, Result("assessed", 4, 1, true)), HttpStatusCode.OK);
            var path = Base(f.ClassId, f.AssessmentId, "/skills");

            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(1, (f.Skills["A2"], 5)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(1, (f.Skills["A1"], 9), (f.Skills["A2"], 5)))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(f.Client, path, Map(0, (f.Skills["A1"], 5), (f.Skills["A2"], 5), (f.Skills["A3"], 5)))).StatusCode);

            var changed = await Json(await Put(f.Client, path, Map(1, (f.Skills["A1"], 5), (f.Skills["A2"], 7), (f.Skills["A3"], 4))), HttpStatusCode.OK);
            Assert.Equal(2, changed.GetProperty("version").GetInt32());
            Assert.Equal(6, changed.GetProperty("results").GetArrayLength());
            var a2 = changed.GetProperty("skills").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "A2");
            Assert.Equal(7, a2.GetProperty("taskCount").GetInt32());

            var removed = await Json(await Put(f.Client, path, Map(2, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            Assert.Equal(3, removed.GetProperty("version").GetInt32());
            Assert.Equal(2, removed.GetProperty("results").GetArrayLength());
            Assert.Equal(4, removed.GetProperty("results").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == f.Students[0]).GetProperty("finalResult").GetProperty("correct").GetInt32());

            var same = await Json(await Put(f.Client, path, Map(3, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            Assert.Equal(3, same.GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task Only_active_approved_skills_of_the_same_class_can_be_mapped()
    {
        var f = await Setup("A1", "A2");
        using (f.Client)
        {
            var other = await NewClass(f.Client, "Lớp khác");
            var (otherSkills, _) = await Seed(f.Client, other, "B1");
            var path = Base(f.ClassId, f.AssessmentId, "/skills");
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(0, (otherSkills["B1"], 5)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(0, (Guid.NewGuid().ToString(), 5)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(0, (f.Skills["A1"], 0)))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(0, (f.Skills["A1"], 5), (f.Skills["A1"], 6)))).StatusCode);

            var curriculum = $"/api/learning/classes/{f.ClassId}/curriculum";
            var archived = await Json(await Put(f.Client, $"{curriculum}/skills/{f.Skills["A2"]}", SkillBody("A2", f.CurriculumVersion, archived: true)), HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(0, (f.Skills["A2"], 5)))).StatusCode);
            var draft = await Json(await Post(f.Client, $"{curriculum}/skills", new { code = "D1", name = "Nháp", strand = "", taught = false, archived = false, status = "draft", version = archived.GetProperty("version").GetInt32() }), HttpStatusCode.Created);
            var draftId = draft.GetProperty("skills").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "D1").GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(f.Client, path, Map(0, (draftId, 5)))).StatusCode);
            Assert.Equal(0, (await Json(await f.Client.GetAsync(path), HttpStatusCode.OK)).GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task Another_teacher_cannot_read_or_change_skill_results_of_an_assessment_they_do_not_own()
    {
        var f = await Setup();
        using (f.Client)
        {
            await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            var (other, _, _) = await Teacher(app, "skill-result-other");
            using (other)
            {
                var path = Base(f.ClassId, f.AssessmentId, "/skills");
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Put(other, path, Map(1))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Put(other, Base(f.ClassId, f.AssessmentId, $"/skill-results/{f.Students[0]}/{f.Skills["A1"]}"), Result("not_done", null, 1, true))).StatusCode);
            }
            var saved = await Json(await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills")), HttpStatusCode.OK);
            Assert.Equal(1, saved.GetProperty("skills").GetArrayLength());
        }
    }

    [Fact]
    public async Task Expired_teacher_reads_skill_results_but_cannot_write()
    {
        var f = await Setup();
        using (f.Client)
        {
            await Json(await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(0, (f.Skills["A1"], 5))), HttpStatusCode.OK);
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(f.Client, f.Email);
            Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(f.Client, Base(f.ClassId, f.AssessmentId, "/skills"), Map(1))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(f.Client, Base(f.ClassId, f.AssessmentId, $"/skill-results/{f.Students[0]}/{f.Skills["A1"]}"), Result("not_done", null, 1, true))).StatusCode);
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var account = (await users.FindByEmailAsync(f.Email))!;
            account.IsBlocked = true;
            Assert.True((await users.UpdateAsync(account)).Succeeded);
            Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync(Base(f.ClassId, f.AssessmentId, "/skills"))).StatusCode);
        }
    }
}
