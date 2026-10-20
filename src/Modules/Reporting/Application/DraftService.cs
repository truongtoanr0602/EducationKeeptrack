using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Application;

public record ReportResult<T>(T? Value, int Status = 200, Dictionary<string, string[]>? Errors = null, string? Message = null);
public record DraftView(Guid ClassId, Guid StudentId, string Month, string? DisplayName, string? ParentDisplayName, string? ContactDisplay,
    string Comment, string Roadmap, string Emoji, long Version, DateTimeOffset? SavedAt);
public interface IReportingRepository
{
    Task<MonthlyReportDraft?> DraftAsync(ReportScope scope, CancellationToken ct);
    Task<T> WithScopeLockAsync<T>(ReportScope scope, Func<Task<T>> action, CancellationToken ct);
    void Add(MonthlyReportDraft draft);
    void Add(ReportPreview preview);
    Task<ReportPreview?> PreviewAsync(ReportScope scope, Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}

public sealed class DraftService(IReportingRepository repository, IClassroomReportSource classroom, TimeProvider clock)
{
    public async Task<ReportResult<DraftView>> ReadAsync(ReportScope scope, CancellationToken ct)
    {
        if (await classroom.ReadAsync(scope, ct) is null) return new(null, 404);
        return new(View(await repository.DraftAsync(scope, ct) ?? new(scope)));
    }
    public async Task<ReportResult<DraftView>> SaveAsync(ReportScope scope, DraftInput input, CancellationToken ct)
    {
        if (await classroom.ReadAsync(scope, ct) is null) return new(null, 404);
        var errors = input.Validate(); if (errors.Count > 0) return new(null, 400, errors);
        return await repository.WithScopeLockAsync(scope, async () =>
        {
            var draft = await repository.DraftAsync(scope, ct);
            if ((draft?.Version ?? 0) != input.Version) return new ReportResult<DraftView>(null, 409, Message: "Bản nháp đã thay đổi. Tải lại trước khi lưu.");
            if (draft is null) { draft = new(scope); repository.Add(draft); }
            draft.Save(input, clock.GetUtcNow()); await repository.SaveAsync(ct); return new(View(draft));
        }, ct);
    }
    public static DraftView View(MonthlyReportDraft draft) => new(draft.ClassId, draft.StudentId, draft.Month.ToString("yyyy-MM"), draft.DisplayName,
        draft.ParentDisplayName, draft.ContactDisplay, draft.Comment, draft.Roadmap, draft.Emoji, draft.Version, draft.SavedAt);
}
