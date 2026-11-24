using System.Text.Json;
using System.Text.RegularExpressions;
namespace EducationKeeptrack.Learning.Domain;

/// <summary>The four fixed question levels ("Mức độ câu hỏi"), kept apart from the three material difficulties.</summary>
public static class QuestionLevel
{
    public const string Recall = "nhan_biet", Understand = "thong_hieu", Apply = "van_dung", Advanced = "van_dung_cao";
    public static readonly string[] All = [Recall, Understand, Apply, Advanced];
    public static readonly Dictionary<string, string> Labels = new()
    {
        [Recall] = "Nhận biết", [Understand] = "Thông hiểu", [Apply] = "Vận dụng", [Advanced] = "Vận dụng cao"
    };

    /// <summary>Accepts the code, the label or the teacher's everyday words (cơ bản, trung bình); null when unknown.</summary>
    public static string? Parse(string? value)
    {
        var text = Normalize(value);
        if (text.Length == 0) return null;
        foreach (var (code, label) in Labels) if (text == Normalize(code) || text == Normalize(label)) return code;
        return text switch
        {
            "co ban" or "muc 1" or "1" => Recall,
            "trung binh" or "muc 2" or "2" => Understand,
            "muc 3" or "3" => Apply,
            "muc 4" or "4" => Advanced,
            _ => null
        };
    }

    internal static string Normalize(string? value)
    {
        var text = (value ?? "").Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = text.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(System.Text.NormalizationForm.FormC).Replace('đ', 'd').Replace('_', ' ');
    }
}

public static class QuestionType
{
    public const string SingleChoice = "single_choice", MultiChoice = "multi_choice", FillBlanks = "fill_blanks", Essay = "essay";
    public static readonly string[] All = [SingleChoice, MultiChoice, FillBlanks, Essay];
    public static readonly Dictionary<string, string> Labels = new()
    {
        [SingleChoice] = "Trắc nghiệm", [MultiChoice] = "Nhiều đáp án", [FillBlanks] = "Điền ô", [Essay] = "Tự luận"
    };
    /// <summary>The two ways to fill a blank: tap a button (the original way, blanks written [ ]) or type into the text (blanks {{1}}, {{2}}…).</summary>
    public const string FillChips = "chips", FillType = "type";
    /// <summary>Every kind but the essay is scored by the computer; an essay is scored by the teacher.</summary>
    public static bool AutoGraded(string type) => type != Essay;

    public static string? Parse(string? value)
    {
        var text = QuestionLevel.Normalize(value);
        if (text.Length == 0) return null;
        if (text is "single choice" or "trac nghiem") return SingleChoice;
        if (text is "fill blanks" or "dien o" or "dien" or "dien vao cho trong") return FillBlanks;
        return null;
    }
}

public static class QuestionStatus
{
    public const string Active = "active", Archived = "archived";
}

/// <summary>
/// One answer option (single choice: keys A–D) or one tap button of a fill-in question (keys 1..n).
/// Image is a file name of the class's question images.
/// </summary>
public sealed record QuestionOption(string Key, string Text, string? Image);

public record QuestionInput(string? Code, string? Type, string? Level, Guid PrimarySkillId, Guid[]? SecondarySkillIds, int? LessonNo,
    string? Stem, string? StemImage, QuestionOption[]? Options, string[][]? Answer, bool Archived, int Version,
    decimal? Points = null, string? FillMode = null, bool IgnoreDiacritics = false, string? SampleAnswer = null);

/// <summary>Everything an exam keeps of a question at the time it was drawn, answer included. Members after PrimarySkillId have
/// defaults so exams drawn before they existed still read: one point, tap a button, accents matter.</summary>
public sealed record QuestionSnapshot(Guid QuestionId, string Code, string Type, string Level, string Stem, string? StemImage,
    QuestionOption[] Options, string[][] Answer, Guid PrimarySkillId,
    decimal Points = 1m, string FillMode = QuestionType.FillChips, bool IgnoreDiacritics = false, string? StemHtml = null, string? AudioFile = null, string? SampleAnswer = null)
{
    /// <summary>How many values a response holds: one per blank of a fill-in, one for every other kind (a multi-choice response is a list of keys).</summary>
    public int Blanks => Type == QuestionType.FillBlanks ? (FillMode == QuestionType.FillType ? Question.CountTokens(Stem) : Question.CountBlanks(Stem)) : 1;

    /// <summary>Points earned for a response, 0 to Points. An essay scores 0 here: only the teacher scores it.</summary>
    public decimal Score(string[]? response)
    {
        if (response is null || Points <= 0) return 0;
        switch (Type)
        {
            case QuestionType.SingleChoice:
                return response.Length == 1 && Answer.Length == 1 && Answer[0].Length == 1 && Same(response[0], Answer[0][0]) ? Points : 0;
            case QuestionType.MultiChoice:
            {
                if (Answer.Length != 1) return 0;
                var given = response.Select(Clean).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var right = Answer[0].Select(Clean).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return given.Length == right.Length && right.All(x => given.Contains(x, StringComparer.OrdinalIgnoreCase)) ? Points : 0;
            }
            case QuestionType.FillBlanks:
            {
                if (response.Length != Answer.Length || Answer.Length == 0) return 0;
                var right = 0;
                for (var i = 0; i < Answer.Length; i++)
                    if (Clean(response[i]).Length > 0 && Answer[i].Any(x => Matches(response[i], x))) right++;
                return Math.Round(Points * right / Answer.Length, 2, MidpointRounding.AwayFromZero);
            }
            default:
                return 0;
        }
    }

    /// <summary>Every point earned: the question is fully right. Never true for an essay.</summary>
    public bool IsCorrect(string[]? response) => QuestionType.AutoGraded(Type) && Points > 0 && Score(response) == Points;

    private bool Matches(string given, string accepted) => FillMode == QuestionType.FillType ? Typed(given, accepted, IgnoreDiacritics) : Same(given, accepted);

    private static bool Same(string? a, string? b) => string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Typed answer: case and spare spaces never matter, accents matter unless the question says otherwise, and 0,5 equals 0.5.</summary>
    internal static bool Typed(string? given, string? accepted, bool ignoreDiacritics)
    {
        var a = Fold(given, ignoreDiacritics);
        var b = Fold(accepted, ignoreDiacritics);
        if (a.Length == 0 || b.Length == 0) return false;
        if (a == b) return true;
        return TryNumber(a, out var x) && TryNumber(b, out var y) && x == y;
    }

    private static string Fold(string? value, bool ignoreDiacritics)
    {
        var text = Clean(value).Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        if (!ignoreDiacritics) return text;
        var chars = text.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        return new string(chars).Normalize(System.Text.NormalizationForm.FormC).Replace('đ', 'd');
    }

    private static bool TryNumber(string text, out decimal value) =>
        decimal.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture, out value);

    private static string Clean(string? value) => Regex.Replace(value ?? "", @"\s+", " ").Trim();
}

/// <summary>
/// "Câu hỏi" of the class's question bank. Exactly one primary skill counts in results; up to two secondary
/// skills only help to find questions. Questions are never deleted, only archived; exams keep their own snapshot.
/// </summary>
public sealed class Question
{
    public const int MaxSecondary = 2, MaxBlanks = 6, MaxChips = 12, MaxTypedBlanks = 10, MaxChoices = 6, EssayLimit = 2000;
    public const decimal MinPoints = 0.25m, MaxPoints = 10m, PointStep = 0.25m;
    private static readonly Regex TokenPattern = new(@"\{\{(\d+)\}\}", RegexOptions.Compiled);
    private static readonly Regex BlankPattern = new(@"\[\s*\]", RegexOptions.Compiled);
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string Code { get; private set; } = "";
    public string Type { get; private set; } = QuestionType.SingleChoice;
    public string Level { get; private set; } = QuestionLevel.Recall;
    public Guid PrimarySkillId { get; private set; }
    public List<Guid> SecondarySkillIds { get; private set; } = [];
    public int? LessonNo { get; private set; }
    public string Stem { get; private set; } = "";
    public string? StemImage { get; private set; }
    /// <summary>Points a full answer earns (0.25 to 10, in steps of 0.25).</summary>
    public decimal Points { get; private set; } = 1m;
    public string FillMode { get; private set; } = QuestionType.FillChips;
    public bool IgnoreDiacritics { get; private set; }
    /// <summary>The teacher's model answer of an essay, shown while grading; a student only sees it once the results are released.</summary>
    public string? SampleAnswer { get; private set; }
    /// <summary>Sanitized rich stem and an uploaded audio file of a typed fill-in; set by the editor, not by this input yet.</summary>
    public string? StemHtml { get; private set; }
    public string? AudioFile { get; private set; }
    public string OptionsJson { get; private set; } = "[]";
    public string AnswerJson { get; private set; } = "[]";
    public string Status { get; private set; } = QuestionStatus.Active;
    public int Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public QuestionOption[] Options => JsonSerializer.Deserialize<QuestionOption[]>(OptionsJson, Json) ?? [];
    public string[][] Answer => JsonSerializer.Deserialize<string[][]>(AnswerJson, Json) ?? [];
    private Question() { }
    public Question(string owner, Guid classId, QuestionInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; CreatedAt = UpdatedAt = now;
        Assign(input);
    }

    public static int CountBlanks(string? stem) => BlankPattern.Matches(stem ?? "").Count;

    /// <summary>Blanks of a typed fill-in: the distinct {{n}} tokens.</summary>
    public static int CountTokens(string? stem) => TokenPattern.Matches(stem ?? "").Select(m => m.Groups[1].Value).Distinct().Count();

    /// <summary>True when the tokens are exactly {{1}}..{{n}}, each once.</summary>
    public static bool TokensInOrder(string? stem)
    {
        var found = TokenPattern.Matches(stem ?? "").Select(m => int.Parse(m.Groups[1].Value)).ToArray();
        return found.Length > 0 && found.SequenceEqual(Enumerable.Range(1, found.Length));
    }

    /// <summary>Checks the request against the class's live skills and question images; keys are request field names.</summary>
    public static Dictionary<string, string[]> Validate(QuestionInput input, IReadOnlySet<Guid> liveSkills, IReadOnlySet<string> images)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors[key] = errors.TryGetValue(key, out var list) ? [.. list, message] : [message];
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 40) Add("code", "Nhập mã câu (tối đa 40 ký tự).");
        if (input.Type is null || !QuestionType.All.Contains(input.Type)) Add("type", "Dạng câu là Trắc nghiệm, Nhiều đáp án, Điền ô hoặc Tự luận.");
        if (input.Points is { } points && (points < MinPoints || points > MaxPoints || points % PointStep != 0)) Add("points", "Điểm từ 0,25 đến 10, bước 0,25.");
        if (input.FillMode is not null && (input.Type != QuestionType.FillBlanks || input.FillMode is not (QuestionType.FillChips or QuestionType.FillType)))
            Add("fillMode", "Cách điền chỉ có ở câu điền ô: chọn nút hoặc gõ chữ.");
        if (input.IgnoreDiacritics && !(input.Type == QuestionType.FillBlanks && input.FillMode == QuestionType.FillType)) Add("ignoreDiacritics", "Chỉ câu điền ô gõ chữ mới bỏ qua dấu được.");
        if (input.SampleAnswer?.Length > EssayLimit) Add("sampleAnswer", $"Đáp án mẫu tối đa {EssayLimit} ký tự.");
        if (input.Level is null || !QuestionLevel.All.Contains(input.Level)) Add("level", "Mức độ là Nhận biết, Thông hiểu, Vận dụng hoặc Vận dụng cao.");
        if (!liveSkills.Contains(input.PrimarySkillId)) Add("primarySkillId", "Chọn kỹ năng chính đang dùng của lớp.");
        var secondary = (input.SecondarySkillIds ?? []).Distinct().Where(x => x != input.PrimarySkillId).ToList();
        if (secondary.Count > MaxSecondary) Add("secondarySkillIds", $"Tối đa {MaxSecondary} kỹ năng phụ.");
        if (secondary.Any(x => !liveSkills.Contains(x))) Add("secondarySkillIds", "Kỹ năng phụ phải là kỹ năng đang dùng của lớp.");
        if (input.LessonNo is < 1 or > 500) Add("lessonNo", "Số buổi từ 1 đến 500.");
        if (string.IsNullOrWhiteSpace(input.Stem) || input.Stem.Trim().Length > 2000) Add("stem", "Nhập đề bài (tối đa 2000 ký tự).");
        CheckImage(input.StemImage, "stemImage");
        var options = input.Options ?? [];
        var answer = input.Answer ?? [];
        foreach (var option in options)
        {
            if (option is null || string.IsNullOrWhiteSpace(option.Key)) { Add("options", "Phương án thiếu ký hiệu."); continue; }
            if (string.IsNullOrWhiteSpace(option.Text) && string.IsNullOrWhiteSpace(option.Image)) Add("options", $"Phương án {option.Key} cần chữ hoặc ảnh.");
            if (option.Text?.Trim().Length > 300) Add("options", $"Phương án {option.Key} tối đa 300 ký tự.");
            CheckImage(option.Image, "options");
        }
        if (options.Select(x => x?.Key?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length) Add("options", "Ký hiệu phương án không được trùng.");
        if (input.Type == QuestionType.SingleChoice)
        {
            if (options.Length is < 2 or > 4) Add("options", "Câu trắc nghiệm có từ 2 đến 4 phương án.");
            if (answer.Length != 1 || answer[0] is not { Length: 1 } || options.All(x => !string.Equals(x?.Key, answer[0][0]?.Trim(), StringComparison.OrdinalIgnoreCase)))
                Add("answer", "Đáp án là ký hiệu của một phương án (A, B, C hoặc D).");
        }
        else if (input.Type == QuestionType.MultiChoice)
        {
            if (options.Length is < 3 or > MaxChoices) Add("options", $"Câu nhiều đáp án có từ 3 đến {MaxChoices} phương án.");
            var keys = (answer.Length == 1 ? answer[0] : []).Select(x => x?.Trim() ?? "").ToArray();
            if (answer.Length != 1 || keys.Length < 2 || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length
                || keys.Any(k => options.All(x => !string.Equals(x?.Key?.Trim(), k, StringComparison.OrdinalIgnoreCase))))
                Add("answer", "Đáp án là từ hai ký hiệu phương án trở lên, không trùng nhau (ví dụ A, C).");
        }
        else if (input.Type == QuestionType.Essay)
        {
            if (options.Length > 0) Add("options", "Câu tự luận không có phương án.");
        }
        else if (input.Type == QuestionType.FillBlanks && input.FillMode == QuestionType.FillType)
        {
            var blanks = CountTokens(input.Stem);
            if (blanks is < 1 or > MaxTypedBlanks || !TokensInOrder(input.Stem)) Add("stem", $"Câu điền ô gõ chữ cần từ 1 đến {MaxTypedBlanks} ô trống viết {{{{1}}}}, {{{{2}}}}… theo thứ tự, mỗi số một lần.");
            if (options.Length > 0) Add("options", "Câu điền ô gõ chữ không có nút để bấm.");
            if (answer.Length != blanks) Add("answer", $"Đề bài có {blanks} ô trống nhưng đáp án có {answer.Length} ô.");
            else if (answer.Any(x => x is null || x.Length == 0 || x.Any(v => string.IsNullOrWhiteSpace(v) || v.Trim().Length > 100))) Add("answer", "Mỗi ô cần ít nhất một đáp án, tối đa 100 ký tự.");
        }
        else if (input.Type == QuestionType.FillBlanks)
        {
            var blanks = CountBlanks(input.Stem);
            if (blanks is < 1 or > MaxBlanks) Add("stem", $"Câu điền ô cần từ 1 đến {MaxBlanks} ô trống, viết là [ ] trong đề bài.");
            if (options.Length is < 2 or > MaxChips) Add("options", $"Câu điền ô có từ 2 đến {MaxChips} nút để bấm.");
            if (options.Any(x => !string.IsNullOrWhiteSpace(x?.Image))) Add("options", "Nút của câu điền ô chỉ là chữ hoặc số.");
            if (answer.Length != blanks) Add("answer", $"Đề bài có {blanks} ô trống nhưng đáp án có {answer.Length} ô.");
            var chips = options.Select(x => x?.Text?.Trim() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (answer.Any(x => x is null || x.Length == 0 || x.Any(string.IsNullOrWhiteSpace))) Add("answer", "Mỗi ô cần ít nhất một đáp án.");
            else if (answer.SelectMany(x => x).Any(x => !chips.Contains(x.Trim()))) Add("answer", "Mỗi đáp án của ô phải là một trong các nút để bấm.");
        }
        return errors;

        void CheckImage(string? name, string key)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (name.Trim().Length > 200) Add(key, "Tên ảnh tối đa 200 ký tự.");
            else if (!images.Contains(QuestionImage.NormalizeName(name))) Add(key, $"Chưa có ảnh \"{name.Trim()}\". Tải ảnh lên trước.");
        }
    }

    /// <summary>Applies the request; returns true when something changed. The caller validates first.</summary>
    public bool Apply(QuestionInput input, DateTimeOffset now)
    {
        var before = (Code, Type, Level, PrimarySkillId, string.Join(",", SecondarySkillIds), LessonNo, Stem, StemImage, OptionsJson, AnswerJson, Status, Points, FillMode, IgnoreDiacritics, SampleAnswer);
        Assign(input);
        var after = (Code, Type, Level, PrimarySkillId, string.Join(",", SecondarySkillIds), LessonNo, Stem, StemImage, OptionsJson, AnswerJson, Status, Points, FillMode, IgnoreDiacritics, SampleAnswer);
        if (before == after) return false;
        Version++; UpdatedAt = now;
        return true;
    }

    public QuestionSnapshot Snapshot() => new(Id, Code, Type, Level, Stem, StemImage, Options, Answer, PrimarySkillId, Points, FillMode, IgnoreDiacritics, StemHtml, AudioFile, SampleAnswer);

    private void Assign(QuestionInput input)
    {
        Code = input.Code!.Trim(); Type = input.Type!; Level = input.Level!; PrimarySkillId = input.PrimarySkillId;
        SecondarySkillIds = (input.SecondarySkillIds ?? []).Distinct().Where(x => x != input.PrimarySkillId).ToList();
        LessonNo = input.LessonNo; Stem = input.Stem!.Trim(); StemImage = Clean(input.StemImage);
        OptionsJson = JsonSerializer.Serialize((input.Options ?? []).Select(x => new QuestionOption(x.Key.Trim().ToUpperInvariant(), x.Text?.Trim() ?? "", Clean(x.Image))).ToArray(), Json);
        AnswerJson = JsonSerializer.Serialize((input.Answer ?? []).Select(x => x.Select(v => v.Trim()).Where(v => v.Length > 0).Select(v => Type is QuestionType.SingleChoice or QuestionType.MultiChoice ? v.ToUpperInvariant() : v).ToArray()).ToArray(), Json);
        Points = input.Points ?? 1m;
        FillMode = Type == QuestionType.FillBlanks ? input.FillMode ?? QuestionType.FillChips : QuestionType.FillChips;
        IgnoreDiacritics = Type == QuestionType.FillBlanks && FillMode == QuestionType.FillType && input.IgnoreDiacritics;
        SampleAnswer = Type == QuestionType.Essay && !string.IsNullOrWhiteSpace(input.SampleAnswer) ? input.SampleAnswer.Trim() : null;
        Status = input.Archived ? QuestionStatus.Archived : QuestionStatus.Active;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>An image of the class's question bank, addressed by its file name so a workbook can refer to it.</summary>
public sealed class QuestionImage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string FileName { get; private set; } = "";
    public string NormalizedName { get; private set; } = "";
    public string StorageKey { get; private set; } = "";
    public string ContentType { get; private set; } = "";
    public long ByteLength { get; private set; }
    public string Sha256 { get; private set; } = "";
    public DateTimeOffset UpdatedAt { get; private set; }
    private QuestionImage() { }
    public QuestionImage(string owner, Guid classId, string fileName, string key, string contentType, long length, string sha256, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; FileName = fileName.Trim(); NormalizedName = NormalizeName(fileName);
        Replace(key, contentType, length, sha256, now);
    }

    public void Replace(string key, string contentType, long length, string sha256, DateTimeOffset now)
    {
        StorageKey = key; ContentType = contentType; ByteLength = length; Sha256 = sha256; UpdatedAt = now;
    }

    public static string NormalizeName(string name) => Path.GetFileName(name.Trim().Replace('\\', '/')).ToLowerInvariant();
}
