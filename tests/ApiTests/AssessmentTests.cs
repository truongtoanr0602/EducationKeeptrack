using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
namespace ApiTests;

public sealed class AssessmentTests(TestApplication app) : IClassFixture<TestApplication>
{
    private async Task<(HttpClient Client, string Path, string Student)> Setup()
    {
        var email = $"learning-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, $"/api/learning/classes/{classId}/assessments", studentId);
    }

    [Fact]
    public async Task Teacher_creates_assessment_with_one_unassessed_submission_per_student_and_reloads_it()
    {
        var (client, path, student) = await Setup();
        using (client)
        {
            var response = await TestApplication.Post(client, path, new { title = "Phân số", date = "2026-10-05", defaultMethod = "manual" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var item = await response.Content.ReadFromJsonAsync<JsonElement>();
            var saved = await client.GetFromJsonAsync<JsonElement>($"{path}/{item.GetProperty("id").GetString()}");
            Assert.Equal("Phân số", saved.GetProperty("title").GetString());
            var submission = Assert.Single(saved.GetProperty("submissions").EnumerateArray());
            Assert.Equal(student, submission.GetProperty("studentId").GetString());
            Assert.Equal("unassessed", submission.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, submission.GetProperty("completion").ValueKind);
            Assert.Equal(JsonValueKind.Null, submission.GetProperty("correctness").ValueKind);
        }
    }

    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Draft_does_not_replace_confirmed_result_and_stale_edits_cannot_overwrite_history()
    {
        var (client, path, student) = await Setup();
        using (client)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Số học", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var detail = $"{path}/{created.GetProperty("id").GetString()}";
            var submission = $"{detail}/submissions/{student}";
            Assert.Equal(HttpStatusCode.OK, (await Put(client, submission, new { status = "assessed", completion = 25, correctness = 100, version = 1, confirm = true })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, submission, new { status = "assessed", completion = 100, correctness = 50, version = 2, confirm = false })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, submission, new { status = "not_done", version = 2, confirm = true })).StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(detail);
            var result = Assert.Single(saved.GetProperty("submissions").EnumerateArray());
            Assert.Equal(100, result.GetProperty("completion").GetInt32());
            Assert.Equal(25, result.GetProperty("finalResult").GetProperty("completion").GetInt32());
            Assert.Equal(100, result.GetProperty("finalResult").GetProperty("correctness").GetInt32());
            Assert.Equal(2, result.GetProperty("history").GetArrayLength());
            Assert.Equal(HttpStatusCode.OK, (await Put(client, submission, new { status = "assessed", completion = 100, correctness = 50, version = 3, confirm = true })).StatusCode);
            var reloaded = await client.GetFromJsonAsync<JsonElement>(detail);
            Assert.Equal(50, reloaded.GetProperty("submissions")[0].GetProperty("finalResult").GetProperty("correctness").GetInt32());
        }
    }

    [Theory]
    [InlineData("not_done", null, null, 0, 0, true)]
    [InlineData("no_correct", 75, null, 75, 0, true)]
    [InlineData("unassigned", null, null, null, null, false)]
    [InlineData("unassessed", null, null, null, null, false)]
    [InlineData("assessed", 25, 100, 25, 100, true)]
    public async Task Explicit_states_preserve_zero_and_missing_distinction(string status, int? completion, int? correctness, int? expectedCompletion, int? expectedCorrectness, bool included)
    {
        var (client, path, student) = await Setup();
        using (client)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var detail = $"{path}/{created.GetProperty("id").GetString()}";
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{detail}/submissions/{student}", new { status, completion, correctness, version = 1, confirm = true })).StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(detail);
            var final = saved.GetProperty("submissions")[0].GetProperty("finalResult");
            Assert.Equal(expectedCompletion, final.GetProperty("completion").Deserialize<int?>());
            Assert.Equal(expectedCorrectness, final.GetProperty("correctness").Deserialize<int?>());
            Assert.Equal(included, final.GetProperty("includedInStatistics").GetBoolean());
        }
    }

    [Theory]
    [InlineData("assessed", null, null)]
    [InlineData("assessed", 60, 75)]
    [InlineData("assessed", 25, 0)]
    [InlineData("not_done", 25, null)]
    [InlineData("no_correct", null, 0)]
    [InlineData("no_correct", 75, 25)]
    [InlineData("unassigned", 25, 25)]
    [InlineData("unassessed", 0, 0)]
    [InlineData("unknown", null, null)]
    public async Task Invalid_or_incomplete_results_do_not_change_submission(string status, int? completion, int? correctness)
    {
        var (client, path, student) = await Setup();
        using (client)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var detail = $"{path}/{created.GetProperty("id").GetString()}";
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"{detail}/submissions/{student}", new { status, completion, correctness, version = 1, confirm = true })).StatusCode);
            var saved = await client.GetFromJsonAsync<JsonElement>(detail);
            Assert.Equal(1, saved.GetProperty("submissions")[0].GetProperty("version").GetInt32());
            Assert.Empty(saved.GetProperty("submissions")[0].GetProperty("history").EnumerateArray());
        }
    }

    [Fact]
    public async Task Ownership_cross_class_references_and_concurrent_confirmations_are_enforced()
    {
        var (client, path, student) = await Setup();
        var (other, otherPath, otherStudent) = await Setup();
        using (client) using (other)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var id = created.GetProperty("id").GetString();
            var detail = $"{path}/{id}";
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(detail)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{otherPath}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(other, $"{detail}/submissions/{student}", new { status = "not_done", version = 1, confirm = true })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Put(client, $"{detail}/submissions/{otherStudent}", new { status = "not_done", version = 1, confirm = true })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await TestApplication.Post(other, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).StatusCode);
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            async Task<HttpResponseMessage> Confirm(int completion)
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"{detail}/submissions/{student}")
                { Content = JsonContent.Create(new { status = "assessed", completion, correctness = 75, version = 1, confirm = true }) };
                request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
                return await client.SendAsync(request);
            }
            var responses = await Task.WhenAll(Confirm(25), Confirm(100));
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            var saved = await client.GetFromJsonAsync<JsonElement>(detail);
            Assert.Single(saved.GetProperty("submissions")[0].GetProperty("history").EnumerateArray());
        }
    }

    [Fact]
    public async Task AI_is_unavailable_and_method_is_locked_after_processing()
    {
        var (client, path, student) = await Setup();
        using (client)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await TestApplication.Post(client, path, new { title = "AI", date = "2026-10-05", defaultMethod = "ai" })).StatusCode);
            var created = await (await TestApplication.Post(client, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var submission = $"{path}/{created.GetProperty("id").GetString()}/submissions/{student}";
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, $"{submission}/method", new { method = "ai", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{submission}/method", new { method = "manual", version = 1 })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, submission, new { status = "not_done", version = 2, confirm = true })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Put(client, $"{submission}/method", new { method = "manual", version = 3 })).StatusCode);
        }
    }

    [Fact]
    public async Task Expired_teacher_can_read_history_but_cannot_write_and_csrf_is_required()
    {
        var (client, path, student) = await Setup();
        using (client)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
            var detail = $"{path}/{created.GetProperty("id").GetString()}";
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{detail}/submissions/{student}", new { status = "not_done", version = 1, confirm = true })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{detail}/submissions/{student}", new { status = "not_done", version = 1, confirm = true })).StatusCode);
            var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            app.Clock.Advance(TimeSpan.FromDays(31));
            await TestApplication.Login(client, me.GetProperty("email").GetString()!);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(detail)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Put(client, $"{detail}/submissions/{student}", new { status = "not_done", version = 2, confirm = true })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await TestApplication.Post(client, path, new { title = "Không được", date = "2026-10-05", defaultMethod = "manual" })).StatusCode);
        }
    }

    private sealed class RecordingLearningHandler : ILearningResultChangeHandler
    {
        public System.Collections.Concurrent.ConcurrentQueue<LearningResultChange> Changes { get; } = new();
        public Task HandleAsync(LearningResultChange change, CancellationToken ct) { Changes.Enqueue(change); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Only_confirmed_results_emit_owner_scoped_versioned_notifications()
    {
        var handler = new RecordingLearningHandler();
        await using var application = new TestApplication { LearningResultChangeHandler = handler };
        await application.InitializeAsync();
        const string email = "learning-events@example.test";
        var owner = await application.AddTeacher(email);
        using var client = application.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "An" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString();
        var studentId = student.GetProperty("id").GetString();
        await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" });
        var path = $"/api/learning/classes/{classId}/assessments";
        var assessment = await (await TestApplication.Post(client, path, new { title = "Bài tập", date = "2026-10-05", defaultMethod = "manual" })).Content.ReadFromJsonAsync<JsonElement>();
        var detail = $"{path}/{assessment.GetProperty("id").GetString()}";
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{detail}/submissions/{studentId}", new { status = "not_done", version = 1, confirm = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(client, $"{detail}/submissions/{studentId}", new { status = "not_done", version = 2, confirm = true })).StatusCode);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (handler.Changes.IsEmpty) await Task.Delay(100, timeout.Token);
        var change = Assert.Single(handler.Changes);
        Assert.Equal(owner, change.OwnerId);
        Assert.Equal(3, change.Version);
        Assert.Equal(Guid.Parse(classId!), change.ClassId);
        Assert.Equal(Guid.Parse(studentId!), change.StudentId);
        Assert.Equal(0, change.Completion);
        Assert.True(change.IncludedInStatistics);
    }
}
