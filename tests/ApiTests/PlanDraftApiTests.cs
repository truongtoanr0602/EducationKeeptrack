using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ApiTests.LearningHttp;
namespace ApiTests;

// UC-AI-04: the AI draft of the weekly review plan. The model ranks skills; the rules and the quota ledger stay in charge.
public sealed class PlanDraftApiTests(TestApplication app) : IClassFixture<TestApplication>
{
    private const string Week = "2026-10-05";
    private sealed record Fixture(HttpClient Client, string ClassId, string Email, string An, string Binh, Dictionary<string, string> Skills);

    private static string Learning(string classId, string suffix) => $"/api/learning/classes/{classId}{suffix}";

    private async Task<Fixture> Setup()
    {
        var (client, classId, email) = await Teacher(app, "draft");
        var ids = new List<string>();
        foreach (var name in new[] { "An", "Bình" })
        {
            var student = await (await TestApplication.Post(client, "/api/students", new { name })).Content.ReadFromJsonAsync<JsonElement>();
            var id = student.GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId = id, startDate = "2026-10-01" })).StatusCode);
            ids.Add(id);
        }
        var (skills, _) = await SeedTaught(client, classId, "A1", "A2");
        foreach (var (skill, title) in new[] { ("A1", "Phiếu A1"), ("A2", "Phiếu A2") })
        {
            var created = await Json(await Post(client, Learning(classId, "/materials"), new { skillId = skills[skill], title, content = "10 bài", minutes = 15, difficulty = "basic", version = 0 }), HttpStatusCode.Created);
            await Json(await Post(client, Learning(classId, $"/materials/{created.GetProperty("id").GetString()}/approve"), new { version = 1 }), HttpStatusCode.OK);
        }
        foreach (var (date, _) in new[] { ("2026-10-06", 0), ("2026-10-08", 1) })
            await Json(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date, startTime = "18:00:00", endTime = "19:00:00", kind = "regular" }), HttpStatusCode.Created);
        return new(client, classId, email, ids[0], ids[1], skills);
    }

    /// <summary>One assessment of both skills (5 tasks each) with a confirmed result for each student and skill.</summary>
    private static async Task Assess(Fixture f, string date, (string Student, string Skill, int Correct)[] results)
    {
        var created = await Json(await TestApplication.Post(f.Client, Learning(f.ClassId, "/assessments"), new { title = $"Bài {date}", date, defaultMethod = "manual" }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString()!;
        await Json(await Put(f.Client, Learning(f.ClassId, $"/assessments/{id}/skills"), new { skills = f.Skills.Values.Select(x => new { skillId = x, taskCount = 5 }).ToArray(), version = 0 }), HttpStatusCode.OK);
        foreach (var (student, skill, correct) in results)
            await Json(await Put(f.Client, Learning(f.ClassId, $"/assessments/{id}/skill-results/{student}/{f.Skills[skill]}"),
                new { status = "assessed", correct, note = (string?)null, version = 1, confirm = true }), HttpStatusCode.OK);
    }

    /// <summary>An is weak at A1 and strong at A2; Bình is the other way round.</summary>
    private static async Task SeedResults(Fixture f)
    {
        await Assess(f, "2026-10-01", [(f.An, "A1", 2), (f.An, "A2", 5), (f.Binh, "A1", 5), (f.Binh, "A2", 1)]);
        await Assess(f, "2026-10-02", [(f.An, "A1", 3), (f.An, "A2", 5), (f.Binh, "A1", 5), (f.Binh, "A2", 2)]);
    }

    private async Task GrantPlanning(string email, int turns)
    {
        using var scope = app.Services.CreateScope();
        var id = (await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email))!.Id;
        var db = scope.ServiceProvider.GetRequiredService<ServiceAccessDatabase>();
        (await db.Accounts.SingleAsync(x => x.AccountId == id)).AiEnabled = true;
        foreach (var period in await db.Periods.Where(x => x.AccountId == id).ToListAsync()) period.PlanningQuota = turns;
        await db.SaveChangesAsync();
    }

    private static Task<HttpResponseMessage> Draft(Fixture f, string week = Week) => Post(f.Client, Learning(f.ClassId, "/review-plans/draft"), new { weekStart = week });

    [Fact]
    public async Task A_teacher_without_the_ai_right_is_refused_and_nothing_is_spent_or_saved()
    {
        var f = await Setup();
        using (f.Client)
        {
            await SeedResults(f);
            var refused = await Draft(f);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Contains("AI", await refused.Content.ReadAsStringAsync());
            var plans = await Json(await f.Client.GetAsync(Learning(f.ClassId, "/review-plans")), HttpStatusCode.OK);
            Assert.Equal(0, plans.GetArrayLength());
        }
    }

    [Fact]
    public async Task The_draft_targets_each_students_weak_skill_explains_why_and_round_trips_into_a_saved_plan()
    {
        var f = await Setup();
        using (f.Client)
        {
            await SeedResults(f);
            await GrantPlanning(f.Email, 1);
            var draft = await Json(await Draft(f), HttpStatusCode.OK);

            Assert.Equal("bkt-priority-v1", draft.GetProperty("model").GetProperty("name").GetString());
            Assert.Equal("default", draft.GetProperty("model").GetProperty("paramSource").GetString());
            var students = draft.GetProperty("content").GetProperty("students").EnumerateArray().ToDictionary(x => x.GetProperty("studentId").GetString()!);
            Assert.Equal(2, students.Count);
            string[] GoalSkills(string id) => students[id].GetProperty("goals").EnumerateArray().Select(g => g.GetProperty("skillId").GetString()!).ToArray();
            Assert.Equal([f.Skills["A1"]], GoalSkills(f.An));
            Assert.Equal([f.Skills["A2"]], GoalSkills(f.Binh));
            Assert.Contains("xác suất nắm kỹ năng", students[f.An].GetProperty("goals")[0].GetProperty("reason").GetString());

            var trace = draft.GetProperty("trace").EnumerateArray().ToList();
            Assert.All(trace, x => Assert.InRange(x.GetProperty("pKnown").GetDouble(), 0.0, 1.0));
            Assert.Contains(trace, x => x.GetProperty("studentId").GetString() == f.An && x.GetProperty("skillId").GetString() == f.Skills["A1"] && x.GetProperty("selected").GetBoolean());
            Assert.DoesNotContain(trace, x => x.GetProperty("studentId").GetString() == f.An && x.GetProperty("skillId").GetString() == f.Skills["A2"]);

            // The draft is not saved by the request; the teacher saves it, and the same rules accept it.
            Assert.Equal(0, (await Json(await f.Client.GetAsync(Learning(f.ClassId, "/review-plans")), HttpStatusCode.OK)).GetArrayLength());
            var saved = await Json(await Post(f.Client, Learning(f.ClassId, "/review-plans"), new { weekStart = Week, content = draft.GetProperty("content") }), HttpStatusCode.Created);
            Assert.Equal("draft", saved.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task One_planning_turn_is_spent_per_draft_and_the_next_is_refused_when_none_is_left()
    {
        var f = await Setup();
        using (f.Client)
        {
            await SeedResults(f);
            await GrantPlanning(f.Email, 1);
            Assert.Equal(HttpStatusCode.OK, (await Draft(f)).StatusCode);
            var second = await Draft(f);
            Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
            Assert.Contains("hạn mức", await second.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Nothing_to_review_or_no_curriculum_is_refused_without_spending_a_turn()
    {
        var f = await Setup();
        using (f.Client)
        {
            await GrantPlanning(f.Email, 1);
            // Mastered everywhere: 5/5 twice for both students.
            await Assess(f, "2026-10-01", [(f.An, "A1", 5), (f.An, "A2", 5), (f.Binh, "A1", 5), (f.Binh, "A2", 5)]);
            await Assess(f, "2026-10-02", [(f.An, "A1", 5), (f.An, "A2", 5), (f.Binh, "A1", 5), (f.Binh, "A2", 5)]);
            var none = await Draft(f);
            Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
            Assert.Contains("goals", await none.Content.ReadAsStringAsync());
            // The turn was never held, so a real request afterwards still succeeds once there is something to review.
            await Assess(f, "2026-10-03", [(f.An, "A1", 0), (f.An, "A2", 0), (f.Binh, "A1", 0), (f.Binh, "A2", 0)]);
            await Assess(f, "2026-10-04", [(f.An, "A1", 0), (f.An, "A2", 0), (f.Binh, "A1", 0), (f.Binh, "A2", 0)]);
            Assert.Equal(HttpStatusCode.OK, (await Draft(f)).StatusCode);
        }
        var (client, classId, email) = await Teacher(app, "draft-empty");
        using (client)
        {
            await GrantPlanning(email, 1);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Learning(classId, "/review-plans/draft"), new { weekStart = Week })).StatusCode);
        }
    }

    [Fact]
    public async Task The_week_must_start_on_a_monday_and_another_teachers_class_is_not_found()
    {
        var f = await Setup();
        using (f.Client)
        {
            await GrantPlanning(f.Email, 1);
            Assert.Equal(HttpStatusCode.BadRequest, (await Draft(f, "2026-10-06")).StatusCode);
            var (other, _, otherEmail) = await Teacher(app, "draft-other");
            using (other)
            {
                await GrantPlanning(otherEmail, 1);
                Assert.Equal(HttpStatusCode.NotFound, (await Post(other, Learning(f.ClassId, "/review-plans/draft"), new { weekStart = Week })).StatusCode);
            }
        }
    }
}
