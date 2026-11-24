using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
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
        Assert.Equal(4, seat.GetProperty("skills").EnumerateArray().Sum(x => x.GetProperty("tasks").GetInt32()));   // the essay counts as a task, waiting to be scored

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

    // ---- Excel (#113) ----

    private static async Task<byte[]> Template(HttpClient client, string classId)
    {
        var response = await client.GetAsync(Learning(classId, "/import-template"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static async Task<JsonElement> Import(HttpClient client, string classId, byte[] workbook, bool commit)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "cau-hoi.xlsx");
        return await Json(await TestApplication.PostForm(client, Learning(classId, $"/imports?commit={commit.ToString().ToLowerInvariant()}"), form), HttpStatusCode.OK);
    }

    /// <summary>The template with extra question rows, each given by header name; the first sample skill is used when none is named.</summary>
    private static byte[] WithRows(byte[] template, Action<IXLWorksheet> edit)
    {
        using var book = new XLWorkbook(new MemoryStream(template));
        edit(book.Worksheet("Câu hỏi"));
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddRow(IXLWorksheet sheet, Dictionary<string, string> values)
    {
        var header = sheet.Row(1);
        var row = sheet.LastRowUsed()!.RowNumber() + 1;
        var skill = sheet.Cell(2, Column(sheet, "Kỹ năng chính")).GetString();
        sheet.Cell(row, Column(sheet, "Kỹ năng chính")).Value = skill;
        sheet.Cell(row, Column(sheet, "Mức độ")).Value = "Nhận biết";
        foreach (var (name, value) in values) sheet.Cell(row, Column(sheet, name)).Value = value;
        _ = header;
    }

    private static int Column(IXLWorksheet sheet, string header) =>
        sheet.Row(1).CellsUsed().First(c => c.GetString() == header).Address.ColumnNumber;

    [Fact]
    public async Task The_workbook_imports_all_four_kinds_with_points_and_importing_again_changes_nothing()
    {
        var (client, classId, _, _) = await Setup();
        var workbook = WithRows(await Template(client, classId), sheet =>
        {
            AddRow(sheet, new() { ["Mã câu"] = "MULTI1", ["Dạng"] = "Nhiều đáp án", ["Đề bài"] = "Chọn các số chẵn", ["Phương án A"] = "2", ["Phương án B"] = "3", ["Phương án C"] = "4",
                ["Phương án D"] = "5", ["Phương án E"] = "6", ["Đáp án"] = "A, C, E", ["Điểm"] = "1,5" });
            AddRow(sheet, new() { ["Mã câu"] = "TYPED1", ["Dạng"] = "Điền từ", ["Đề bài"] = "Thủ đô của Việt Nam là [Hà Nội|Ha Noi], sông dài nhất là [Mê Công].", ["Điểm"] = "2" });
            AddRow(sheet, new() { ["Mã câu"] = "ESSAY1", ["Dạng"] = "Tự luận", ["Đề bài"] = "Giải thích vì sao", ["Đáp án"] = "Vì chia hết cho 2", ["Điểm"] = "3" });
            AddRow(sheet, new() { ["Mã câu"] = "OLDFILL", ["Dạng"] = "Điền ô", ["Đề bài"] = "8 + 5 = [ ]", ["Các nút để điền"] = "12; 13; 14", ["Đáp án"] = "13" });
        });

        var preview = await Import(client, classId, workbook, false);
        Assert.Empty(preview.GetProperty("issues").EnumerateArray());
        Assert.Equal(4, preview.GetProperty("questions").GetProperty("added").GetInt32());
        var committed = await Import(client, classId, workbook, true);
        Assert.True(committed.GetProperty("committed").GetBoolean(), committed.ToString());

        var bank = await Json(await client.GetAsync(Learning(classId, "/questions")), HttpStatusCode.OK);
        var byCode = bank.GetProperty("questions").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!);
        var multi = byCode["MULTI1"];
        Assert.Equal("multi_choice", multi.GetProperty("type").GetString());
        Assert.Equal(1.5m, multi.GetProperty("points").GetDecimal());
        Assert.Equal(["A", "C", "E"], multi.GetProperty("answer")[0].EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(5, multi.GetProperty("options").GetArrayLength());
        var typed = byCode["TYPED1"];
        Assert.Equal("fill_blanks", typed.GetProperty("type").GetString());
        Assert.Equal("type", typed.GetProperty("fillMode").GetString());
        Assert.Equal("Thủ đô của Việt Nam là {{1}}, sông dài nhất là {{2}}.", typed.GetProperty("stem").GetString());
        Assert.Equal(["Hà Nội", "Ha Noi"], typed.GetProperty("answer")[0].EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(2m, typed.GetProperty("points").GetDecimal());
        Assert.Equal("Vì chia hết cho 2", byCode["ESSAY1"].GetProperty("sampleAnswer").GetString());
        Assert.Equal(3m, byCode["ESSAY1"].GetProperty("points").GetDecimal());
        Assert.Equal("chips", byCode["OLDFILL"].GetProperty("fillMode").GetString());
        Assert.Equal(1m, byCode["OLDFILL"].GetProperty("points").GetDecimal());

        var again = await Import(client, classId, workbook, true);
        Assert.True(again.GetProperty("committed").GetBoolean());
        Assert.Equal(0, again.GetProperty("questions").GetProperty("added").GetInt32());
        Assert.Equal(0, again.GetProperty("questions").GetProperty("updated").GetInt32());
    }

    [Fact]
    public async Task A_workbook_whose_kind_column_is_still_called_Loai_imports_as_before()
    {
        var (client, classId, _, _) = await Setup();
        var workbook = WithRows(await Template(client, classId), sheet => sheet.Cell(1, Column(sheet, "Dạng")).Value = "Loại");
        var report = await Import(client, classId, workbook, true);
        Assert.True(report.GetProperty("committed").GetBoolean(), report.ToString());
        Assert.Empty(report.GetProperty("issues").EnumerateArray());
    }

    [Fact]
    public async Task Problems_in_the_new_columns_are_reported_by_row_and_column()
    {
        var (client, classId, _, _) = await Setup();
        var workbook = WithRows(await Template(client, classId), sheet =>
        {
            AddRow(sheet, new() { ["Mã câu"] = "B1", ["Dạng"] = "Nhiều đáp án", ["Đề bài"] = "x", ["Phương án A"] = "1", ["Phương án B"] = "2", ["Phương án C"] = "3", ["Đáp án"] = "A", ["Điểm"] = "1" });
            AddRow(sheet, new() { ["Mã câu"] = "B2", ["Dạng"] = "Điền từ", ["Đề bài"] = "Một [đáp án] và một [ ] trống", ["Điểm"] = "1" });
            AddRow(sheet, new() { ["Mã câu"] = "B3", ["Dạng"] = "Tự luận", ["Đề bài"] = "Giải thích", ["Điểm"] = "nhiều" });
            AddRow(sheet, new() { ["Mã câu"] = "B4", ["Dạng"] = "Tự luận", ["Đề bài"] = "Giải thích", ["Điểm"] = "0,3" });
            AddRow(sheet, new() { ["Mã câu"] = "B5", ["Dạng"] = "Bài tập", ["Đề bài"] = "x" });
        });
        var report = await Import(client, classId, workbook, true);
        Assert.False(report.GetProperty("committed").GetBoolean());
        var issues = report.GetProperty("issues").EnumerateArray().Select(x => (Row: x.GetProperty("row").GetInt32(), Column: x.GetProperty("column").GetString(),
            Message: x.GetProperty("message").GetString()!)).ToList();
        var first = AddedRow(workbook, 1);
        Assert.Contains(issues, x => x.Row == first && x.Column == "Đáp án");          // multiple choice needs two or more keys
        Assert.Contains(issues, x => x.Row == first + 1 && x.Column == "Đề bài");      // mixed [ ] and [answer]
        Assert.Contains(issues, x => x.Row == first + 2 && x.Column == "Điểm");        // not a number
        Assert.Contains(issues, x => x.Row == first + 3 && x.Column == "Điểm");        // not a quarter step
        Assert.Contains(issues, x => x.Row == first + 4 && x.Column == "Dạng");        // unknown kind
    }

    private static int AddedRow(byte[] workbook, int index)
    {
        using var book = new XLWorkbook(new MemoryStream(workbook));
        var sheet = book.Worksheet("Câu hỏi");
        return sheet.LastRowUsed()!.RowNumber() - 5 + index;
    }

    [Fact]
    public async Task Copying_a_class_keeps_the_typed_blanks_points_and_sample_answers()
    {
        var (client, classId, skill, _) = await Setup();
        await Create(client, classId, new
        {
            code = "K1", type = "fill_blanks", fillMode = "type", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(),
            stem = "{{1}} cộng {{2}} bằng 5", options = Array.Empty<object>(), answer = new[] { new[] { "2", "hai" }, new[] { "3", "ba" } }, points = 2, version = 0
        });
        await Create(client, classId, new
        {
            code = "K2", type = "essay", level = "van_dung", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Giải thích",
            options = Array.Empty<object>(), answer = Array.Empty<string[]>(), sampleAnswer = "mẫu", points = 4, version = 0
        });
        await Create(client, classId, new
        {
            code = "K3", type = "multi_choice", level = "thong_hieu", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Chọn",
            options = Options("1", "2", "3", "4", "5", "6"), answer = new[] { new[] { "B", "F" } }, points = 0.5, version = 0
        });
        var target = await Json(await TestApplication.Post(client, "/api/classes", new { name = "Lớp đích", subject = "Toán" }), HttpStatusCode.Created);
        var targetId = target.GetProperty("id").GetString()!;
        var report = await Json(await TestApplication.Post(client, Learning(targetId, $"/copy-from/{classId}?commit=true"), new { }), HttpStatusCode.OK);
        Assert.True(report.GetProperty("committed").GetBoolean(), report.ToString());

        var bank = await Json(await client.GetAsync(Learning(targetId, "/questions")), HttpStatusCode.OK);
        var byCode = bank.GetProperty("questions").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!);
        Assert.Equal("{{1}} cộng {{2}} bằng 5", byCode["K1"].GetProperty("stem").GetString());
        Assert.Equal("type", byCode["K1"].GetProperty("fillMode").GetString());
        Assert.Equal(["2", "hai"], byCode["K1"].GetProperty("answer")[0].EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(2m, byCode["K1"].GetProperty("points").GetDecimal());
        Assert.Equal("mẫu", byCode["K2"].GetProperty("sampleAnswer").GetString());
        Assert.Equal(4m, byCode["K2"].GetProperty("points").GetDecimal());
        Assert.Equal(["B", "F"], byCode["K3"].GetProperty("answer")[0].EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(0.5m, byCode["K3"].GetProperty("points").GetDecimal());
    }

    // ---- Grading by the teacher (#116) ----

    [Fact]
    public async Task The_teacher_scores_an_essay_and_the_total_the_student_page_and_the_confirmation_follow()
    {
        var (client, classId, skill, studentId) = await Setup();
        var essay = await Create(client, classId, new
        {
            code = "G1", type = "essay", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "Giải thích",
            options = Array.Empty<object>(), answer = Array.Empty<string[]>(), sampleAnswer = "mẫu", points = 3, version = 0
        });
        var single = await Create(client, classId, new
        {
            code = "G2", type = "single_choice", level = "nhan_biet", primarySkillId = skill, secondarySkillIds = Array.Empty<string>(), stem = "1 + 1 = ?",
            options = Options("1", "2"), answer = new[] { new[] { "B" } }, points = 2, version = 0
        });
        var exam = await Json(await Post(client, Learning(classId, "/exams"), new
        {
            title = "Chấm tay", counts = new[] { 2, 0, 0, 0 }, skillIds = new[] { skill }, durationMinutes = 30, questionIds = new[] { essay, single }
        }), HttpStatusCode.Created);
        var examPath = Learning(classId, $"/exams/{exam.GetProperty("id").GetString()}");
        exam = await Json(await Post(client, $"{examPath}/open", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var token = exam.GetProperty("publicToken").GetString()!;
        var code = exam.GetProperty("seats").EnumerateArray().Single().GetProperty("code").GetString()!;
        var order = exam.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("questionId").GetString()!, x => x.GetProperty("order").GetInt32());

        using var student = app.Client();
        await Json(await Post(student, $"/api/exam-take/{token}/join", new { studentId, code }), HttpStatusCode.OK);
        await Json(await Put(student, $"/api/exam-take/{token}/attempt/answers/{order[essay]}", new { values = new[] { "Vì 4 chia hết cho 2." } }), HttpStatusCode.OK);
        await Json(await Put(student, $"/api/exam-take/{token}/attempt/answers/{order[single]}", new { values = new[] { "B" } }), HttpStatusCode.OK);
        await Json(await Post(student, $"/api/exam-take/{token}/attempt/submit", new { }), HttpStatusCode.OK);
        exam = await Json(await client.GetAsync(examPath), HttpStatusCode.OK);
        exam = await Json(await Post(client, $"{examPath}/close", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);

        // Before any scoring the essay is waiting, and the results cannot be confirmed.
        var board = await Json(await client.GetAsync($"{examPath}/grading"), HttpStatusCode.OK);
        Assert.Equal(1, board.GetProperty("pending").GetInt32());
        var response = board.GetProperty("essays")[0].GetProperty("responses")[0];
        Assert.Equal("Vì 4 chia hết cho 2.", response.GetProperty("answer").GetString());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("points").ValueKind);
        Assert.Equal("mẫu", board.GetProperty("essays")[0].GetProperty("sampleAnswer").GetString());
        var early = await Post(client, $"{examPath}/confirm", new { version = exam.GetProperty("version").GetInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains("chưa chấm", await early.Content.ReadAsStringAsync());

        string Grade(int o) => $"{examPath}/seats/{studentId}/grades/{o}";
        // Refused: over the points, not a quarter step, a remark too long, a question that is not an essay, an unknown order.
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[essay]), new { points = 3.25 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[essay]), new { points = 2.3 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[essay]), new { points = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[essay]), new { points = 1, comment = new string('x', 501) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[single]), new { points = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(client, Grade(99), new { points = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[essay]), new { })).StatusCode);

        var first = await Json(await Put(client, Grade(order[essay]), new { points = 2.5, comment = "Đúng ý, thiếu ví dụ" }), HttpStatusCode.OK);
        Assert.Equal(4.5m, first.GetProperty("score").GetDecimal());   // 2 for the single choice + 2.5
        Assert.Equal(5m, first.GetProperty("maxScore").GetDecimal());
        Assert.Equal(0, first.GetProperty("pendingEssays").GetInt32());
        var again = await Json(await Put(client, Grade(order[essay]), new { points = 2.75, comment = "Đúng ý, thiếu ví dụ" }), HttpStatusCode.OK);
        Assert.Equal(4.75m, again.GetProperty("score").GetDecimal());   // scoring again replaces
        board = await Json(await client.GetAsync($"{examPath}/grading"), HttpStatusCode.OK);
        Assert.Equal(0, board.GetProperty("pending").GetInt32());
        Assert.Equal(2.75m, board.GetProperty("essays")[0].GetProperty("responses")[0].GetProperty("points").GetDecimal());

        exam = await Json(await client.GetAsync(examPath), HttpStatusCode.OK);
        exam = await Json(await Put(client, $"{examPath}/show-results", new { show = true, version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        var released = await Json(await student.GetAsync($"/api/exam-take/{token}/attempt"), HttpStatusCode.OK);
        var result = released.GetProperty("result");
        Assert.Equal(4.75m, result.GetProperty("score").GetDecimal());
        Assert.Equal(0, result.GetProperty("pendingEssays").GetInt32());
        var essayItem = result.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "essay");
        Assert.True(essayItem.GetProperty("graded").GetBoolean());
        Assert.Equal(2.75m, essayItem.GetProperty("score").GetDecimal());
        Assert.Equal("Đúng ý, thiếu ví dụ", essayItem.GetProperty("comment").GetString());

        exam = await Json(await Post(client, $"{examPath}/confirm", new { version = exam.GetProperty("version").GetInt32() }), HttpStatusCode.OK);
        Assert.Equal("confirmed", exam.GetProperty("status").GetString());
        var skills = exam.GetProperty("seats").EnumerateArray().Single().GetProperty("skills").EnumerateArray().Single();
        Assert.Equal(2, skills.GetProperty("tasks").GetInt32());
        Assert.Equal(2, skills.GetProperty("correct").GetInt32());       // 4.75 of 5 points over 2 tasks rounds to 2 right questions
        Assert.Equal(4.75m, skills.GetProperty("earned").GetDecimal());
        // Once confirmed the scores are final.
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, Grade(order[essay]), new { points = 1 })).StatusCode);
    }
}
