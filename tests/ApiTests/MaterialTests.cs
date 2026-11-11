using System.Net;
using System.Text.Json;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ApiTests.LearningHttp;
namespace ApiTests;

public sealed class MaterialTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static string Materials(string classId, string suffix = "") => $"/api/learning/classes/{classId}/materials{suffix}";
    private static string Curriculum(string classId, string suffix = "") => $"/api/learning/classes/{classId}/curriculum{suffix}";

    // The body ui/src/Personalization.tsx sends.
    private static object Body(string skillId, string title = "Phiếu cộng qua 10", int version = 0, string content = "10 bài cộng qua 10", int minutes = 10, string difficulty = "standard") =>
        new { skillId, title, content, minutes, difficulty, version };

    [Fact]
    public async Task New_material_is_a_draft_and_only_approval_makes_it_usable()
    {
        var (client, classId, _) = await Teacher(app, "material");
        using (client)
        {
            var (skills, _) = await Seed(client, classId, "A1");
            var created = await Json(await Post(client, Materials(classId), Body(skills["A1"])), HttpStatusCode.Created);
            Assert.Equal("draft", created.GetProperty("status").GetString());
            Assert.Equal(1, created.GetProperty("version").GetInt32());
            Assert.Equal("standard", created.GetProperty("difficulty").GetString());
            var id = created.GetProperty("id").GetString()!;

            var approved = await Json(await Post(client, Materials(classId, $"/{id}/approve"), new { version = 1 }), HttpStatusCode.OK);
            Assert.Equal("approved", approved.GetProperty("status").GetString());
            Assert.Equal(2, approved.GetProperty("version").GetInt32());
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Materials(classId, $"/{id}/approve"), new { version = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Post(client, Materials(classId, $"/{id}/approve"), new { version = 1 })).StatusCode);

            var list = await Json(await client.GetAsync(Materials(classId)), HttpStatusCode.OK);
            Assert.Equal("approved", Assert.Single(list.EnumerateArray()).GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Editing_an_approved_material_returns_it_to_draft_and_a_save_without_changes_does_not()
    {
        var (client, classId, _) = await Teacher(app, "material-edit");
        using (client)
        {
            var (skills, _) = await Seed(client, classId, "A1", "A2");
            var created = await Json(await Post(client, Materials(classId), Body(skills["A1"])), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            await Json(await Post(client, Materials(classId, $"/{id}/approve"), new { version = 1 }), HttpStatusCode.OK);

            var same = await Json(await Put(client, Materials(classId, $"/{id}"), Body(skills["A1"], version: 2)), HttpStatusCode.OK);
            Assert.Equal("approved", same.GetProperty("status").GetString());
            Assert.Equal(2, same.GetProperty("version").GetInt32());

            var edited = await Json(await Put(client, Materials(classId, $"/{id}"), Body(skills["A1"], version: 2, minutes: 15)), HttpStatusCode.OK);
            Assert.Equal("draft", edited.GetProperty("status").GetString());
            Assert.Equal(3, edited.GetProperty("version").GetInt32());
            Assert.Equal(15, edited.GetProperty("minutes").GetInt32());
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, Materials(classId, $"/{id}"), Body(skills["A1"], version: 2, minutes: 20))).StatusCode);

            var moved = await Json(await Put(client, Materials(classId, $"/{id}"), Body(skills["A2"], version: 3, minutes: 15)), HttpStatusCode.OK);
            Assert.Equal(skills["A2"], moved.GetProperty("skillId").GetString());
        }
    }

    [Fact]
    public async Task Every_change_keeps_a_revision_so_older_versions_stay_readable()
    {
        var (client, classId, _) = await Teacher(app, "material-history");
        using (client)
        {
            var (skills, _) = await Seed(client, classId, "A1");
            var created = await Json(await Post(client, Materials(classId), Body(skills["A1"], content: "Bản đầu")), HttpStatusCode.Created);
            var id = Guid.Parse(created.GetProperty("id").GetString()!);
            await Json(await Post(client, Materials(classId, $"/{id}/approve"), new { version = 1 }), HttpStatusCode.OK);
            await Json(await Put(client, Materials(classId, $"/{id}"), Body(skills["A1"], version: 2, content: "Bản sửa")), HttpStatusCode.OK);

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LearningDatabase>();
            var revisions = await db.MaterialRevisions.AsNoTracking().Where(x => x.MaterialId == id).OrderBy(x => x.Version).ToListAsync();
            Assert.Equal(["created", "approved", "edited"], revisions.Select(x => x.Action).ToArray());
            Assert.Equal(["Bản đầu", "Bản đầu", "Bản sửa"], revisions.Select(x => x.Content).ToArray());
            Assert.Equal(["draft", "approved", "draft"], revisions.Select(x => x.Status).ToArray());
        }
    }

    [Fact]
    public async Task A_material_needs_an_active_skill_of_the_same_class()
    {
        var (client, classId, _) = await Teacher(app, "material-skill");
        using (client)
        {
            var (skills, version) = await Seed(client, classId, "A1", "A2");
            var other = await NewClass(client, "Lớp khác");
            var (otherSkills, _) = await Seed(client, other, "B1");
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Materials(classId), Body(otherSkills["B1"]))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Materials(classId), Body(Guid.NewGuid().ToString()))).StatusCode);

            var created = await Json(await Post(client, Materials(classId), Body(skills["A2"])), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            await Json(await Put(client, Curriculum(classId, $"/skills/{skills["A2"]}"), SkillBody("A2", version, archived: true)), HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Materials(classId), Body(skills["A2"], "Khác"))).StatusCode);
            // The material of a skill archived later cannot be approved until it is moved to a live skill.
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Materials(classId, $"/{id}/approve"), new { version = 1 })).StatusCode);
            var moved = await Json(await Put(client, Materials(classId, $"/{id}"), Body(skills["A1"], version: 1)), HttpStatusCode.OK);
            await Json(await Post(client, Materials(classId, $"/{id}/approve"), new { version = moved.GetProperty("version").GetInt32() }), HttpStatusCode.OK);

            Assert.Empty((await Json(await client.GetAsync(Materials(other)), HttpStatusCode.OK)).EnumerateArray());
        }
    }

    [Fact]
    public async Task Invalid_material_input_is_rejected_with_a_message_per_field()
    {
        var (client, classId, _) = await Teacher(app, "material-invalid");
        using (client)
        {
            var (skills, _) = await Seed(client, classId, "A1");
            var problem = await Json(await Post(client, Materials(classId), new { skillId = skills["A1"], title = " ", content = "", minutes = 0, difficulty = "hard", version = 0 }), HttpStatusCode.BadRequest);
            foreach (var field in new[] { "title", "content", "minutes", "difficulty" }) Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), field);
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Materials(classId), Body(skills["A1"], minutes: 241))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(client, Materials(classId, $"/{Guid.NewGuid()}"), Body(skills["A1"]))).StatusCode);
            Assert.Empty((await Json(await client.GetAsync(Materials(classId)), HttpStatusCode.OK)).EnumerateArray());
        }
    }

    [Fact]
    public async Task Materials_are_listed_by_title_and_belong_to_one_teacher()
    {
        var (owner, classId, _) = await Teacher(app, "material-owner");
        using (owner)
        {
            var (skills, _) = await Seed(owner, classId, "A1");
            await Json(await Post(owner, Materials(classId), Body(skills["A1"], "Zeta")), HttpStatusCode.Created);
            var first = await Json(await Post(owner, Materials(classId), Body(skills["A1"], "Alpha")), HttpStatusCode.Created);
            Assert.Equal(["Alpha", "Zeta"], (await Json(await owner.GetAsync(Materials(classId)), HttpStatusCode.OK)).EnumerateArray().Select(x => x.GetProperty("title").GetString()!).ToArray());
            var id = first.GetProperty("id").GetString()!;

            var (other, _, _) = await Teacher(app, "material-other");
            using (other)
            {
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Materials(classId))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Post(other, Materials(classId), Body(skills["A1"]))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Put(other, Materials(classId, $"/{id}"), Body(skills["A1"], version: 1, minutes: 99))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await Post(other, Materials(classId, $"/{id}/approve"), new { version = 1 })).StatusCode);
            }
            Assert.Equal("draft", (await Json(await owner.GetAsync(Materials(classId)), HttpStatusCode.OK))[0].GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Expired_teacher_reads_materials_but_cannot_write()
    {
        var (client, classId, email) = await Teacher(app, "material-expired");
        using (client)
        {
            var (skills, _) = await Seed(client, classId, "A1");
            var created = await Json(await Post(client, Materials(classId), Body(skills["A1"])), HttpStatusCode.Created);
            var id = created.GetProperty("id").GetString()!;
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(client, email);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Materials(classId))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, Materials(classId), Body(skills["A1"], "Mới"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, Materials(classId, $"/{id}/approve"), new { version = 1 })).StatusCode);
            using var scope = app.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var account = (await users.FindByEmailAsync(email))!;
            account.IsBlocked = true;
            Assert.True((await users.UpdateAsync(account)).Succeeded);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Materials(classId))).StatusCode);
        }
    }
}
