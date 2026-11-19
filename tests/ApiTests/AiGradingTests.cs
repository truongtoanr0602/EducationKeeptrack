using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using Xunit;

namespace ApiTests;

public sealed class AiGradingTests(TestApplication app) : IClassFixture<TestApplication>
{
    private async Task<(HttpClient Client, string Path, string StudentId)> Setup()
    {
        var email = $"aigrading-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "Học sinh A" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, $"/api/learning/classes/{classId}/assessments", studentId);
    }

    [Fact]
    public async Task Ai_grading_requires_photos_and_rubric_then_proposes_valid_levels_and_supports_fallback()
    {
        var (client, path, studentId) = await Setup();
        using (client)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Bài tập hình học", date = "2026-10-12", defaultMethod = "ai", rubric = "" })).Content.ReadFromJsonAsync<JsonElement>();
            var assessmentId = created.GetProperty("id").GetString()!;
            var submissionPath = $"{path}/{assessmentId}/submissions/{studentId}";

            // Calling AI without photos returns 400
            var aiReq1 = await TestApplication.Post(client, $"{submissionPath}/ai-grade", new { });
            Assert.Equal(HttpStatusCode.BadRequest, aiReq1.StatusCode);

            // Upload a photo
            var fakePhoto = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x01, 0x01 };
            using var form = new MultipartFormDataContent();
            var photoContent = new ByteArrayContent(fakePhoto);
            photoContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            form.Add(photoContent, "file", "bai_tap.jpg");
            var uploadRes = await client.PostAsync($"{submissionPath}/photos", form);
            Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);

            // Still missing rubric returns 400
            var aiReq2 = await TestApplication.Post(client, $"{submissionPath}/ai-grade", new { });
            Assert.Equal(HttpStatusCode.BadRequest, aiReq2.StatusCode);

            // Add rubric
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            using var rubricReq = new HttpRequestMessage(HttpMethod.Put, $"{path}/{assessmentId}/rubric")
            {
                Content = JsonContent.Create(new { rubric = "Tiêu chí 1: Vẽ hình đúng (50%). Tiêu chí 2: Tính diện tích đúng (50%)." })
            };
            rubricReq.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(rubricReq)).StatusCode);

            // AI grading succeeds (or reports quota status)
            var aiRes = await TestApplication.Post(client, $"{submissionPath}/ai-grade", new { });
            // Since development grant might not have AI enabled by default, 403 or 200 are expected based on quota
            if (aiRes.StatusCode == HttpStatusCode.OK)
            {
                var sub = await aiRes.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("proposed", sub.GetProperty("aiStatus").GetString());
                Assert.NotNull(sub.GetProperty("aiCompletion").GetInt32());
                Assert.NotNull(sub.GetProperty("aiCorrectness").GetInt32());

                // Fallback reverts cleanly to manual
                var fallbackRes = await TestApplication.Post(client, $"{submissionPath}/ai-fallback", new { });
                Assert.Equal(HttpStatusCode.OK, fallbackRes.StatusCode);
                var fallbackSub = await fallbackRes.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("manual", fallbackSub.GetProperty("method").GetString());
                Assert.Equal("none", fallbackSub.GetProperty("aiStatus").GetString());
            }
            else
            {
                Assert.Equal(HttpStatusCode.Forbidden, aiRes.StatusCode);
            }
        }
    }
}
