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
    public const string SingleChoice = "single_choice", FillBlanks = "fill_blanks";
    public static readonly string[] All = [SingleChoice, FillBlanks];
    public static readonly Dictionary<string, string> Labels = new() { [SingleChoice] = "Trắc nghiệm", [FillBlanks] = "Điền ô" };

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
    string? Stem, string? StemImage, QuestionOption[]? Options, string[][]? Answer, bool Archived, int Version);

/// <summary>Everything an exam keeps of a question at the time it was drawn, answer included.</summary>
public sealed record QuestionSnapshot(Guid QuestionId, string Code, string Type, string Level, string Stem, string? StemImage,
    QuestionOption[] Options, string[][] Answer, Guid PrimarySkillId)
{
    public int Blanks => Type == QuestionType.FillBlanks ? Question.CountBlanks(Stem) : 1;

    /// <summary>Single choice: the chosen key matches. Fill blanks: every blank holds one of its accepted values.</summary>
    public bool IsCorrect(string[]? response)
    {
        if (response is null || response.Length != Answer.Length) return false;
        for (var i = 0; i < Answer.Length; i++)
        {
            var given = Clean(response[i]);
            if (given.Length == 0 || !Answer[i].Any(x => string.Equals(Clean(x), given, StringComparison.OrdinalIgnoreCase))) return false;
        }
        return true;
    }

    private static string Clean(string? value) => Regex.Replace(value ?? "", @"\s+", " ").Trim();
}

/// <summary>
/// "Câu hỏi" of the class's question bank. Exactly one primary skill counts in results; up to two secondary
/// skills only help to find questions. Questions are never deleted, only archived; exams keep their own snapshot.
/// </summary>
public sealed class Question
{
    public const int MaxSecondary = 2, MaxBlanks = 6, MaxChips = 12;
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

    /// <summary>Checks the request against the class's live skills and question images; keys are request field names.</summary>
    public static Dictionary<string, string[]> Validate(QuestionInput input, IReadOnlySet<Guid> liveSkills, IReadOnlySet<string> images)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string key, string message) => errors[key] = errors.TryGetValue(key, out var list) ? [.. list, message] : [message];
        if (string.IsNullOrWhiteSpace(input.Code) || input.Code.Trim().Length > 40) Add("code", "Nhập mã câu (tối đa 40 ký tự).");
        if (input.Type is null || !QuestionType.All.Contains(input.Type)) Add("type", "Loại câu là Trắc nghiệm hoặc Điền ô.");
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
        var before = (Code, Type, Level, PrimarySkillId, string.Join(",", SecondarySkillIds), LessonNo, Stem, StemImage, OptionsJson, AnswerJson, Status);
        Assign(input);
        var after = (Code, Type, Level, PrimarySkillId, string.Join(",", SecondarySkillIds), LessonNo, Stem, StemImage, OptionsJson, AnswerJson, Status);
        if (before == after) return false;
        Version++; UpdatedAt = now;
        return true;
    }

    public QuestionSnapshot Snapshot() => new(Id, Code, Type, Level, Stem, StemImage, Options, Answer, PrimarySkillId);

    private void Assign(QuestionInput input)
    {
        Code = input.Code!.Trim(); Type = input.Type!; Level = input.Level!; PrimarySkillId = input.PrimarySkillId;
        SecondarySkillIds = (input.SecondarySkillIds ?? []).Distinct().Where(x => x != input.PrimarySkillId).ToList();
        LessonNo = input.LessonNo; Stem = input.Stem!.Trim(); StemImage = Clean(input.StemImage);
        OptionsJson = JsonSerializer.Serialize((input.Options ?? []).Select(x => new QuestionOption(x.Key.Trim().ToUpperInvariant(), x.Text?.Trim() ?? "", Clean(x.Image))).ToArray(), Json);
        AnswerJson = JsonSerializer.Serialize((input.Answer ?? []).Select(x => x.Select(v => v.Trim()).Where(v => v.Length > 0).Select(v => Type == QuestionType.SingleChoice ? v.ToUpperInvariant() : v).ToArray()).ToArray(), Json);
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
