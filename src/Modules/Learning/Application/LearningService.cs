using System.Security.Cryptography;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.Extensions.Options;

namespace EducationKeeptrack.Learning.Application;

public interface ILearningRepository
{
    Task<IReadOnlyList<Assessment>> ListAsync(string owner, Guid classId, CancellationToken ct);
    /// <summary>`track` must be true when the caller changes the assessment and saves; untracked reads are for views.</summary>
    Task<Assessment?> FindAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct);
    Task<Submission?> FindSubmissionAsync(string owner, Guid classId, Guid id, Guid studentId, CancellationToken ct);
    Task<SubmissionPhoto?> FindPhotoAsync(Guid photoId, CancellationToken ct);
    /// <summary>Every assessment of the class with only this student's submission and its history loaded.</summary>
    Task<IReadOnlyList<Assessment>> ListForStudentAsync(string owner, Guid classId, Guid studentId, CancellationToken ct);
    void Add(Assessment assessment);
    Task<bool> SaveAsync(CancellationToken ct);
}

public record LearningResult<T>(T? Value, int Status = 200, Dictionary<string, string[]>? Errors = null) where T : class;
public record AssessmentInput(string? Title, DateOnly Date, string DefaultMethod, string? Rubric = "");
public record MethodInput(string Method, int Version);
public record RubricInput(string? Rubric);
public record PhotoView(Guid Id, Guid SubmissionId, int SequenceIndex, string ContentType, long ByteLength, string DownloadUrl, DateTimeOffset CreatedAt);
public record ReorderPhotosInput(List<Guid> PhotoIds);

public sealed class LearningService(
    ILearningRepository repository,
    IClassroomLearningSource classroom,
    ILearningPhotoStore photoStore,
    PhotoTokens photoTokens,
    IAiQuota quota,
    IAiGradingEngine aiEngine,
    IOptions<LearningStorageOptions> storageOptions,
    TimeProvider clock)
{
    public async Task<LearningResult<Assessment>> CreateAsync(string owner, Guid classId, AssessmentInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 160 || input.Date == default || (input.DefaultMethod != "manual" && input.DefaultMethod != "ai"))
            return new(null, 400, new() { ["assessment"] = ["Nhập tên (tối đa 160 ký tự), ngày và cách chấm hợp lệ."] });
        var roster = await classroom.ReadRosterAsync(owner, classId, input.Date, ct);
        if (roster is null) return new(null, 404);
        var assessment = new Assessment(owner, classId, input.Title, input.Date, roster.Students.Select(x => (x.Id, x.Name)), input.DefaultMethod, input.Rubric ?? "");
        repository.Add(assessment);
        await repository.SaveAsync(ct);
        return new(assessment, 201);
    }

    public async Task<LearningResult<IReadOnlyList<Assessment>>> ListAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct) is null) return new(null, 404);
        return new(await repository.ListAsync(owner, classId, ct));
    }

    /// <summary>Two-indicator progress series for one student, built on read from confirmed results only.</summary>
    public async Task<LearningResult<StudentProgress>> ProgressAsync(string owner, Guid classId, Guid studentId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime); // Asia/Ho_Chi_Minh has no DST
        var end = to ?? today;
        var start = from ?? end.AddDays(-180);
        if (start > end || end.DayNumber - start.DayNumber > 366)
            return new(null, 400, new() { ["range"] = ["Chọn khoảng ngày hợp lệ, tối đa 366 ngày."] });
        var roster = await classroom.ReadRosterAsync(owner, classId, today, ct);
        if (roster is null) return new(null, 404);
        var assessments = await repository.ListForStudentAsync(owner, classId, studentId, ct);
        var name = assessments.OrderByDescending(x => x.Date).SelectMany(x => x.Submissions).Where(x => x.StudentId == studentId).Select(x => x.StudentName).FirstOrDefault()
            ?? roster.Students.FirstOrDefault(x => x.Id == studentId)?.Name;
        if (name is null) return new(null, 404);
        return new(ProgressSeries.Build(studentId, name, start, end, assessments));
    }

    public async Task<LearningResult<Assessment>> FindAsync(string owner, Guid classId, Guid id, CancellationToken ct)
    {
        var assessment = await repository.FindAsync(owner, classId, id, false, ct);
        return new(assessment, assessment is null ? 404 : 200);
    }

    public async Task<LearningResult<Assessment>> UpdateRubricAsync(string owner, Guid classId, Guid id, RubricInput input, CancellationToken ct)
    {
        var assessment = await repository.FindAsync(owner, classId, id, true, ct);
        if (assessment is null) return new(null, 404);
        var rubric = input.Rubric?.Trim() ?? "";
        if (rubric.Length > 5000)
            return new(null, 400, new() { ["rubric"] = ["Hướng dẫn chấm tối đa 5000 ký tự."] });
        assessment.UpdateRubric(rubric);
        return await repository.SaveAsync(ct) ? new(assessment) : new(null, 409);
    }

    public async Task<LearningResult<Submission>> RecordAsync(string owner, Guid classId, Guid id, Guid studentId, ResultInput input, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        if (input.Version != submission.Version) return new(null, 409);
        var errors = submission.Validate(input);
        if (errors.Count > 0) return new(null, 400, errors);
        submission.Record(input, clock.GetUtcNow());
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }

    public async Task<LearningResult<Submission>> ChangeMethodAsync(string owner, Guid classId, Guid id, Guid studentId, MethodInput input, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        if (input.Version != submission.Version || submission.History.Any(x => x.Action != "method_changed")) return new(null, 409);
        if (input.Method is not ("manual" or "ai")) return new(null, 400, new() { ["method"] = ["Cách chấm phải là 'manual' hoặc 'ai'."] });
        submission.RecordMethod(input.Method, clock.GetUtcNow());
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }

    public async Task<LearningResult<PhotoView>> UploadPhotoAsync(string owner, Guid classId, Guid id, Guid studentId, Stream stream, string contentType, long byteLength, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        var normContentType = contentType?.ToLowerInvariant().Trim() ?? "";
        if (normContentType is not ("image/jpeg" or "image/jpg" or "image/png" or "image/webp"))
            return new(null, 400, new() { ["file"] = ["Chỉ chấp nhận ảnh định dạng JPG, PNG hoặc WebP."] });
        if (byteLength <= 0 || byteLength > 5 * 1024 * 1024)
            return new(null, 400, new() { ["file"] = ["Kích thước ảnh không được vượt quá 5MB."] });
        if (submission.Photos.Count >= 10)
            return new(null, 400, new() { ["file"] = ["Mỗi bài nộp tối đa 10 trang ảnh."] });

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var ext = normContentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        var key = $"{submission.Id}/{Guid.NewGuid():N}{ext}";
        await photoStore.PutPhotoAsync(key, bytes, normContentType, ct);

        var photo = new SubmissionPhoto(submission.Id, key, submission.Photos.Count + 1, normContentType, bytes.Length, sha256, clock.GetUtcNow());
        submission.AddPhoto(photo);
        if (!await repository.SaveAsync(ct))
        {
            await photoStore.DeletePhotoAsync(key, ct);
            return new(null, 409);
        }

        var expires = clock.GetUtcNow().AddMinutes(storageOptions.Value.TokenMinutes);
        var token = photoTokens.Issue(owner, photo.Id, expires);
        var downloadUrl = $"/api/learning/photos/{Uri.EscapeDataString(token)}";
        return new(new PhotoView(photo.Id, photo.SubmissionId, photo.SequenceIndex, photo.ContentType, photo.ByteLength, downloadUrl, photo.CreatedAt), 201);
    }

    public async Task<LearningResult<IReadOnlyList<PhotoView>>> ListPhotosAsync(string owner, Guid classId, Guid id, Guid studentId, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        var expires = clock.GetUtcNow().AddMinutes(storageOptions.Value.TokenMinutes);
        var views = submission.Photos.OrderBy(x => x.SequenceIndex).Select(x =>
        {
            var token = photoTokens.Issue(owner, x.Id, expires);
            return new PhotoView(x.Id, x.SubmissionId, x.SequenceIndex, x.ContentType, x.ByteLength, $"/api/learning/photos/{Uri.EscapeDataString(token)}", x.CreatedAt);
        }).ToList();
        return new(views);
    }

    public async Task<LearningResult<Submission>> DeletePhotoAsync(string owner, Guid classId, Guid id, Guid studentId, Guid photoId, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        var photo = submission.Photos.FirstOrDefault(x => x.Id == photoId);
        if (photo is null) return new(null, 404);
        await photoStore.DeletePhotoAsync(photo.PhotoKey, ct);
        submission.RemovePhoto(photoId);
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }

    public async Task<LearningResult<Submission>> ReorderPhotosAsync(string owner, Guid classId, Guid id, Guid studentId, ReorderPhotosInput input, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        if (input.PhotoIds == null || input.PhotoIds.Count != submission.Photos.Count ||
            input.PhotoIds.Any(x => submission.Photos.All(p => p.Id != x)))
            return new(null, 400, new() { ["reorder"] = ["Danh sách ảnh không khớp với bài nộp hiện tại."] });
        submission.ReorderPhotos(input.PhotoIds);
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }

    public async Task<(Stream? Stream, string ContentType, int Status)> GetPhotoFileAsync(string token, CancellationToken ct)
    {
        var (valid, _, photoId) = photoTokens.Validate(token, clock.GetUtcNow());
        if (!valid) return (null, "", 401);
        var photo = await repository.FindPhotoAsync(photoId, ct);
        if (photo is null) return (null, "", 404);
        try
        {
            var stream = await photoStore.OpenPhotoAsync(photo.PhotoKey, ct);
            return (stream, photo.ContentType, 200);
        }
        catch (PhotoStorageException)
        {
            return (null, "", 503);
        }
    }

    public async Task<LearningResult<Submission>> GradeWithAiAsync(string owner, Guid classId, Guid id, Guid studentId, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        var assessment = await repository.FindAsync(owner, classId, id, false, ct);
        if (assessment is null) return new(null, 404);

        if (submission.Photos.Count == 0)
            return new(null, 400, new() { ["photos"] = ["Cần tải ảnh bài làm của học sinh trước khi yêu cầu AI chấm bài."] });
        if (string.IsNullOrWhiteSpace(assessment.Rubric))
            return new(null, 400, new() { ["rubric"] = ["Cần cập nhật hướng dẫn chấm cho bài kiểm tra trước khi yêu cầu AI chấm bài."] });

        var requestKey = $"grading:{submission.Id}:{submission.Photos.Count}:{submission.Version}";
        var reservation = await quota.ReserveAsync(owner, AiTaskKind.Grading, requestKey, units: 1, ct);
        if (reservation.Outcome == AiReservationOutcome.Denied)
        {
            var msg = reservation.DeniedReason switch
            {
                "ai_disabled" => "Tài khoản chưa được kích hoạt quyền AI. Vui lòng liên hệ Admin.",
                "no_quota" or "exhausted" => "Hạn mức lượt AI của kỳ này đã hết.",
                "expired" => "Thời hạn dịch vụ của tài khoản đã hết.",
                _ => "Không thể sử dụng AI lúc này (" + reservation.DeniedReason + ")."
            };
            return new(null, 403, new() { ["ai"] = [msg] });
        }

        try
        {
            var photoBytesList = new List<byte[]>();
            foreach (var p in submission.Photos.OrderBy(x => x.SequenceIndex))
            {
                using var stream = await photoStore.OpenPhotoAsync(p.PhotoKey, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                photoBytesList.Add(ms.ToArray());
            }

            var proposal = await aiEngine.GradeAsync(assessment.Rubric, photoBytesList, ct);
            if (reservation.ReservationId.HasValue)
            {
                var committed = await quota.CommitAsync(reservation.ReservationId.Value, ct);
                if (!committed)
                {
                    return new(null, 403, new() { ["ai"] = ["Không thể trừ lượt AI do tài khoản bị hạn chế."] });
                }
            }

            submission.SetAiProposal(proposal.Completion, proposal.Correctness, proposal.Confidence, proposal.Reasoning, proposal.Warning, clock.GetUtcNow());
            return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
        }
        catch (Exception)
        {
            if (reservation.ReservationId.HasValue)
                await quota.ReleaseAsync(reservation.ReservationId.Value, ct);
            return new(null, 502, new() { ["ai"] = ["Không thể kết nối dịch vụ AI. Lượt AI đã được hoàn trả, vui lòng thử lại hoặc chấm thủ công."] });
        }
    }

    public async Task<LearningResult<Submission>> AiFallbackAsync(string owner, Guid classId, Guid id, Guid studentId, CancellationToken ct)
    {
        var submission = await repository.FindSubmissionAsync(owner, classId, id, studentId, ct);
        if (submission is null) return new(null, 404);
        submission.ClearAiProposal();
        submission.RecordMethod("manual", clock.GetUtcNow());
        return await repository.SaveAsync(ct) ? new(submission) : new(null, 409);
    }
}
