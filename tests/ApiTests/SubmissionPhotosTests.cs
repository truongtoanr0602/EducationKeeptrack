using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using Xunit;

namespace ApiTests;

public sealed class SubmissionPhotosTests(TestApplication app) : IClassFixture<TestApplication>
{
    private async Task<(HttpClient Client, string Path, string StudentId)> Setup()
    {
        var email = $"photos-{Guid.NewGuid():N}@example.test";
        await app.AddTeacher(email);
        var client = app.Client();
        await TestApplication.Login(client, email);
        var classroom = await (await TestApplication.Post(client, "/api/classes", new { name = "Toán", subject = "Toán" })).Content.ReadFromJsonAsync<JsonElement>();
        var student = await (await TestApplication.Post(client, "/api/students", new { name = "Bình" })).Content.ReadFromJsonAsync<JsonElement>();
        var classId = classroom.GetProperty("id").GetString()!;
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-10-01" })).StatusCode);
        return (client, $"/api/learning/classes/{classId}/assessments", studentId);
    }

    [Fact]
    public async Task Teacher_updates_rubric_and_uploads_submission_photos_in_order()
    {
        var (client, path, studentId) = await Setup();
        using (client)
        {
            var created = await (await TestApplication.Post(client, path, new { title = "Kiểm tra 15 phút", date = "2026-10-08", defaultMethod = "manual", rubric = "Đáp án: Câu 1 B, Câu 2 15" })).Content.ReadFromJsonAsync<JsonElement>();
            var assessmentId = created.GetProperty("id").GetString()!;
            var detailPath = $"{path}/{assessmentId}";

            // Update rubric
            var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            using var rubricReq = new HttpRequestMessage(HttpMethod.Put, $"{detailPath}/rubric")
            {
                Content = JsonContent.Create(new { rubric = "Đáp án cập nhật: Câu 1 B, Câu 2 20" })
            };
            rubricReq.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
            var rubricRes = await client.SendAsync(rubricReq);
            Assert.Equal(HttpStatusCode.OK, rubricRes.StatusCode);
            var updatedAssessment = await rubricRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Đáp án cập nhật: Câu 1 B, Câu 2 20", updatedAssessment.GetProperty("rubric").GetString());
            Assert.True(updatedAssessment.GetProperty("rubricVersion").GetInt32() >= 2);

            // The new rubric is stored, not only echoed back: a later read (and AI grading) sees it.
            var stored = (await client.GetFromJsonAsync<JsonElement>(path)).EnumerateArray().Single();
            Assert.Equal("Đáp án cập nhật: Câu 1 B, Câu 2 20", stored.GetProperty("rubric").GetString());
            Assert.Equal(updatedAssessment.GetProperty("rubricVersion").GetInt32(), stored.GetProperty("rubricVersion").GetInt32());

            // Upload 2 photo pages
            var photo1 = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }; // Fake JPEG
            var photo2 = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // Fake PNG

            using var form1 = new MultipartFormDataContent();
            var content1 = new ByteArrayContent(photo1);
            content1.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            form1.Add(content1, "file", "page1.jpg");

            var uploadRes1 = await TestApplication.PostForm(client, $"{detailPath}/submissions/{studentId}/photos", form1);
            Assert.Equal(HttpStatusCode.Created, uploadRes1.StatusCode);
            var photoInfo1 = await uploadRes1.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, photoInfo1.GetProperty("sequenceIndex").GetInt32());
            Assert.Equal("image/jpeg", photoInfo1.GetProperty("contentType").GetString());

            using var form2 = new MultipartFormDataContent();
            var content2 = new ByteArrayContent(photo2);
            content2.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            form2.Add(content2, "file", "page2.png");

            var uploadRes2 = await TestApplication.PostForm(client, $"{detailPath}/submissions/{studentId}/photos", form2);
            Assert.Equal(HttpStatusCode.Created, uploadRes2.StatusCode);
            var photoInfo2 = await uploadRes2.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, photoInfo2.GetProperty("sequenceIndex").GetInt32());

            // Both pages are in the store, in the in-memory test store and not in R2.
            Assert.Equal(2, app.Photos.Count);

            // List photos with signed URLs
            var listRes = await client.GetAsync($"{detailPath}/submissions/{studentId}/photos");
            Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
            var photosList = await listRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, photosList.GetArrayLength());
            Assert.False(string.IsNullOrWhiteSpace(photosList[0].GetProperty("downloadUrl").GetString()));

            // Delete a photo
            var photoId1 = photoInfo1.GetProperty("id").GetString()!;
            using var deleteReq = new HttpRequestMessage(HttpMethod.Delete, $"{detailPath}/submissions/{studentId}/photos/{photoId1}");
            deleteReq.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
            var deleteRes = await client.SendAsync(deleteReq);
            Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);

            // Deleting a photo removes its object from the store too.
            Assert.Equal(1, app.Photos.Count);

            // Verify 1 photo remaining and re-indexed to 1
            var remainingPhotos = await client.GetFromJsonAsync<JsonElement>($"{detailPath}/submissions/{studentId}/photos");
            Assert.Equal(1, remainingPhotos.GetArrayLength());
            Assert.Equal(1, remainingPhotos[0].GetProperty("sequenceIndex").GetInt32());
        }
    }
}
