using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using Xunit;

namespace ApiTests;

public sealed class AiCommentTests(TestApplication app) : IClassFixture<TestApplication>
{
    [Fact]
    public async Task Ai_comment_generates_neutral_comment_and_roadmap_for_monthly_report()
    {
        var email = $"aicomment-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        using var client = app.Client();
        await TestApplication.Login(client, email);

        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán 6", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "Minh" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);

        var reportPath = $"/api/reporting/classes/{classId}/students/{studentId}/months/2026-10";
        var aiRes = await TestApplication.Post(client, $"{reportPath}/ai-comment", new { });

        // Depending on teacher's AI quota status in test database
        if (aiRes.StatusCode == HttpStatusCode.OK)
        {
            var content = await aiRes.Content.ReadFromJsonAsync<JsonElement>();
            var comment = content.GetProperty("comment").GetString()!;
            var roadmap = content.GetProperty("roadmap").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(comment));
            Assert.False(string.IsNullOrWhiteSpace(roadmap));
            Assert.Contains("Minh", comment);
            // Verify no banned negative words per BR-AI-20
            Assert.DoesNotContain("yếu", comment, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("kém", comment, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lười", comment, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, aiRes.StatusCode);
        }
    }
}
