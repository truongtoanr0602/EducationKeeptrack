using System.Net;
using System.Text.Json;
using static ApiTests.LearningHttp;
namespace ApiTests;

/// <summary>The four question kinds through the API (ADR 0008, #112): create, draw into an exam, answer, submit, score.</summary>
public sealed class QuestionTypesApiTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static string Learning(string classId, string path) => $"/api/learning/classes/{classId}{path}";

    private async Task<(HttpClient Client, string ClassId, string Skill, string Student)> Setup()
    {
        var (client, classId, _) = await LearningHttp.Teacher(app, "types");
        var student = await Json(await TestApplication.Post(client, "/api/students", new { name = "Lê Văn Cường" }), HttpStatusCode.Created);
        var studentId = student.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId, startDate = "2026-01-01" })).StatusCode);
        // The sample workbook is the quickest way to get a class with skills.
        var template = await client.GetAsync(Learning(classId, "/import-template"));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(await template.Content.ReadAsByteArrayAsync());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "mau.xlsx");
        await Json(await TestApplication.PostForm(client, Learning(classId, "/imports?commit=true"), form), HttpStatusCode.OK);
        var bank = await Json(await client.GetAsync(Learning(classId, "/questions")), HttpStatusCode.OK);
        return (client, classId, bank.GetProperty("skills")[0].GetProperty("id").GetString()!, studentId);
    }

    private static object[] Options(params string[] texts) => texts.Select((t, i) => new { key = ((char)('A' + i)).ToString(), text = t, image = (string?)null }).ToArray();

    private static async Task<string> Create(HttpClient client, string classId, object body) =>
        (await Json(await Post(client, Learning(classId, "/questions"), body), HttpStatusCode.Created)).GetProperty("id").GetString()!;

    [Fact]
    public async Task A_question_of_each_kind_is_saved_with_its_points_and_listed_back()
    {
        var (client, classId, skill, _) = await Setup();
        var multi = await Json(await Post(client, Learning(classId, "/questions"), new
        {
            code = "N1", type = "multi_choice", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Chọn các số chẵn",
            options = Options("2", "3", "4", "5"), answer = new[] { new[] { "A", "C" } }, points = 1.5, version = 0
        }), HttpStatusCode.Created);
        Assert.Equal("multi_choice", multi.GetProperty("type").GetString());
        Assert.Equal(1.5m, multi.GetProperty("points").GetDecimal());

        var typed = await Json(await Post(client, Learning(classId, "/questions"), new
        {
            code = "N2", type = "fill_blanks", fillMode = "type", ignoreDiacritics = true, level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(),
            stem = "Thủ đô của {{1}} là {{2}}.", options = Array.Empty<object>(), answer = new[] { new[] { "Việt Nam" }, new[] { "Hà Nội", "Ha Noi" } }, version = 0
        }), HttpStatusCode.Created);
        Assert.Equal("type", typed.GetProperty("fillMode").GetString());
        Assert.True(typed.GetProperty("ignoreDiacritics").GetBoolean());
        Assert.Equal(2, typed.GetProperty("blanks").GetInt32());
        Assert.Equal(1m, typed.GetProperty("points").GetDecimal());

        var essay = await Json(await Post(client, Learning(classId, "/questions"), new
        {
            code = "N3", type = "essay", level = "van_dung", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Giải thích",
            options = Array.Empty<object>(), answer = Array.Empty<string[]>(), sampleAnswer = "Vì chia hết cho 2", points = 3, version = 0
        }), HttpStatusCode.Created);
        Assert.Equal("Vì chia hết cho 2", essay.GetProperty("sampleAnswer").GetString());

        var bad = await Post(client, Learning(classId, "/questions"), new
        {
            code = "N4", type = "multi_choice", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "x",
            options = Options("1", "2", "3"), answer = new[] { new[] { "A" } }, points = 0.3, version = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.Content.ReadAsStringAsync());
        Assert.Contains("points", errors);
        Assert.Contains("answer", errors);
    }

    [Fact]
    public async Task An_exam_with_every_kind_hides_the_answers_scores_the_computer_part_and_leaves_the_essay_pending()
    {
        var (client, classId, skill, studentId) = await Setup();
        var multi = await Create(client, classId, new
        {
            code = "X1", type = "multi_choice", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Chọn các số chẵn",
            options = Options("2", "3", "4", "5"), answer = new[] { new[] { "A", "C" } }, points = 1, version = 0
        });
        var typed = await Create(client, classId, new
        {
            code = "X2", type = "fill_blanks", fillMode = "type", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(),
            stem = "Thủ đô của {{1}} là {{2}}.", options = Array.Empty<object>(), answer = new[] { new[] { "Việt Nam" }, new[] { "Hà Nội", "Ha Noi" } }, points = 2, version = 0
        });
        var essay = await Create(client, classId, new
        {
            code = "X3", type = "essay", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Giải thích vì sao",
            options = Array.Empty<object>(), answer = Array.Empty<string[]>(), sampleAnswer = "ĐÁP ÁN MẪU BÍ MẬT", points = 3, version = 0
        });
        var single = await Create(client, classId, new
        {
            code = "X4", type = "single_choice", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "1 + 1 = ?",
            options = Options("1", "2", "3"), answer = new[] { new[] { "B" } }, points = 2, version = 0
        });

        var exam = await Json(await Post(client, Learning(classId, "/exams"), new
        {
            title = "Đủ dạng", counts = new[] { 4, 0, 0, 0 }, skillIds = new[] { skill }, durationMinutes = 30, questionIds = new[] { multi, typed, essay, single }
        }), HttpStatusCode.Created);
        var examPath = Learning(classId, $"/exams/{exam.GetProperty("id").GetString()}");
        exam = await Json(await Post(client, $"{examPath}/open", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var token = exam.GetProperty("publicToken").GetString()!;
        var code = exam.GetProperty("seats").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == studentId).GetProperty("code").GetString()!;
        var order = exam.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("questionId").GetString()!, x => x.GetProperty("order").GetInt32());

        using var student = app.Client();
        var attempt = await Json(await Post(student, $"/api/exam-take/{token}/join", new { studentId, code }), HttpStatusCode.OK);
        var shown = attempt.ToString();
        Assert.DoesNotContain("\"answer\"", shown);
        Assert.DoesNotContain("BÍ MẬT", shown);
        Assert.DoesNotContain("Hà Nội", shown);
        var byOrder = attempt.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("order").GetInt32());
        Assert.Equal(2, byOrder[order[multi]].GetProperty("choose").GetInt32());
        Assert.Equal(JsonValueKind.Null, byOrder[order[single]].GetProperty("choose").ValueKind);
        Assert.Equal(2, byOrder[order[typed]].GetProperty("blanks").GetInt32());
        Assert.Equal(3m, byOrder[order[essay]].GetProperty("points").GetDecimal());

        string Answers(string question) => $"/api/exam-take/{token}/attempt/answers/{order[question]}";
        // Mixed order and a stray key are refused; the right shapes are saved.
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(student, Answers(multi), new { values = new[] { "A", "Z" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(student, Answers(multi), new { values = new[] { "A", "A" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(student, Answers(typed), new { values = new[] { "chỉ một ô" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(student, Answers(essay), new { values = new[] { new string('x', 2001) } })).StatusCode);
        await Json(await Put(student, Answers(multi), new { values = new[] { "C", "A" } }), HttpStatusCode.OK);
        await Json(await Put(student, Answers(typed), new { values = new[] { "việt nam", "ha noi" } }), HttpStatusCode.OK);
        await Json(await Put(student, Answers(essay), new { values = new[] { new string('y', 1500) } }), HttpStatusCode.OK);
        await Json(await Put(student, Answers(single), new { values = new[] { "A" } }), HttpStatusCode.OK);
        await Json(await Post(student, $"/api/exam-take/{token}/attempt/submit", new { }), HttpStatusCode.OK);

        exam = await Json(await client.GetAsync(examPath), HttpStatusCode.OK);
        exam = await Json(await Post(client, $"{examPath}/close", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var seat = exam.GetProperty("seats").EnumerateArray().Single();
        Assert.Equal(3m, seat.GetProperty("score").GetDecimal());       // multiple choice 1 + typed fill-in 2 + wrong single 0
        Assert.Equal(8m, seat.GetProperty("maxScore").GetDecimal());    // 1 + 2 + 3 + 2
        Assert.Equal(2, seat.GetProperty("correct").GetInt32());
        Assert.Equal(1, seat.GetProperty("pendingEssays").GetInt32());
        Assert.Equal(3, seat.GetProperty("skills").EnumerateArray().Sum(x => x.GetProperty("tasks").GetInt32()));

        exam = await Json(await Put(client, $"{examPath}/show-results", new { show = true, version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var released = await Json(await student.GetAsync($"/api/exam-take/{token}/attempt"), HttpStatusCode.OK);
        var result = released.GetProperty("result");
        Assert.Equal(3m, result.GetProperty("score").GetDecimal());
        Assert.Equal(8m, result.GetProperty("maxScore").GetDecimal());
        Assert.Equal(1, result.GetProperty("pendingEssays").GetInt32());
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var essayItem = items.Single(x => x.GetProperty("type").GetString() == "essay");
        Assert.Equal("ĐÁP ÁN MẪU BÍ MẬT", essayItem.GetProperty("answer")[0].GetString());
        Assert.Equal(0m, essayItem.GetProperty("score").GetDecimal());
        Assert.Equal(2m, items.Single(x => x.GetProperty("type").GetString() == "fill_blanks").GetProperty("score").GetDecimal());
    }
}
