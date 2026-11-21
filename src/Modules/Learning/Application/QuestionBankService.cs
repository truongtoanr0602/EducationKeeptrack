using System.Security.Cryptography;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
namespace EducationKeeptrack.Learning.Application;

public sealed record QuestionBank(IReadOnlyList<Question> Questions, IReadOnlyList<QuestionImage> Images, IReadOnlyList<Skill> Skills);
public sealed record ImageView(Guid Id, string FileName, string ContentType, long ByteLength, string Url, DateTimeOffset UpdatedAt);

/// <summary>"Ngân hàng câu hỏi" of one class and its images.</summary>
public sealed class QuestionBankService(IQuestionBankRepository repository, IClassroomLearningSource classroom, ILearningPhotoStore store, BankTokens tokens, TimeProvider clock)
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxImages = 500;
    public static readonly TimeSpan ImageLinkLifetime = TimeSpan.FromHours(6);

    public async Task<LearningResult<QuestionBank>> GetAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        return new(new(await repository.ListQuestionsAsync(owner, classId, false, ct), await repository.ListImagesAsync(owner, classId, ct),
            curriculum?.Skills ?? []));
    }

    public async Task<LearningResult<Question>> CreateAsync(string owner, Guid classId, QuestionInput input, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var errors = await ValidateAsync(owner, classId, input, null, ct);
        if (errors.Count > 0) return new(null, 400, errors);
        var question = new Question(owner, classId, input, clock.GetUtcNow());
        repository.Add([question]);
        return await repository.SaveAsync(ct) ? new(question, 201) : new(null, 409);
    }

    public async Task<LearningResult<Question>> EditAsync(string owner, Guid classId, Guid id, QuestionInput input, CancellationToken ct)
    {
        var question = await repository.FindQuestionAsync(owner, classId, id, ct);
        if (question is null) return new(null, 404);
        if (question.Version != input.Version) return new(null, 409);
        var errors = await ValidateAsync(owner, classId, input, id, ct);
        if (errors.Count > 0) return new(null, 400, errors);
        if (question.Apply(input, clock.GetUtcNow()) && !await repository.SaveAsync(ct)) return new(null, 409);
        return new(question);
    }

    /// <summary>Adds an image or replaces the one with the same file name; questions refer to images by name.</summary>
    public async Task<LearningResult<QuestionImage>> UploadImageAsync(string owner, Guid classId, string fileName, Stream stream, string? contentType, long length, CancellationToken ct)
    {
        if (!await OwnsAsync(owner, classId, ct)) return new(null, 404);
        var type = contentType?.ToLowerInvariant().Trim() ?? "";
        if (type is not ("image/jpeg" or "image/jpg" or "image/png" or "image/webp" or "image/gif"))
            return new(null, 400, new() { ["file"] = ["Chỉ chấp nhận ảnh JPG, PNG, WebP hoặc GIF."] });
        if (length <= 0 || length > MaxImageBytes) return new(null, 400, new() { ["file"] = ["Mỗi ảnh tối đa 5MB."] });
        var name = Path.GetFileName((fileName ?? "").Trim().Replace('\\', '/'));
        if (name.Length is 0 or > 200) return new(null, 400, new() { ["file"] = ["Tên ảnh từ 1 đến 200 ký tự."] });
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var images = await repository.ListImagesAsync(owner, classId, ct);
        var existing = images.FirstOrDefault(x => x.NormalizedName == QuestionImage.NormalizeName(name));
        if (existing is null && images.Count >= MaxImages) return new(null, 400, new() { ["file"] = [$"Một lớp có tối đa {MaxImages} ảnh câu hỏi."] });
        if (existing?.Sha256 == hash) return new(existing);
        var key = $"questions/{classId:N}/{Guid.NewGuid():N}{Extension(type)}";
        await store.PutPhotoAsync(key, bytes, type == "image/jpg" ? "image/jpeg" : type, ct);
        var now = clock.GetUtcNow();
        if (existing is null)
        {
            existing = new QuestionImage(owner, classId, name, key, type, bytes.Length, hash, now);
            repository.Add([existing]);
        }
        else existing.Replace(key, type, bytes.Length, hash, now);
        if (await repository.SaveAsync(ct)) return new(existing, 201);
        await store.DeletePhotoAsync(key, ct);
        return new(null, 409);
    }

    public string ImageUrl(QuestionImage image) => $"/api/learning/question-images/{Uri.EscapeDataString(tokens.IssueImage(image.Id, clock.GetUtcNow().Add(ImageLinkLifetime)))}";

    public ImageView View(QuestionImage image) => new(image.Id, image.FileName, image.ContentType, image.ByteLength, ImageUrl(image), image.UpdatedAt);

    public async Task<(Stream? Stream, string ContentType, int Status)> OpenImageAsync(string token, CancellationToken ct)
    {
        if (tokens.ValidateImage(token, clock.GetUtcNow()) is not { } id) return (null, "", 401);
        var image = await repository.FindImageAsync(id, ct);
        if (image is null) return (null, "", 404);
        try { return (await store.OpenPhotoAsync(image.StorageKey, ct), image.ContentType, 200); }
        catch (PhotoStorageException) { return (null, "", 503); }
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(string owner, Guid classId, QuestionInput input, Guid? id, CancellationToken ct)
    {
        var curriculum = await repository.FindCurriculumAsync(owner, classId, false, ct);
        var live = (curriculum?.GetEffectiveSkills() ?? []).Select(x => x.Id).ToHashSet();
        var images = (await repository.ListImagesAsync(owner, classId, ct)).Select(x => x.NormalizedName).ToHashSet();
        var errors = Question.Validate(input, live, images);
        if (!errors.ContainsKey("code") && (await repository.ListQuestionsAsync(owner, classId, false, ct))
                .Any(x => x.Id != id && string.Equals(x.Code, input.Code!.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors["code"] = ["Mã câu đã có trong ngân hàng của lớp."];
        return errors;
    }

    private static string Extension(string type) => type switch { "image/png" => ".png", "image/webp" => ".webp", "image/gif" => ".gif", _ => ".jpg" };

    private async Task<bool> OwnsAsync(string owner, Guid classId, CancellationToken ct) =>
        await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is not null;
}
