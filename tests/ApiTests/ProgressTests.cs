using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static ApiTests.LearningHttp;
namespace ApiTests;

public sealed class ProgressTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static string Learning(string classId, string suffix) => $"/api/learning/classes/{classId}{suffix}";

    private async Task<(HttpClient Client, string ClassId, string StudentId)> Setup(string prefix)
    {
        var (client, classId, _) = await Teacher(app, prefix);
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-01-01" })).StatusCode);
        return (client, classId, studentId);
    }

    /// <summary>Creates an assessment and records the student's result; confirm=false leaves only a draft.</summary>
    private static async Task Assess(HttpClient client, string classId, string studentId, string date, string status, int? completion, int? correctness, bool confirm)
    {
        var created = await Json(await TestApplication.Post(client, Learning(classId, "/assessments"), new { title = $"Bài {date}", date, defaultMethod = "manual" }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString()!;
        if (status == "unassessed") return;
        await Json(await Put(client, Learning(classId, $"/assessments/{id}/submissions/{studentId}"), new { status, completion, correctness, version = 1, confirm }), HttpStatusCode.OK);
    }

    private static Task<JsonElement> Progress(HttpClient client, string classId, string studentId, string query = "?from=2026-10-01&to=2026-12-31") =>
        client.GetAsync(Learning(classId, $"/assessments/progress/{studentId}{query}")).ContinueWith(t => Json(t.Result, HttpStatusCode.OK)).Unwrap();

    [Fact]
    public async Task Two_indicators_are_plotted_independently_and_not_done_or_no_correct_count_as_real_zeros()
    {
        var (client, classId, studentId) = await Setup("progress-zero");
        using (client)
        {
            await Assess(client, classId, studentId, "2026-10-05", "assessed", 75, 50, true);
            await Assess(client, classId, studentId, "2026-10-12", "not_done", 0, 0, true);
            await Assess(client, classId, studentId, "2026-10-19", "no_correct", 100, 0, true);
            var progress = await Progress(client, classId, studentId);
            var points = progress.GetProperty("points").EnumerateArray().ToArray();
            Assert.Equal(["2026-10-05", "2026-10-12", "2026-10-19"], points.Select(x => x.GetProperty("date").GetString()!).ToArray());
            Assert.Equal((75, 50), (points[0].GetProperty("completion").GetInt32(), points[0].GetProperty("correctness").GetInt32()));
            Assert.Equal((0, 0), (points[1].GetProperty("completion").GetInt32(), points[1].GetProperty("correctness").GetInt32()));
            Assert.Equal((100, 0), (points[2].GetProperty("completion").GetInt32(), points[2].GetProperty("correctness").GetInt32()));
            Assert.All(points, x => Assert.True(x.GetProperty("plotted").GetBoolean()));
            Assert.Equal("manual", points[0].GetProperty("method").GetString());
            Assert.True(progress.GetProperty("enoughForTrend").GetBoolean());
            Assert.Equal(3, progress.GetProperty("completeness").GetProperty("plotted").GetInt32());
        }
    }

    [Fact]
    public async Task Unassigned_unassessed_and_unconfirmed_work_is_a_gap_and_shown_as_completeness_not_as_zero()
    {
        var (client, classId, studentId) = await Setup("progress-gap");
        using (client)
        {
            await Assess(client, classId, studentId, "2026-10-05", "assessed", 100, 100, true);
            await Assess(client, classId, studentId, "2026-10-12", "unassigned", null, null, true);
            await Assess(client, classId, studentId, "2026-10-19", "assessed", 25, 25, false); // draft only
            await Assess(client, classId, studentId, "2026-10-26", "unassessed", null, null, true);
            var progress = await Progress(client, classId, studentId);
            var points = progress.GetProperty("points").EnumerateArray().ToArray();
            Assert.Equal(4, points.Length);
            Assert.True(points[0].GetProperty("plotted").GetBoolean());
            Assert.All(points.Skip(1), x =>
            {
                Assert.False(x.GetProperty("plotted").GetBoolean());
                Assert.Equal(JsonValueKind.Null, x.GetProperty("completion").ValueKind);
                Assert.Equal(JsonValueKind.Null, x.GetProperty("correctness").ValueKind);
            });
            Assert.False(points[2].GetProperty("confirmed").GetBoolean()); // the draft 25/25 never reaches the chart
            var completeness = progress.GetProperty("completeness");
            Assert.Equal((4, 1, 1), (completeness.GetProperty("total").GetInt32(), completeness.GetProperty("plotted").GetInt32(), completeness.GetProperty("notAssigned").GetInt32()));
            Assert.Equal(2, completeness.GetProperty("unconfirmed").GetInt32()); // the draft and the never-recorded assessment
            Assert.False(progress.GetProperty("enoughForTrend").GetBoolean());
        }
    }

    [Fact]
    public async Task Confirming_a_new_result_replaces_the_plotted_value_and_the_range_filters_assessments()
    {
        var (client, classId, studentId) = await Setup("progress-update");
        using (client)
        {
            await Assess(client, classId, studentId, "2026-10-05", "assessed", 50, 50, true);
            await Assess(client, classId, studentId, "2026-11-20", "assessed", 75, 75, true);
            var first = (await Progress(client, classId, studentId)).GetProperty("points")[0].GetProperty("assessmentId").GetString()!;
            // Correct the first confirmed result: version 2 after the first confirmation.
            await Json(await Put(client, Learning(classId, $"/assessments/{first}/submissions/{studentId}"), new { status = "assessed", completion = 100, correctness = 25, version = 2, confirm = true }), HttpStatusCode.OK);
            var all = await Progress(client, classId, studentId);
            Assert.Equal((100, 25), (all.GetProperty("points")[0].GetProperty("completion").GetInt32(), all.GetProperty("points")[0].GetProperty("correctness").GetInt32()));
            var october = await Progress(client, classId, studentId, "?from=2026-10-01&to=2026-10-31");
            Assert.Equal(1, october.GetProperty("points").GetArrayLength());
            Assert.Equal(400, (int)(await client.GetAsync(Learning(classId, $"/assessments/progress/{studentId}?from=2026-12-31&to=2026-10-01"))).StatusCode);
        }
    }

    [Fact]
    public async Task Another_teacher_cannot_read_a_progress_series_and_unknown_students_are_not_found()
    {
        var (client, classId, studentId) = await Setup("progress-owner");
        var (other, otherClass, _) = await Teacher(app, "progress-other");
        using (client)
        using (other)
        {
            await Assess(client, classId, studentId, "2026-10-05", "assessed", 100, 100, true);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Learning(classId, $"/assessments/progress/{studentId}"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Learning(otherClass, $"/assessments/progress/{studentId}"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Learning(classId, $"/assessments/progress/{Guid.NewGuid()}"))).StatusCode);
        }
    }
}
