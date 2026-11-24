using ClosedXML.Excel;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Infrastructure;

/// <summary>
/// The Excel template the teacher downloads and uploads back: a guide, the class information, skills, lessons and
/// questions. Columns are found by their header text, so the teacher may reorder or add columns without breaking it.
/// </summary>
public static class WorkbookFormat
{
    public static readonly string[] InfoKeys = ["Môn học", "Khối lớp", "Học kỳ", "Bộ sách"];
    public static readonly string[] SkillColumns = ["Mã kỹ năng", "Tên kỹ năng", "Mạch nội dung", "Tiêu chí đạt (chuẩn đầu ra)"];
    public static readonly string[] LessonColumns = ["Buổi", "Chủ đề", "Nội dung (SGK)", "Kiến thức", "Kỹ năng cần đạt", "Chuẩn đầu ra", "Năng lực – thái độ", "Mã kỹ năng"];
    public static readonly string[] QuestionColumns =
        ["Mã câu", "Dạng", "Mức độ", "Kỹ năng chính", "Kỹ năng phụ", "Buổi", "Đề bài", "Ảnh đề", "Phương án A", "Phương án B", "Phương án C", "Phương án D", "Các nút để điền", "Đáp án",
         "Phương án E", "Phương án F", "Điểm"];
    /// <summary>Headers of older workbooks that still read: the "Dạng" column used to be called "Loại".</summary>
    private static readonly Dictionary<string, string> HeaderAliases = new() { ["Dạng"] = "Loại" };
    private const int MaxRows = 2000;
    private const string Lists = "Danh mục";

    public static byte[] BuildTemplate()
    {
        using var book = new XLWorkbook();
        var guide = book.AddWorksheet(ImportSheets.Guide);
        string[] lines =
        [
            "HƯỚNG DẪN ĐIỀN FILE CHƯƠNG TRÌNH HỌC VÀ NGÂN HÀNG CÂU HỎI",
            "",
            "File này đã điền sẵn chương trình Toán 2 – Học kỳ 1 (Kết nối tri thức) làm ví dụ. Có thể sửa, xóa bớt hoặc thêm dòng rồi tải lên.",
            "Tải lên nhiều lần không bị trùng: dòng có cùng Mã kỹ năng / số Buổi / Mã câu sẽ được cập nhật, không tạo mới. Hệ thống không tự xóa gì.",
            "Trước khi ghi, hệ thống hiện bản xem trước: bao nhiêu dòng thêm mới, cập nhật và các dòng lỗi (ghi rõ sheet, dòng, cột).",
            "",
            "SHEET \"Thông tin chung\": môn học, khối lớp, học kỳ, bộ sách. Chỉ dùng khi lớp chưa có chương trình.",
            "",
            "SHEET \"Kỹ năng\": mỗi dòng là một kỹ năng có mã, dùng để gắn vào buổi học và câu hỏi.",
            "  • Mã kỹ năng (bắt buộc, tối đa 20 ký tự, không trùng), ví dụ E1.",
            "  • Tên kỹ năng (bắt buộc). Mạch nội dung và Tiêu chí đạt (chuẩn đầu ra của kỹ năng) không bắt buộc.",
            "",
            "SHEET \"Buổi\": mỗi dòng là một buổi trong chương trình (Buổi 1, 2, 3...).",
            "  • Buổi (bắt buộc, là số) và Nội dung (SGK) (bắt buộc).",
            "  • Kỹ năng cần đạt, Chuẩn đầu ra, Kiến thức, Năng lực – thái độ: viết tự do.",
            "  • Mã kỹ năng: các mã trong sheet Kỹ năng, cách nhau bằng dấu phẩy, ví dụ: E1, E3.",
            "  • Buổi học thật thứ N của lớp (không tính buổi bị hủy) được tự gắn với Buổi N. Có thể đổi trên web.",
            "",
            "SHEET \"Câu hỏi\": mỗi dòng là một câu trong ngân hàng câu hỏi.",
            "  • Mã câu (bắt buộc, không trùng). Dạng: Trắc nghiệm, Nhiều đáp án, Điền ô, Điền từ hoặc Tự luận (cột cũ tên \"Loại\" vẫn dùng được).",
            "  • Điểm (không bắt buộc): từ 0,25 đến 10, bước 0,25. Để trống thì tính 1 điểm.",
            "  • Mức độ: Nhận biết, Thông hiểu, Vận dụng hoặc Vận dụng cao.",
            "  • Kỹ năng chính (bắt buộc, một mã, được dùng để tính kết quả). Kỹ năng phụ: tối đa 2 mã, chỉ để lọc câu.",
            "  • Buổi: số buổi của câu (không bắt buộc).",
            "  • Ảnh đề / ảnh phương án: ghi đúng tên file ảnh (ví dụ dongho3.png) rồi chọn các file ảnh đó cùng lúc khi tải lên.",
            "",
            "  Câu TRẮC NGHIỆM: điền 2–4 phương án vào cột Phương án A–D. Phương án là ảnh thì ghi: ảnh: tên-file.png",
            "    Đáp án: một chữ cái A, B, C hoặc D.",
            "",
            "  Câu NHIỀU ĐÁP ÁN: điền 3–6 phương án vào cột Phương án A–F.",
            "    Đáp án: từ hai chữ cái trở lên, cách nhau bằng dấu phẩy, ví dụ: A, C. Chỉ đúng khi chọn đúng và đủ.",
            "",
            "  Câu ĐIỀN TỪ (học sinh gõ chữ vào chỗ trống): viết đáp án ngay trong Đề bài, trong ngoặc vuông.",
            "    Ví dụ: Thủ đô của Việt Nam là [Hà Nội|Ha Noi].   Một chỗ có nhiều đáp án đúng thì ngăn bằng dấu |",
            "    Tối đa 10 chỗ trống. Không cần điền cột Các nút để điền và Đáp án. Không dùng lẫn [ ] và [đáp án] trong một câu.",
            "    Chữ hoa/thường và khoảng trắng thừa không quan trọng; dấu tiếng Việt có quan trọng. 0,5 và 0.5 được coi là bằng nhau.",
            "",
            "  Câu TỰ LUẬN (giáo viên chấm tay): chỉ cần Đề bài. Cột Đáp án để trống hoặc ghi đáp án mẫu để đối chiếu khi chấm.",
            "",
            "  Câu ĐIỀN Ô: trong Đề bài, mỗi ô trống viết là [ ]  (ví dụ: 8 + 5 = [ ]).",
            "    Các nút để điền: các nút học sinh bấm để điền, cách nhau bằng dấu chấm phẩy, ví dụ: 12; 13; 14; 15",
            "    Đáp án: đáp án của từng ô theo thứ tự, cách nhau bằng dấu chấm phẩy. Một ô có nhiều đáp án đúng thì ngăn bằng dấu /",
            "    Ví dụ đề \"6 chục 4 đơn vị: [ ] chục [ ] đơn vị\" có đáp án: 6; 4",
            "    Mỗi đáp án phải là một trong các nút để điền. Câu chỉ được tính đúng khi mọi ô đều đúng."
        ];
        for (var i = 0; i < lines.Length; i++) guide.Cell(i + 1, 1).Value = lines[i];
        guide.Cell(1, 1).Style.Font.Bold = true;
        guide.Cell(1, 1).Style.Font.FontSize = 14;
        guide.Column(1).Width = 120;

        var info = book.AddWorksheet(ImportSheets.Info);
        Header(info, ["Mục", "Giá trị"]);
        string[] values = [Toan2Hk1Sample.Info.Subject!, Toan2Hk1Sample.Info.Grade!, Toan2Hk1Sample.Info.Term!, Toan2Hk1Sample.Info.Textbook!];
        for (var i = 0; i < InfoKeys.Length; i++) { info.Cell(i + 2, 1).Value = InfoKeys[i]; info.Cell(i + 2, 2).Value = values[i]; }
        info.Columns().AdjustToContents();

        var skills = book.AddWorksheet(ImportSheets.Skills);
        Header(skills, SkillColumns);
        var row = 2;
        foreach (var skill in Toan2Hk1Sample.Skills) Fill(skills, row++, [skill.Code, skill.Name, skill.Strand, skill.Criteria]);
        Widths(skills, [14, 45, 22, 70]);

        var lessons = book.AddWorksheet(ImportSheets.Lessons);
        Header(lessons, LessonColumns);
        row = 2;
        foreach (var lesson in Toan2Hk1Sample.Lessons)
            Fill(lessons, row++, [lesson.No.ToString(), lesson.Topic, lesson.Title, lesson.Knowledge, lesson.SkillsText, lesson.Outcomes, lesson.Competencies, lesson.Codes]);
        lessons.Column(1).Style.NumberFormat.Format = "0";
        Widths(lessons, [8, 34, 45, 40, 45, 45, 26, 18]);

        var questions = book.AddWorksheet(ImportSheets.Questions);
        Header(questions, QuestionColumns);
        row = 2;
        foreach (var question in Toan2Hk1Sample.Questions) Fill(questions, row++, question);
        Widths(questions, [10, 13, 14, 13, 12, 7, 55, 14, 16, 16, 16, 16, 22, 10, 16, 16, 8]);

        var lists = book.AddWorksheet(Lists);
        lists.Cell(1, 1).Value = "Dạng";
        lists.Cell(2, 1).Value = QuestionType.Labels[QuestionType.SingleChoice];
        lists.Cell(3, 1).Value = QuestionType.Labels[QuestionType.MultiChoice];
        lists.Cell(4, 1).Value = QuestionType.Labels[QuestionType.FillBlanks];
        lists.Cell(5, 1).Value = QuestionType.TypedLabel;
        lists.Cell(6, 1).Value = QuestionType.Labels[QuestionType.Essay];
        lists.Cell(1, 2).Value = "Mức độ";
        for (var i = 0; i < QuestionLevel.All.Length; i++) lists.Cell(i + 2, 2).Value = QuestionLevel.Labels[QuestionLevel.All[i]];
        lists.Visibility = XLWorksheetVisibility.Hidden;
        questions.Range("B2:B1000").CreateDataValidation().List(lists.Range("A2:A6"), true);
        questions.Range("C2:C1000").CreateDataValidation().List(lists.Range("B2:B5"), true);
        questions.Range("D2:D1000").CreateDataValidation().List(skills.Range("A2:A300"), true);
        questions.Column(7).Style.Alignment.WrapText = true;
        guide.SetTabActive();

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Reads a workbook; format problems become issues, never exceptions. Missing sheets are simply empty.</summary>
    public static ImportBundle Read(Stream stream)
    {
        var issues = new List<ImportIssue>();
        XLWorkbook book;
        try { book = new XLWorkbook(stream); }
        catch (Exception)
        {
            return new(new(null, null, null, null), [], [], [], [new("", 0, "", "Không đọc được file. Hãy tải lên file Excel (.xlsx) theo mẫu.")]);
        }
        using (book)
        {
            var info = ReadInfo(book);
            var skills = new List<SkillImportRow>();
            var lessons = new List<LessonImportRow>();
            var questions = new List<QuestionImportRow>();
            if (Sheet(book, ImportSheets.Skills) is { } skillSheet && Columns(skillSheet, ImportSheets.Skills, SkillColumns, [0, 1], issues) is { } sc)
                foreach (var (r, get) in Rows(skillSheet, sc))
                {
                    var code = get(0); var name = get(1);
                    if (code.Length == 0) { issues.Add(new(ImportSheets.Skills, r, SkillColumns[0], "Thiếu mã kỹ năng.")); continue; }
                    if (skills.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { issues.Add(new(ImportSheets.Skills, r, SkillColumns[0], $"Mã \"{code}\" bị trùng trong file.")); continue; }
                    if (name.Length == 0) { issues.Add(new(ImportSheets.Skills, r, SkillColumns[1], "Thiếu tên kỹ năng.")); continue; }
                    skills.Add(new(r, code, name, get(2), Null(get(3))));
                }
            if (Sheet(book, ImportSheets.Lessons) is { } lessonSheet && Columns(lessonSheet, ImportSheets.Lessons, LessonColumns, [0, 2], issues) is { } lc)
                foreach (var (r, get) in Rows(lessonSheet, lc))
                {
                    if (!int.TryParse(get(0).Replace("Buổi", "", StringComparison.OrdinalIgnoreCase).Trim(), out var no) || no < 1)
                    { issues.Add(new(ImportSheets.Lessons, r, LessonColumns[0], "Buổi phải là số nguyên dương.")); continue; }
                    if (lessons.Any(x => x.No == no)) { issues.Add(new(ImportSheets.Lessons, r, LessonColumns[0], $"Buổi {no} bị trùng trong file.")); continue; }
                    if (get(2).Length == 0) { issues.Add(new(ImportSheets.Lessons, r, LessonColumns[2], "Thiếu nội dung buổi.")); continue; }
                    lessons.Add(new(r, no, get(1), get(2), get(3), get(5), get(4), get(6), Split(get(7), ',', ';')));
                }
            if (Sheet(book, ImportSheets.Questions) is { } questionSheet && Columns(questionSheet, ImportSheets.Questions, QuestionColumns, [0, 1, 2, 3, 6, 13], issues, HeaderAliases) is { } qc)
                foreach (var (r, get) in Rows(questionSheet, qc))
                {
                    var code = get(0);
                    if (code.Length == 0) { issues.Add(new(ImportSheets.Questions, r, QuestionColumns[0], "Thiếu mã câu.")); continue; }
                    if (questions.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) { issues.Add(new(ImportSheets.Questions, r, QuestionColumns[0], $"Mã câu \"{code}\" bị trùng trong file.")); continue; }
                    int? lesson = null;
                    if (get(5).Length > 0)
                    {
                        if (int.TryParse(get(5), out var n) && n > 0) lesson = n;
                        else { issues.Add(new(ImportSheets.Questions, r, QuestionColumns[5], "Buổi phải là số nguyên dương hoặc để trống.")); continue; }
                    }
                    questions.Add(new(r, code, get(1), get(2), get(3), Split(get(4), ',', ';'), lesson, get(6), Null(get(7)),
                        [get(8), get(9), get(10), get(11), get(14), get(15)], Split(get(12), ';'), get(13), get(16)));
                }
            return new(info, skills, lessons, questions, issues);
        }
    }

    private static ImportInfo ReadInfo(XLWorkbook book)
    {
        var sheet = Sheet(book, ImportSheets.Info);
        if (sheet is null) return new(null, null, null, null);
        string? Value(string key)
        {
            foreach (var row in sheet.RowsUsed())
                if (Same(row.Cell(1).GetString(), key)) return Null(row.Cell(2).GetFormattedString().Trim());
            return null;
        }
        return new(Value(InfoKeys[0]), Value(InfoKeys[1]), Value(InfoKeys[2]), Value(InfoKeys[3]));
    }

    private static IXLWorksheet? Sheet(XLWorkbook book, string name) => book.Worksheets.FirstOrDefault(x => Same(x.Name, name));

    /// <summary>Column numbers by expected header; required headers missing become one issue and the sheet is skipped.</summary>
    private static int[]? Columns(IXLWorksheet sheet, string sheetName, string[] expected, int[] required, List<ImportIssue> issues,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        var header = sheet.Row(1);
        var last = Math.Max(header.LastCellUsed()?.Address.ColumnNumber ?? 0, 1);
        var map = expected.Select(name => Enumerable.Range(1, last).FirstOrDefault(c =>
            Same(header.Cell(c).GetString(), name) || aliases is not null && aliases.TryGetValue(name, out var old) && Same(header.Cell(c).GetString(), old))).ToArray();
        var missing = required.Where(i => map[i] == 0).Select(i => expected[i]).ToList();
        if (missing.Count == 0) return map;
        issues.Add(new(sheetName, 1, string.Join(", ", missing), "Thiếu cột tiêu đề bắt buộc. Hãy dùng đúng file mẫu."));
        return null;
    }

    private static IEnumerable<(int Row, Func<int, string> Get)> Rows(IXLWorksheet sheet, int[] columns)
    {
        var last = Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 1, MaxRows + 1);
        for (var r = 2; r <= last; r++)
        {
            var row = sheet.Row(r);
            string Get(int i) => columns[i] == 0 ? "" : row.Cell(columns[i]).GetFormattedString().Trim();
            if (columns.Where(c => c > 0).All(c => row.Cell(c).GetFormattedString().Trim().Length == 0)) continue;
            yield return (r, Get);
        }
    }

    private static void Header(IXLWorksheet sheet, string[] names)
    {
        for (var i = 0; i < names.Length; i++) sheet.Cell(1, i + 1).Value = names[i];
        var range = sheet.Range(1, 1, 1, names.Length);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEBFA");
        sheet.SheetView.FreezeRows(1);
    }

    private static void Fill(IXLWorksheet sheet, int row, string[] values)
    {
        for (var i = 0; i < values.Length; i++)
            if (values[i].Length > 0)
            {
                if (int.TryParse(values[i], out var n) && i == 0 && sheet.Name == ImportSheets.Lessons) sheet.Cell(row, i + 1).Value = n;
                else sheet.Cell(row, i + 1).Value = values[i];
            }
    }

    private static void Widths(IXLWorksheet sheet, int[] widths)
    {
        for (var i = 0; i < widths.Length; i++) sheet.Column(i + 1).Width = widths[i];
    }

    private static string[] Split(string value, params char[] separators) =>
        value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool Same(string a, string b) => QuestionLevel.Normalize(a) == QuestionLevel.Normalize(b);
}
