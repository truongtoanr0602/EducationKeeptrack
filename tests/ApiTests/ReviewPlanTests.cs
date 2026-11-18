using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static ApiTests.LearningHttp;
namespace ApiTests;

public sealed class ReviewPlanTests(TestApplication app) : IClassFixture<TestApplication>
{
    private const string Week = "2026-10-05"; // a Monday
    private sealed record Fixture(HttpClient Client, string ClassId, string Email, string An, string Binh,
        Dictionary<string, string> Skills, string Session1, string Session2, string MaterialA1, string DraftMaterialA1, string MaterialA2);

    private static string Plans(string classId, string suffix = "") => $"/api/learning/classes/{classId}/review-plans{suffix}";

    private static object Sess(string date, int minutes, string? sessionId) => new { date, minutes, sessionId };
    private static object Goal(string skill, string? reason = "Chưa đạt theo quy tắc", string? recheck = "Bài khác câu") => new { skillId = skill, reason, recheck };
    private static object Stu(string id, params object[] goals) => new { studentId = id, studentName = (string?)null, goals };
    private static object Act(int session, string[] students, string? skill, string? material, int minutes, string? description = "Luyện tập") =>
        new { session, studentIds = students, skillId = skill, materialId = material, minutes, description };
    private static object Content(object[] sessions, object[] students, object[] activities, string? note = null) => new { sessions, students, activities, note };

    /// <summary>A valid plan: An works on A1 and Bình on A2; a shared warm-up plus one private activity each in session 1.</summary>
    private static object Standard(Fixture f) => Content(
        [Sess("2026-10-06", 30, f.Session1), Sess("2026-10-08", 30, f.Session2)],
        [Stu(f.An, Goal(f.Skills["A1"])), Stu(f.Binh, Goal(f.Skills["A2"]))],
        [Act(0, [f.An, f.Binh], null, null, 5, "Que tính"), Act(0, [f.An], f.Skills["A1"], f.MaterialA1, 20), Act(0, [f.Binh], f.Skills["A2"], f.MaterialA2, 20)]);

    private async Task<string> Material(HttpClient client, string classId, string skillId, string title, bool approve)
    {
        var created = await Json(await Post(client, $"/api/learning/classes/{classId}/materials", new { skillId, title, content = "10 bài", minutes = 10, difficulty = "standard", version = 0 }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString()!;
        if (approve) await Json(await Post(client, $"/api/learning/classes/{classId}/materials/{id}/approve", new { version = 1 }), HttpStatusCode.OK);
        return id;
    }

    private async Task<Fixture> Setup()
    {
        var (client, classId, email) = await Teacher(app, "plan");
        var ids = new List<string>();
        foreach (var name in new[] { "An", "Bình" })
        {
            var student = await (await TestApplication.Post(client, "/api/students", new { name })).Content.ReadFromJsonAsync<JsonElement>();
            var id = student.GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId = id, startDate = "2026-10-01" })).StatusCode);
            ids.Add(id);
        }
        var (skills, version) = await SeedTaught(client, classId, "A1", "A2");
        var curriculum = $"/api/learning/classes/{classId}/curriculum";
        await Json(await Post(client, $"{curriculum}/skills", SkillBody("A3", version)), HttpStatusCode.Created); // not taught
        var all = (await Json(await client.GetAsync(curriculum), HttpStatusCode.OK)).GetProperty("skills").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("id").GetString()!);
        string[] sessions = new string[2];
        foreach (var (date, index) in new[] { ("2026-10-06", 0), ("2026-10-08", 1) })
        {
            var created = await Json(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date, startTime = "18:00:00", endTime = "19:00:00", kind = "regular" }), HttpStatusCode.Created);
            sessions[index] = created.GetProperty("id").GetString()!;
        }
        return new(client, classId, email, ids[0], ids[1], all, sessions[0], sessions[1],
            await Material(client, classId, all["A1"], "Phiếu A1", true), await Material(client, classId, all["A1"], "Nháp A1", false), await Material(client, classId, all["A2"], "Phiếu A2", true));
    }

    private static Task<HttpResponseMessage> Create(Fixture f, object content, string week = Week) =>
        Post(f.Client, Plans(f.ClassId), new { weekStart = week, content });
    private static async Task<JsonElement> Errors(HttpResponseMessage response, string field)
    {
        var problem = await Json(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), $"missing error for {field}: {problem.GetRawText()}");
        return problem;
    }

    [Fact]
    public async Task The_screen_gets_the_class_sessions_of_the_week_including_cancelled_ones()
    {
        var f = await Setup();
        using (f.Client)
        {
            var list = await Json(await f.Client.GetAsync($"/api/learning/classes/{f.ClassId}/review-sessions?weekStart={Week}"), HttpStatusCode.OK);
            Assert.Equal(["2026-10-06", "2026-10-08"], list.EnumerateArray().Select(x => x.GetProperty("date").GetString()!).ToArray());
            Assert.Equal("18:00:00", list[0].GetProperty("startTime").GetString());
            Assert.Equal("planned", list[0].GetProperty("status").GetString());
            Assert.Empty((await Json(await f.Client.GetAsync($"/api/learning/classes/{f.ClassId}/review-sessions?weekStart=2026-10-12"), HttpStatusCode.OK)).EnumerateArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.GetAsync($"/api/learning/classes/{f.ClassId}/review-sessions?weekStart=nope")).StatusCode);
            var (other, _, _) = await Teacher(app, "plan-other");
            using (other) Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/learning/classes/{f.ClassId}/review-sessions?weekStart={Week}")).StatusCode);
        }
    }

    [Fact]
    public async Task A_plan_is_saved_as_a_draft_with_its_evidence_and_listed_for_its_week()
    {
        var f = await Setup();
        using (f.Client)
        {
            var created = await Json(await Create(f, Standard(f)), HttpStatusCode.Created);
            Assert.Equal("draft", created.GetProperty("status").GetString());
            Assert.Equal(1, created.GetProperty("version").GetInt32());
            Assert.Equal(JsonValueKind.Null, created.GetProperty("approvedVersion").ValueKind);
            Assert.Equal(JsonValueKind.Null, created.GetProperty("approvedContent").ValueKind);
            Assert.Equal(Week, created.GetProperty("weekStart").GetString());
            Assert.Equal(0, created.GetProperty("execution").GetArrayLength());
            Assert.Equal(["created"], created.GetProperty("revisions").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray());
            var content = created.GetProperty("content");
            Assert.Equal(2, content.GetProperty("sessions").GetArrayLength());
            Assert.Equal("An", content.GetProperty("students")[0].GetProperty("studentName").GetString());
            Assert.Equal(3, content.GetProperty("activities").GetArrayLength());
            var evidence = created.GetProperty("evidence").EnumerateArray().ToArray();
            Assert.Equal(2, evidence.Length);
            Assert.All(evidence, x => { Assert.Equal("insufficient", x.GetProperty("state").GetString()); Assert.Equal(1, x.GetProperty("ruleVersion").GetInt32()); });

            var id = created.GetProperty("id").GetString()!;
            var list = await Json(await f.Client.GetAsync(Plans(f.ClassId)), HttpStatusCode.OK);
            Assert.Equal(id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetString());
            Assert.Equal(created.GetProperty("content").GetRawText(), (await Json(await f.Client.GetAsync(Plans(f.ClassId, $"/{id}")), HttpStatusCode.OK)).GetProperty("content").GetRawText());
            Assert.Equal(HttpStatusCode.Conflict, (await Create(f, Standard(f))).StatusCode);
        }
    }

    [Fact]
    public async Task The_evidence_of_a_goal_is_the_competency_at_the_time_of_saving()
    {
        var f = await Setup();
        using (f.Client)
        {
            foreach (var (date, correct) in new[] { ("2026-10-01", 2), ("2026-10-02", 3) })
            {
                var assessment = await Json(await TestApplication.Post(f.Client, $"/api/learning/classes/{f.ClassId}/assessments", new { title = $"Bài {date}", date, defaultMethod = "manual" }), HttpStatusCode.Created);
                var id = assessment.GetProperty("id").GetString()!;
                await Json(await Put(f.Client, $"/api/learning/classes/{f.ClassId}/assessments/{id}/skills", new { skills = new[] { new { skillId = f.Skills["A1"], taskCount = 5 } }, version = 0 }), HttpStatusCode.OK);
                await Json(await Put(f.Client, $"/api/learning/classes/{f.ClassId}/assessments/{id}/skill-results/{f.An}/{f.Skills["A1"]}", new { status = "assessed", correct, note = (string?)null, version = 1, confirm = true }), HttpStatusCode.OK);
            }
            var created = await Json(await Create(f, Standard(f)), HttpStatusCode.Created);
            var an = created.GetProperty("evidence").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == f.An);
            Assert.Equal("not_mastered", an.GetProperty("state").GetString());
            Assert.Equal(2, an.GetProperty("evidence").GetArrayLength());
            Assert.Equal(["2026-10-02", "2026-10-01"], an.GetProperty("evidence").EnumerateArray().Select(x => x.GetProperty("date").GetString()!).ToArray());
        }
    }

    [Fact]
    public async Task Plan_rules_reject_what_the_teacher_must_not_save()
    {
        var f = await Setup();
        using (f.Client)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(f, Standard(f), "2026-10-06")).StatusCode); // not a Monday
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(f, Standard(f), "nope")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(f.Client, Plans(f.ClassId), new { weekStart = Week })).StatusCode);
            var sessions = new[] { Sess("2026-10-06", 30, f.Session1) };
            var students = new[] { Stu(f.An, Goal(f.Skills["A1"])) };

            await Errors(await Create(f, Content([], students, [])), "sessions");
            await Errors(await Create(f, Content([Sess("2026-10-13", 30, null)], students, [])), "sessions"); // outside the week
            await Errors(await Create(f, Content([Sess("2026-10-07", 30, f.Session1)], students, [])), "sessions"); // date differs from the linked session
            await Errors(await Create(f, Content([Sess("2026-10-06", 30, f.Session1), Sess("2026-10-06", 30, f.Session1)], students, [])), "sessions");
            await Errors(await Create(f, Content([Sess("2026-10-06", 2, null)], students, [])), "sessions");
            await Errors(await Create(f, Content(sessions, [Stu(Guid.NewGuid().ToString(), Goal(f.Skills["A1"]))], [])), "students");
            await Errors(await Create(f, Content(sessions, [Stu(f.An), Stu(f.An)], [])), "students");
            await Errors(await Create(f, Content(sessions, [Stu(f.An, Goal(f.Skills["A1"]), Goal(f.Skills["A2"]), Goal(f.Skills["A3"]))], [])), "goals"); // three priorities
            await Errors(await Create(f, Content(sessions, [Stu(f.An, Goal(f.Skills["A3"]))], [])), "goals"); // not taught yet
            await Errors(await Create(f, Content(sessions, [Stu(f.An, Goal(f.Skills["A1"]), Goal(f.Skills["A1"]))], [])), "goals");
            await Errors(await Create(f, Content(sessions, [Stu(f.An, Goal(Guid.NewGuid().ToString()))], [])), "goals");
            await Errors(await Create(f, Content(sessions, students, [Act(5, [f.An], null, null, 10)])), "activities"); // no such session
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.Binh], null, null, 10)])), "activities"); // Bình is not in the plan
            await Errors(await Create(f, Content(sessions, students, [Act(0, [], null, null, 10)])), "activities");
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An], f.Skills["A2"], null, 10)])), "activities"); // A2 is not An's goal
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An], f.Skills["A1"], f.MaterialA2, 10)])), "activities"); // material of another skill
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An], f.Skills["A1"], f.DraftMaterialA1, 10)])), "activities"); // material not approved
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An], null, f.MaterialA1, 10)])), "activities"); // material without a skill
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An], null, null, 0)])), "activities");
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An], null, null, 20), Act(0, [f.An], null, null, 20)])), "minutes"); // 40 > 30
            Assert.Empty((await Json(await f.Client.GetAsync(Plans(f.ClassId)), HttpStatusCode.OK)).EnumerateArray());
        }
    }

    [Fact]
    public async Task Minutes_count_per_student_so_private_activities_run_in_parallel_but_shared_ones_count_for_everyone()
    {
        var f = await Setup();
        using (f.Client)
        {
            var sessions = new[] { Sess("2026-10-06", 30, null) };
            var students = new[] { Stu(f.An, Goal(f.Skills["A1"])), Stu(f.Binh, Goal(f.Skills["A2"])) };
            Assert.Equal(HttpStatusCode.Created, (await Create(f, Content(sessions, students, [Act(0, [f.An], null, null, 30), Act(0, [f.Binh], null, null, 30)]))).StatusCode);
            await Errors(await Create(f, Content(sessions, students, [Act(0, [f.An, f.Binh], null, null, 10), Act(0, [f.Binh], null, null, 25)]), "2026-10-12"), "minutes");
        }
    }

    [Fact]
    public async Task Saving_makes_a_new_version_only_when_something_changed_and_stale_saves_are_refused()
    {
        var f = await Setup();
        using (f.Client)
        {
            var created = await Json(await Create(f, Standard(f)), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            var same = await Json(await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = Standard(f), version = 1 }), HttpStatusCode.OK);
            Assert.Equal(1, same.GetProperty("version").GetInt32());

            var changed = Content([Sess("2026-10-06", 45, f.Session1), Sess("2026-10-08", 30, f.Session2)], [Stu(f.An, Goal(f.Skills["A1"]))], [], "Ghi chú");
            var saved = await Json(await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = changed, version = 1 }), HttpStatusCode.OK);
            Assert.Equal(2, saved.GetProperty("version").GetInt32());
            Assert.Equal("Ghi chú", saved.GetProperty("content").GetProperty("note").GetString());
            Assert.Equal(1, saved.GetProperty("content").GetProperty("students").GetArrayLength());
            Assert.Equal(["created", "saved"], saved.GetProperty("revisions").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray());
            Assert.Equal(HttpStatusCode.Conflict, (await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = Standard(f), version = 1 })).StatusCode);
            await Errors(await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = Content([], [], []), version = 2 }), "sessions");
            Assert.Equal(HttpStatusCode.NotFound, (await Put(f.Client, Plans(f.ClassId, $"/{Guid.NewGuid()}"), new { content = Standard(f), version = 1 })).StatusCode);
        }
    }

    [Fact]
    public async Task Approval_needs_reasons_rechecks_and_descriptions_and_freezes_the_approved_content()
    {
        var f = await Setup();
        using (f.Client)
        {
            var incomplete = Content([Sess("2026-10-06", 30, f.Session1)], [Stu(f.An, Goal(f.Skills["A1"], "", ""))], [Act(0, [f.An], null, null, 10, "")]);
            var draft = await Json(await Create(f, incomplete), HttpStatusCode.Created);
            var id = draft.GetProperty("id").GetString()!;
            var problem = await Errors(await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = 1 }), "goals");
            Assert.True(problem.GetProperty("errors").TryGetProperty("activities", out _));

            var complete = await Json(await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = Standard(f), version = 1 }), HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.Conflict, (await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = 1 })).StatusCode);
            var approved = await Json(await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = complete.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
            Assert.Equal("approved", approved.GetProperty("status").GetString());
            Assert.Equal(3, approved.GetProperty("version").GetInt32());
            Assert.Equal(3, approved.GetProperty("approvedVersion").GetInt32());
            Assert.Equal(approved.GetProperty("content").GetRawText(), approved.GetProperty("approvedContent").GetRawText());
            Assert.Equal(["created", "saved", "approved"], approved.GetProperty("revisions").EnumerateArray().Select(x => x.GetProperty("action").GetString()!).ToArray());
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = 3 })).StatusCode);

            // Editing after approval makes a new draft; the approved content stays until it is approved again.
            var edited = Content([Sess("2026-10-06", 30, f.Session1)], [Stu(f.An, Goal(f.Skills["A1"]))], [Act(0, [f.An], null, null, 10)]);
            var reopened = await Json(await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = edited, version = 3 }), HttpStatusCode.OK);
            Assert.Equal("draft", reopened.GetProperty("status").GetString());
            Assert.Equal(4, reopened.GetProperty("version").GetInt32());
            Assert.Equal(3, reopened.GetProperty("approvedVersion").GetInt32());
            Assert.Equal(approved.GetProperty("approvedContent").GetRawText(), reopened.GetProperty("approvedContent").GetRawText());
            Assert.NotEqual(reopened.GetProperty("content").GetRawText(), reopened.GetProperty("approvedContent").GetRawText());
            var again = await Json(await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = 4 }), HttpStatusCode.OK);
            Assert.Equal(5, again.GetProperty("approvedVersion").GetInt32());
        }
    }

    [Fact]
    public async Task A_material_that_lost_its_approval_after_saving_blocks_approval_of_the_plan()
    {
        var f = await Setup();
        using (f.Client)
        {
            var created = await Json(await Create(f, Standard(f)), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            var materials = $"/api/learning/classes/{f.ClassId}/materials/{f.MaterialA1}";
            await Json(await Put(f.Client, materials, new { skillId = f.Skills["A1"], title = "Phiếu A1", content = "Đã sửa nội dung", minutes = 10, difficulty = "standard", version = 2 }), HttpStatusCode.OK);
            await Errors(await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = 1 }), "activities");
        }
    }

    [Fact]
    public async Task Another_teacher_cannot_read_or_change_the_plans_of_a_class_they_do_not_own()
    {
        var f = await Setup();
        using (f.Client)
        {
            var created = await Json(await Create(f, Standard(f)), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            var (other, _, _) = await Teacher(app, "plan-other");
            using (other)
            {
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Plans(f.ClassId))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Plans(f.ClassId, $"/{id}"))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Post(other, Plans(f.ClassId), new { weekStart = "2026-10-12", content = Standard(f) })).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Put(other, Plans(f.ClassId, $"/{id}"), new { content = Standard(f), version = 1 })).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Post(other, Plans(f.ClassId, $"/{id}/approve"), new { version = 1 })).StatusCode);
            }
            Assert.Equal(1, (await Json(await f.Client.GetAsync(Plans(f.ClassId, $"/{id}")), HttpStatusCode.OK)).GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task Expired_teacher_reads_plans_but_cannot_write()
    {
        var f = await Setup();
        using (f.Client)
        {
            var created = await Json(await Create(f, Standard(f)), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(f.Client, f.Email);
            Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(Plans(f.ClassId))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(Plans(f.ClassId, $"/{id}"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Create(f, Standard(f), "2026-10-12")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(f.Client, Plans(f.ClassId, $"/{id}"), new { content = Standard(f), version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(f.Client, Plans(f.ClassId, $"/{id}/approve"), new { version = 1 })).StatusCode);
        }
    }
}
