using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using EducationKeeptrack.Learning.Infrastructure;
using static ApiTests.LearningHttp;
namespace ApiTests;

/// <summary>Lessons and Excel import (#78), question bank (#79), exams (#80), the student's attempt (#81) and confirmed results (#82).</summary>
public sealed class QuestionBankApiTests(TestApplication app) : IClassFixture<TestApplication>
{
    private static string Learning(string classId, string path) => $"/api/learning/classes/{classId}{path}";

    private async Task<(HttpClient Client, string ClassId, string[] Students)> Teacher(params string[] names)
    {
        var (client, classId, _) = await LearningHttp.Teacher(app, "bank");
        var students = new List<string>();
        foreach (var name in names)
        {
            var student = await Json(await TestApplication.Post(client, "/api/students", new { name }), HttpStatusCode.Created);
            var id = student.GetProperty("id").GetString()!;
            Assert.Equal(HttpStatusCode.Created, (await TestApplication.Post(client, $"/api/classes/{classId}/enrollments", new { studentId = id, startDate = "2026-01-01" })).StatusCode);
            students.Add(id);
        }
        return (client, classId, [.. students]);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string path, byte[] bytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await TestApplication.PostForm(client, path, form);
    }

    private static Task<HttpResponseMessage> Import(HttpClient client, string classId, byte[] workbook, bool commit) =>
        Upload(client, Learning(classId, $"/imports?commit={commit.ToString().ToLowerInvariant()}"), workbook, "chuong-trinh.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    private static async Task<byte[]> Template(HttpClient client, string classId)
    {
        var response = await client.GetAsync(Learning(classId, "/import-template"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task ImportSample(HttpClient client, string classId)
    {
        var report = await Json(await Import(client, classId, await Template(client, classId), true), HttpStatusCode.OK);
        Assert.True(report.GetProperty("committed").GetBoolean(), report.ToString());
    }

    [Fact]
    public async Task Preview_writes_nothing_commit_imports_the_sample_and_importing_again_changes_nothing()
    {
        var (client, classId, _) = await Teacher();
        var template = await Template(client, classId);

        var preview = await Json(await Import(client, classId, template, false), HttpStatusCode.OK);
        Assert.False(preview.GetProperty("committed").GetBoolean());
        Assert.Empty(preview.GetProperty("issues").EnumerateArray());
        Assert.Equal(9, preview.GetProperty("skills").GetProperty("added").GetInt32());
        Assert.Equal(30, preview.GetProperty("lessons").GetProperty("added").GetInt32());
        Assert.Equal(Toan2Hk1Sample.Questions.Length, preview.GetProperty("questions").GetProperty("added").GetInt32());
        Assert.False((await Json(await client.GetAsync(Learning(classId, "/curriculum")), HttpStatusCode.OK)).GetProperty("configured").GetBoolean());

        var committed = await Json(await Import(client, classId, template, true), HttpStatusCode.OK);
        Assert.True(committed.GetProperty("committed").GetBoolean());
        Assert.True(committed.GetProperty("curriculumCreated").GetBoolean());
        var lessons = await Json(await client.GetAsync(Learning(classId, "/lessons")), HttpStatusCode.OK);
        Assert.Equal(30, lessons.GetProperty("lessons").GetArrayLength());
        var fourth = lessons.GetProperty("lessons")[3];
        Assert.Equal(4, fourth.GetProperty("no").GetInt32());
        Assert.Equal(["E3", "E7"], fourth.GetProperty("skillCodes").EnumerateArray().Select(x => x.GetString()));
        var bank = await Json(await client.GetAsync(Learning(classId, "/questions")), HttpStatusCode.OK);
        Assert.Equal(Toan2Hk1Sample.Questions.Length, bank.GetProperty("questions").GetArrayLength());
        Assert.Equal(9, bank.GetProperty("skills").GetArrayLength());

        var again = await Json(await Import(client, classId, template, true), HttpStatusCode.OK);
        Assert.True(again.GetProperty("committed").GetBoolean());
        foreach (var part in new[] { "skills", "lessons", "questions" })
        {
            Assert.Equal(0, again.GetProperty(part).GetProperty("added").GetInt32());
            Assert.Equal(0, again.GetProperty(part).GetProperty("updated").GetInt32());
        }
    }

    [Fact]
    public async Task Rows_with_problems_are_reported_by_sheet_row_and_column_and_nothing_is_written()
    {
        var (client, classId, _) = await Teacher();
        using var book = new XLWorkbook(new MemoryStream(await Template(client, classId)));
        book.Worksheet("Buổi").Cell(3, 8).Value = "E1, Z9";
        var questions = book.Worksheet("Câu hỏi");
        questions.Cell(2, 3).Value = "Rất khó";
        questions.Cell(3, 8).Value = "dongho3.png";
        questions.Cell(4, 14).Value = "40";
        using var stream = new MemoryStream();
        book.SaveAs(stream);

        var report = await Json(await Import(client, classId, stream.ToArray(), true), HttpStatusCode.OK);
        Assert.False(report.GetProperty("committed").GetBoolean());
        var issues = report.GetProperty("issues").EnumerateArray().Select(x => (Sheet: x.GetProperty("sheet").GetString(), Row: x.GetProperty("row").GetInt32(),
            Column: x.GetProperty("column").GetString(), Message: x.GetProperty("message").GetString()!)).ToList();
        Assert.Contains(issues, x => x.Sheet == "Buổi" && x.Row == 3 && x.Message.Contains("Z9"));
        Assert.Contains(issues, x => x.Sheet == "Câu hỏi" && x.Row == 2 && x.Column == "Mức độ");
        Assert.Contains(issues, x => x.Sheet == "Câu hỏi" && x.Row == 3 && x.Column == "Ảnh đề");
        Assert.Contains(issues, x => x.Sheet == "Câu hỏi" && x.Row == 4 && x.Column == "Đáp án");
        Assert.Equal(["dongho3.png"], report.GetProperty("missingImages").EnumerateArray().Select(x => x.GetString()));
        Assert.False((await Json(await client.GetAsync(Learning(classId, "/curriculum")), HttpStatusCode.OK)).GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task An_uploaded_image_satisfies_the_reference_and_is_served_by_a_signed_link()
    {
        var (client, classId, _) = await Teacher();
        using var book = new XLWorkbook(new MemoryStream(await Template(client, classId)));
        book.Worksheet("Câu hỏi").Cell(3, 8).Value = "DongHo3.png";
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];
        await Json(await Upload(client, Learning(classId, "/question-images"), png, "dongho3.png", "image/png"), HttpStatusCode.Created);

        Assert.True((await Json(await Import(client, classId, stream.ToArray(), true), HttpStatusCode.OK)).GetProperty("committed").GetBoolean());
        var bank = await Json(await client.GetAsync(Learning(classId, "/questions")), HttpStatusCode.OK);
        var url = bank.GetProperty("questions").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "T2-002").GetProperty("stemImageUrl").GetString()!;
        using var anonymous = app.Client();
        var image = await anonymous.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(png, await image.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/learning/question-images/forged")).StatusCode);
    }

    [Fact]
    public async Task Skills_of_a_lesson_count_as_taught_once_its_paired_session_is_held()
    {
        var (client, classId, _) = await Teacher("Lê Minh");
        await ImportSample(client, classId);
        var first = await Json(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date = "2026-09-07", startTime = "17:00", endTime = "18:30", kind = "regular" }), HttpStatusCode.Created);
        var cancelled = await Json(await TestApplication.Post(client, $"/api/classes/{classId}/sessions", new { date = "2026-09-09", startTime = "17:00", endTime = "18:30", kind = "regular" }), HttpStatusCode.Created);
        await Json(await Put(client, $"/api/classes/{classId}/sessions/{first.GetProperty("id").GetString()}", new { status = "held", reason = (string?)null, version = first.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        await Json(await Put(client, $"/api/classes/{classId}/sessions/{cancelled.GetProperty("id").GetString()}", new { status = "cancelled", reason = "Mưa", version = cancelled.GetProperty("version").GetInt32() }), HttpStatusCode.OK);

        var plan = await Json(await client.GetAsync(Learning(classId, "/lessons")), HttpStatusCode.OK);
        var lessons = plan.GetProperty("lessons");
        Assert.Equal(first.GetProperty("id").GetString(), lessons[0].GetProperty("session").GetProperty("id").GetString());
        Assert.True(lessons[0].GetProperty("taught").GetBoolean());
        Assert.Equal(JsonValueKind.Null, lessons[1].GetProperty("session").ValueKind);

        var profile = await Json(await client.GetAsync(Learning(classId, "/competency")), HttpStatusCode.OK);
        var skills = profile.GetProperty("students")[0].GetProperty("skills").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x.GetProperty("state").GetString());
        Assert.Equal("insufficient", skills["E1"]);
        Assert.Equal("not_taught", skills["E2"]);
    }

    [Fact]
    public async Task A_structure_the_bank_cannot_fill_is_refused_with_what_is_missing()
    {
        var (client, classId, _) = await Teacher();
        await ImportSample(client, classId);
        var refused = await Json(await Post(client, Learning(classId, "/exams/preview"), new { counts = new[] { 0, 0, 5, 0 }, lessonFrom = 1, lessonTo = 30 }), HttpStatusCode.BadRequest);
        var message = refused.GetProperty("errors").GetProperty("counts")[0].GetString()!;
        Assert.StartsWith("Mức Vận dụng: cần 5 câu nhưng ngân hàng chỉ có 4 câu", message);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, Learning(classId, "/exams/preview"), new { counts = new[] { 1, 0, 0, 0 } })).StatusCode);
    }

    [Fact]
    public async Task Exam_runs_from_structure_to_confirmed_skill_results_without_ever_showing_answers_while_open()
    {
        var (client, classId, students) = await Teacher("Nguyễn Văn An", "Trần Thị Bình");
        await ImportSample(client, classId);

        var preview = await Json(await Post(client, Learning(classId, "/exams/preview"), new { counts = new[] { 4, 3, 2, 1 }, lessonFrom = 1, lessonTo = 30, seed = 3 }), HttpStatusCode.OK);
        var ids = preview.GetProperty("questions").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
        Assert.Equal(10, ids.Length);
        var exam = await Json(await Post(client, Learning(classId, "/exams"), new
        {
            title = "Kiểm tra nhanh", counts = new[] { 4, 3, 2, 1 }, lessonFrom = 1, lessonTo = 30, durationMinutes = 30, questionIds = ids
        }), HttpStatusCode.Created);
        var examPath = Learning(classId, $"/exams/{exam.GetProperty("id").GetString()}");
        Assert.Equal("draft", exam.GetProperty("status").GetString());
        exam = await Json(await Post(client, $"{examPath}/swap", new { order = 1, version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        exam = await Json(await Post(client, $"{examPath}/open", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        Assert.Equal("open", exam.GetProperty("status").GetString());
        var token = exam.GetProperty("publicToken").GetString()!;
        var seats = exam.GetProperty("seats").EnumerateArray().ToDictionary(x => x.GetProperty("studentId").GetString()!, x => x.GetProperty("code").GetString()!);
        var items = exam.GetProperty("items").EnumerateArray().ToList();

        using var student = app.Client();
        var entry = await Json(await student.GetAsync($"/api/exam-take/{token}"), HttpStatusCode.OK);
        Assert.Equal(["An N.", "Bình T."], entry.GetProperty("students").EnumerateArray().Select(x => x.GetProperty("name").GetString()).Order());
        Assert.DoesNotContain("Nguyễn Văn An", entry.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await student.GetAsync($"/api/exam-take/{token}/attempt")).StatusCode);
        var wrong = await Post(student, $"/api/exam-take/{token}/join", new { studentId = students[0], code = seats[students[0]] == "0000" ? "1111" : "0000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var attempt = await Json(await Post(student, $"/api/exam-take/{token}/join", new { studentId = students[0], code = seats[students[0]] }), HttpStatusCode.OK);
        Assert.Equal("in_progress", attempt.GetProperty("attemptStatus").GetString());
        Assert.Equal(10, attempt.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain("\"answer\"", attempt.ToString());

        // Answer every item with the key from the teacher's view, except the first one, which is left wrong.
        var key = items.ToDictionary(x => x.GetProperty("order").GetInt32(), x => x.GetProperty("answer").EnumerateArray().Select(a => a[0].GetString()!).ToArray());
        var wrongOrder = items[0].GetProperty("order").GetInt32();
        foreach (var (order, values) in key)
        {
            var given = order == wrongOrder ? values.Select(_ => "khác").ToArray() : values;
            await Json(await Put(student, $"/api/exam-take/{token}/attempt/answers/{order}", new { values = given }), HttpStatusCode.OK);
        }
        var submitted = await Json(await Post(student, $"/api/exam-take/{token}/attempt/submit", new { }), HttpStatusCode.OK);
        Assert.Equal("submitted", submitted.GetProperty("attemptStatus").GetString());
        Assert.Equal(JsonValueKind.Null, submitted.GetProperty("result").ValueKind);
        Assert.Equal(JsonValueKind.Null, submitted.GetProperty("items").ValueKind);

        exam = await Json(await client.GetAsync(examPath), HttpStatusCode.OK);
        exam = await Json(await Post(client, $"{examPath}/close", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var mine = exam.GetProperty("seats").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == students[0]);
        Assert.Equal(9, mine.GetProperty("correct").GetInt32());
        Assert.Equal(10, mine.GetProperty("skills").EnumerateArray().Sum(x => x.GetProperty("tasks").GetInt32()));
        Assert.Equal(9, mine.GetProperty("skills").EnumerateArray().Sum(x => x.GetProperty("correct").GetInt32()));

        exam = await Json(await Put(client, $"{examPath}/show-results", new { show = true, version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var released = await Json(await student.GetAsync($"/api/exam-take/{token}/attempt"), HttpStatusCode.OK);
        Assert.Equal(9, released.GetProperty("result").GetProperty("correct").GetInt32());

        exam = await Json(await Post(client, $"{examPath}/confirm", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        Assert.Equal("confirmed", exam.GetProperty("status").GetString());
        var assessment = await Json(await client.GetAsync(Learning(classId, $"/assessments/{exam.GetProperty("assessmentId").GetString()}")), HttpStatusCode.OK);
        var results = assessment.GetProperty("submissions").EnumerateArray().ToDictionary(x => x.GetProperty("studentId").GetString()!);
        Assert.Equal("assessed", results[students[0]].GetProperty("finalResult").GetProperty("status").GetString());
        Assert.Equal(100, results[students[0]].GetProperty("finalResult").GetProperty("completion").GetInt32());
        Assert.Equal(100, results[students[0]].GetProperty("finalResult").GetProperty("correctness").GetInt32());
        Assert.Equal("not_done", results[students[1]].GetProperty("finalResult").GetProperty("status").GetString());

        var skillResults = await Json(await client.GetAsync(Learning(classId, $"/assessments/{exam.GetProperty("assessmentId").GetString()}/skills")), HttpStatusCode.OK);
        Assert.Contains("confirmed", skillResults.ToString());
    }

    /// <summary>Opens an exam of the given questions, lets each student answer every item with the given values and confirms it.</summary>
    private async Task<JsonElement> RunExam(HttpClient client, string classId, object scope, int[] counts, Func<JsonElement, string[]> answer, params string[] students)
    {
        var body = JsonSerializer.SerializeToElement(scope);
        var preview = await Json(await Post(client, Learning(classId, "/exams/preview"), Merge(body, new { counts })), HttpStatusCode.OK);
        var ids = preview.GetProperty("questions").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
        var exam = await Json(await Post(client, Learning(classId, "/exams"), Merge(body, new { title = "Đề E1", counts, questionIds = ids })), HttpStatusCode.Created);
        var path = Learning(classId, $"/exams/{exam.GetProperty("id").GetString()}");
        exam = await Json(await Post(client, $"{path}/open", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var token = exam.GetProperty("publicToken").GetString()!;
        var items = exam.GetProperty("items").EnumerateArray().ToList();
        foreach (var studentId in students)
        {
            var code = exam.GetProperty("seats").EnumerateArray().Single(x => x.GetProperty("studentId").GetString() == studentId).GetProperty("code").GetString();
            using var device = app.Client();
            await Json(await Post(device, $"/api/exam-take/{token}/join", new { studentId, code }), HttpStatusCode.OK);
            foreach (var item in items)
                await Json(await Put(device, $"/api/exam-take/{token}/attempt/answers/{item.GetProperty("order").GetInt32()}", new { values = answer(item) }), HttpStatusCode.OK);
            await Json(await Post(device, $"/api/exam-take/{token}/attempt/submit", new { }), HttpStatusCode.OK);
        }
        exam = await Json(await client.GetAsync(path), HttpStatusCode.OK);
        exam = await Json(await Post(client, $"{path}/close", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        return await Json(await Post(client, $"{path}/confirm", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
    }

    private static Dictionary<string, object?> Merge(JsonElement scope, object extra)
    {
        var result = scope.EnumerateObject().ToDictionary(x => x.Name, x => (object?)x.Value);
        foreach (var p in JsonSerializer.SerializeToElement(extra).EnumerateObject()) result[p.Name] = p.Value;
        return result;
    }

    [Fact]
    public async Task Exam_results_are_pooled_so_two_short_exams_reach_a_conclusion_rule_34_could_not()
    {
        var (client, classId, students) = await Teacher("Đỗ Khoa");
        await ImportSample(client, classId);
        var curriculum = await Json(await client.GetAsync(Learning(classId, "/curriculum")), HttpStatusCode.OK);
        var e1 = curriculum.GetProperty("skills").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "E1");
        await Json(await Put(client, Learning(classId, $"/curriculum/skills/{e1.GetProperty("id").GetString()}"), new
        {
            code = "E1", name = e1.GetProperty("name").GetString(), strand = e1.GetProperty("strand").GetString(), taught = true, archived = false,
            version = curriculum.GetProperty("version").GetInt32()
        }), HttpStatusCode.OK);

        // E1 has two Nhận biết and one Vận dụng cao question in the sample: each exam asks 3 questions of E1, all answered wrong.
        static string[] Wrong(JsonElement item) => item.GetProperty("answer").EnumerateArray().Select(_ => "sai").ToArray();
        var scope = new { skillIds = new[] { e1.GetProperty("id").GetString() } };
        await RunExam(client, classId, scope, [2, 0, 0, 1], Wrong, students[0]);
        var afterOne = await Json(await client.GetAsync(Learning(classId, "/competency")), HttpStatusCode.OK);
        Assert.Equal("insufficient", Skill(afterOne, "E1").GetProperty("state").GetString());

        await RunExam(client, classId, scope, [2, 0, 0, 1], Wrong, students[0]);
        var afterTwo = await Json(await client.GetAsync(Learning(classId, "/competency")), HttpStatusCode.OK);
        var e1State = Skill(afterTwo, "E1");
        Assert.Equal("not_mastered", e1State.GetProperty("state").GetString());
        Assert.Equal(2, e1State.GetProperty("evidence").EnumerateArray().Count(x => x.GetProperty("counted").GetBoolean()));

        static JsonElement Skill(JsonElement profile, string code) =>
            profile.GetProperty("students")[0].GetProperty("skills").EnumerateArray().Single(x => x.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task A_student_who_is_locked_out_gets_back_in_after_the_teacher_resets_the_seat()
    {
        var (client, classId, students) = await Teacher("Phạm Hoa");
        await ImportSample(client, classId);
        var preview = await Json(await Post(client, Learning(classId, "/exams/preview"), new { counts = new[] { 2, 0, 0, 0 }, lessonFrom = 1, lessonTo = 2 }), HttpStatusCode.OK);
        var exam = await Json(await Post(client, Learning(classId, "/exams"), new
        {
            title = "Tia số", counts = new[] { 2, 0, 0, 0 }, lessonFrom = 1, lessonTo = 2,
            questionIds = preview.GetProperty("questions").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray()
        }), HttpStatusCode.Created);
        var examPath = Learning(classId, $"/exams/{exam.GetProperty("id").GetString()}");
        exam = await Json(await Post(client, $"{examPath}/open", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var token = exam.GetProperty("publicToken").GetString()!;
        var code = exam.GetProperty("seats")[0].GetProperty("code").GetString()!;
        var bad = code == "9999" ? "0000" : "9999";

        using var student = app.Client();
        for (var i = 0; i < 4; i++) Assert.Equal(HttpStatusCode.BadRequest, (await Post(student, $"/api/exam-take/{token}/join", new { studentId = students[0], code = bad })).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await Post(student, $"/api/exam-take/{token}/join", new { studentId = students[0], code = bad })).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await Post(student, $"/api/exam-take/{token}/join", new { studentId = students[0], code })).StatusCode);

        exam = await Json(await client.GetAsync(examPath), HttpStatusCode.OK);
        await Json(await Post(client, $"{examPath}/seats/{students[0]}/reset", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        await Json(await Post(student, $"/api/exam-take/{token}/join", new { studentId = students[0], code }), HttpStatusCode.OK);
    }
}
