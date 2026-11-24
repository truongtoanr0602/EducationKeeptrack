using System.Text.RegularExpressions;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
namespace EducationKeeptrack.Learning.Application;

/// <summary>
/// Imports a workbook (or another class's data) into the class: skills by code, lessons by number, questions by code.
/// Preview and commit run the very same steps on tracked entities; preview just never saves, so they cannot disagree.
/// </summary>
public sealed class ImportService(IQuestionBankRepository repository, IClassroomLearningSource classroom, TimeProvider clock)
{
    private static readonly string[] ImagePrefixes = ["ảnh:", "anh:", "img:", "image:"];

    public async Task<LearningResult<ImportReport>> ImportAsync(string owner, Guid classId, Stream file, bool commit, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        return await ApplyAsync(owner, classId, WorkbookFormat.Read(file), commit, ct);
    }

    /// <summary>Copies curriculum, lessons, questions and question images of another class of the same teacher.</summary>
    public async Task<LearningResult<ImportReport>> CopyAsync(string owner, Guid classId, Guid sourceClassId, bool commit, CancellationToken ct)
    {
        if (sourceClassId == classId) return new(null, 400, new() { ["source"] = ["Chọn một lớp khác để sao chép."] });
        if (!await OwnsAsync(owner, classId, ct) || !await OwnsAsync(owner, sourceClassId, ct)) return new(null, 404);
        var source = await repository.FindCurriculumAsync(owner, sourceClassId, false, ct);
        if (source is null) return new(null, 400, new() { ["source"] = ["Lớp nguồn chưa có chương trình."] });
        var codes = source.Skills.Where(x => !x.Archived).ToDictionary(x => x.Id, x => x.Code);
        var skills = source.Skills.Where(x => !x.Archived).Select(x => new SkillImportRow(0, x.Code, x.Name, x.Strand, x.Criteria)).ToList();
        var lessons = source.Lessons.Where(x => !x.Archived).OrderBy(x => x.No).Select(x => new LessonImportRow(0, x.No, x.Topic, x.Title, x.Knowledge, x.Outcomes,
            x.SkillsText, x.Competencies, [.. x.SkillIds.Where(codes.ContainsKey).Select(id => codes[id])])).ToList();
        var questions = (await repository.ListQuestionsAsync(owner, sourceClassId, false, ct)).Where(x => x.Status == QuestionStatus.Active && codes.ContainsKey(x.PrimarySkillId))
            .Select(x => ToRow(x, codes)).ToList();
        var targetImages = (await repository.ListImagesAsync(owner, classId, ct)).Select(x => x.NormalizedName).ToHashSet();
        foreach (var image in await repository.ListImagesAsync(owner, sourceClassId, ct))
            if (targetImages.Add(image.NormalizedName))
                repository.Add([new QuestionImage(owner, classId, image.FileName, image.StorageKey, image.ContentType, image.ByteLength, image.Sha256, clock.GetUtcNow())]);
        var bundle = new ImportBundle(new(source.Subject, source.Grade, source.Term, source.Textbook), skills, lessons, questions, []);
        return await ApplyAsync(owner, classId, bundle, commit, ct, copiedImages: true);
    }

    private async Task<LearningResult<ImportReport>> ApplyAsync(string owner, Guid classId, ImportBundle bundle, bool commit, CancellationToken ct, bool copiedImages = false)
    {
        var issues = bundle.Issues.ToList();
        var now = clock.GetUtcNow();
        var curriculum = await repository.FindCurriculumAsync(owner, classId, true, ct);
        var created = false;
        if (curriculum is null)
        {
            var input = new CurriculumInput(bundle.Info.Subject, bundle.Info.Grade, bundle.Info.Term, bundle.Info.Textbook,
                ClassCurriculum.DefaultThreshold, ClassCurriculum.DefaultMinTasks, ClassCurriculum.DefaultAttempts, 0);
            if (ClassCurriculum.Validate(input).Count > 0)
                issues.Add(new(ImportSheets.Info, 0, "", "Lớp chưa có chương trình: điền đủ Môn học, Khối lớp, Học kỳ, Bộ sách trong sheet Thông tin chung."));
            else
            {
                curriculum = new ClassCurriculum(owner, classId, input, now);
                repository.Add([curriculum]);
                created = true;
            }
        }
        int skillsAdded = 0, skillsUpdated = 0, lessonsAdded = 0, lessonsUpdated = 0, questionsAdded = 0, questionsUpdated = 0;
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (curriculum is not null)
        {
            var skillResult = curriculum.ImportSkills(bundle.Skills, owner, now);
            var lessonResult = curriculum.ImportLessons(bundle.Lessons, now);
            (skillsAdded, skillsUpdated, lessonsAdded, lessonsUpdated) = (skillResult.Added, skillResult.Updated, lessonResult.Added, lessonResult.Updated);
            issues.AddRange(skillResult.Issues);
            issues.AddRange(lessonResult.Issues);
            repository.Add(curriculum.TakeAdded());

            var skills = curriculum.GetEffectiveSkills().GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var live = skills.Values.ToHashSet();
            var images = (await repository.ListImagesAsync(owner, classId, ct)).Select(x => x.NormalizedName).ToHashSet();
            if (copiedImages) foreach (var row in bundle.Questions) foreach (var name in ImagesOf(row)) images.Add(QuestionImage.NormalizeName(name));
            var existing = await repository.ListQuestionsAsync(owner, classId, true, ct);
            foreach (var row in bundle.Questions)
            {
                foreach (var name in ImagesOf(row).Where(x => !images.Contains(QuestionImage.NormalizeName(x)))) missing.Add(name.Trim());
                var (input, rowIssues) = ToInput(row, skills);
                if (input is not null)
                {
                    var match = existing.FirstOrDefault(x => string.Equals(x.Code, row.Code, StringComparison.OrdinalIgnoreCase));
                    input = input with { Archived = match?.Status == QuestionStatus.Archived };
                    foreach (var (key, messages) in Question.Validate(input, live, images))
                        rowIssues.AddRange(messages.Select(m => new ImportIssue(ImportSheets.Questions, row.Row, ColumnOf(key, input.Type), m)));
                    if (rowIssues.Count == 0)
                    {
                        if (match is null)
                        {
                            var question = new Question(owner, classId, input, now);
                            existing.Add(question);
                            repository.Add([question]);
                            questionsAdded++;
                        }
                        else if (match.Apply(input, now)) questionsUpdated++;
                    }
                }
                issues.AddRange(rowIssues);
            }
        }
        var committed = false;
        if (commit && issues.Count == 0)
        {
            if (!await repository.SaveAsync(ct)) return new(null, 409);
            committed = true;
        }
        return new(new(committed, created, skillsAdded, skillsUpdated, lessonsAdded, lessonsUpdated, questionsAdded, questionsUpdated,
            issues.OrderBy(x => SheetOrder(x.Sheet)).ThenBy(x => x.Row).ToList(), [.. missing]));
    }

    /// <summary>Turns one sheet row into a request; problems the domain check cannot name (unknown labels or codes) are reported here.</summary>
    private static (QuestionInput? Input, List<ImportIssue> Issues) ToInput(QuestionImportRow row, IReadOnlyDictionary<string, Guid> skills)
    {
        var issues = new List<ImportIssue>();
        void Issue(int column, string message) => issues.Add(new(ImportSheets.Questions, row.Row, WorkbookFormat.QuestionColumns[column], message));
        var type = QuestionType.Parse(row.Type);
        if (type is null) Issue(1, "Dạng câu là Trắc nghiệm, Nhiều đáp án, Điền ô, Điền từ hoặc Tự luận.");
        decimal? points = null;
        if (row.Points.Length > 0)
        {
            if (decimal.TryParse(row.Points.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) points = parsed;
            else Issue(16, "Điểm phải là một số, ví dụ 1 hoặc 0,5.");
        }
        var level = QuestionLevel.Parse(row.Level);
        if (level is null) Issue(2, "Mức độ là Nhận biết, Thông hiểu, Vận dụng hoặc Vận dụng cao.");
        if (!skills.TryGetValue(row.PrimarySkill.Trim(), out var primary)) Issue(3, $"Không có kỹ năng mã \"{row.PrimarySkill}\".");
        var secondary = new List<Guid>();
        foreach (var code in row.SecondarySkills)
            if (skills.TryGetValue(code, out var id)) secondary.Add(id); else Issue(4, $"Không có kỹ năng mã \"{code}\".");
        if (issues.Count > 0) return (null, issues);
        QuestionOption[] options;
        string[][] answer;
        var stem = row.Stem;
        string? fillMode = null, sample = null;
        if (type is QuestionType.SingleChoice or QuestionType.MultiChoice)
        {
            string[] letters = ["A", "B", "C", "D", "E", "F"];
            var limit = type == QuestionType.SingleChoice ? 4 : letters.Length;
            options = row.Options.Take(limit).Select((text, i) => (text, i)).Where(x => x.text.Trim().Length > 0)
                .Select(x => IsImage(x.text, out var name) ? new QuestionOption(letters[x.i], "", name) : new QuestionOption(letters[x.i], x.text.Trim(), null)).ToArray();
            answer = type == QuestionType.SingleChoice ? [[row.Answer.Trim()]]
                : [row.Answer.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }
        else if (type == QuestionType.Essay)
        {
            options = [];
            answer = [];
            sample = row.Answer.Trim().Length == 0 ? null : row.Answer.Trim();
        }
        else if (TypedBlanks(row.Stem) is { } typed)
        {
            if (typed.Mixed) { Issue(6, "Không dùng lẫn [ ] và [đáp án] trong một câu."); return (null, issues); }
            (stem, answer) = (typed.Stem, typed.Answers);
            options = [];
            fillMode = QuestionType.FillType;
        }
        else
        {
            options = row.Chips.Select((text, i) => new QuestionOption((i + 1).ToString(), text, null)).ToArray();
            answer = row.Answer.Split(';', StringSplitOptions.TrimEntries).Select(x => x.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToArray();
            if (row.Answer.Trim().Length == 0) answer = [];
        }
        return (new QuestionInput(row.Code, type, level, primary, [.. secondary], row.LessonNo, stem, row.StemImage, options, answer, false, 0, points, fillMode, false, sample), issues);
    }

    private static QuestionImportRow ToRow(Question question, IReadOnlyDictionary<Guid, string> codes)
    {
        var options = question.Options;
        string[] letters = ["", "", "", "", "", ""];
        string[] chips = [];
        var stem = question.Stem;
        var label = QuestionType.Labels[question.Type];
        string answer;
        if (question.Type is QuestionType.SingleChoice or QuestionType.MultiChoice)
        {
            foreach (var option in options)
            {
                var index = "ABCDEF".IndexOf(option.Key, StringComparison.Ordinal);
                if (index >= 0) letters[index] = option.Image is { } image ? $"ảnh: {image}" : option.Text;
            }
            answer = question.Type == QuestionType.MultiChoice ? string.Join(", ", question.Answer.FirstOrDefault() ?? []) : string.Join("; ", question.Answer.Select(x => string.Join("/", x)));
        }
        else if (question.Type == QuestionType.Essay) answer = question.SampleAnswer ?? "";
        else if (question.FillMode == QuestionType.FillType)
        {
            stem = Regex.Replace(question.Stem, @"\{\{(\d+)\}\}", m =>
                int.Parse(m.Groups[1].Value) is var n && n >= 1 && n <= question.Answer.Length ? "[" + string.Join("|", question.Answer[n - 1]) + "]" : m.Value);
            label = QuestionType.TypedLabel;
            answer = "";
        }
        else
        {
            chips = options.Select(x => x.Text).ToArray();
            answer = string.Join("; ", question.Answer.Select(x => string.Join("/", x)));
        }
        return new(0, question.Code, label, QuestionLevel.Labels[question.Level], codes[question.PrimarySkillId],
            [.. question.SecondarySkillIds.Where(codes.ContainsKey).Select(id => codes[id])], question.LessonNo, stem, question.StemImage, letters, chips, answer,
            question.Points.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static IEnumerable<string> ImagesOf(QuestionImportRow row)
    {
        if (row.StemImage is { } stem) yield return stem;
        if (QuestionType.Parse(row.Type) is not (QuestionType.SingleChoice or QuestionType.MultiChoice)) yield break;
        foreach (var option in row.Options) if (IsImage(option, out var name)) yield return name;
    }

    private static readonly Regex Bracket = new(@"\[([^\[\]]*)\]", RegexOptions.Compiled);

    /// <summary>A fill-in written with answers inside the brackets, [Hà Nội|Ha Noi]: the stem with {{n}} tokens, and the answers of each blank.
    /// Null when every bracket is empty (a tap-a-button question) or there is none; Mixed when empty and filled brackets are both present.</summary>
    private static (string Stem, string[][] Answers, bool Mixed)? TypedBlanks(string stem)
    {
        var matches = Bracket.Matches(stem);
        var filled = matches.Count(m => m.Groups[1].Value.Trim().Length > 0);
        if (filled == 0) return null;
        if (filled != matches.Count) return ("", [], true);
        var answers = new List<string[]>();
        var text = Bracket.Replace(stem, m =>
        {
            answers.Add(m.Groups[1].Value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
            return "{{" + answers.Count + "}}";
        });
        return (text, [.. answers], false);
    }

    private static bool IsImage(string text, out string name)
    {
        var trimmed = text.Trim();
        foreach (var prefix in ImagePrefixes)
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { name = trimmed[prefix.Length..].Trim(); return name.Length > 0; }
        name = "";
        return false;
    }

    private static string ColumnOf(string key, string? type) => key switch
    {
        "code" => "Mã câu", "type" => "Loại", "level" => "Mức độ", "primarySkillId" => "Kỹ năng chính", "secondarySkillIds" => "Kỹ năng phụ",
        "lessonNo" => "Buổi", "stem" => "Đề bài", "stemImage" => "Ảnh đề", "answer" => "Đáp án",
        "options" => type == QuestionType.FillBlanks ? "Các nút để điền" : type == QuestionType.MultiChoice ? "Phương án A–F" : "Phương án A–D",
        "points" => "Điểm", "fillMode" or "ignoreDiacritics" => "Dạng", "sampleAnswer" => "Đáp án", _ => ""
    };

    private static int SheetOrder(string sheet) => sheet switch
    {
        ImportSheets.Info => 0, ImportSheets.Skills => 1, ImportSheets.Lessons => 2, ImportSheets.Questions => 3, _ => -1
    };

    private async Task<bool> OwnsAsync(string owner, Guid classId, CancellationToken ct) =>
        await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is not null;
}
