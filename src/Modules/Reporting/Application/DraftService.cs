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
    Task<T> WithExportLockAsync<T>(string owner, Guid key, Func<Task<T>> action, CancellationToken ct);
    void Add(MonthlyReportDraft draft);
    void Add(ReportPreview preview);
    Task<ReportPreview?> PreviewAsync(ReportScope scope, Guid id, CancellationToken ct);
    Task<long> DraftVersionAsync(ReportScope scope, CancellationToken ct);
    Task<ReportExport?> ExportByKeyAsync(string owner, Guid key, CancellationToken ct);
    Task<ReportExport?> ExportAsync(ReportScope scope, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ReportExport>> ExportsAsync(ReportScope scope, CancellationToken ct);
    void Add(ReportExport export);
    Task SaveAsync(CancellationToken ct);
}

public sealed class DraftService(
    IReportingRepository repository,
    IClassroomReportSource classroom,
    ReportSourceCoordinator coordinator,
    IAiQuota quota,
    IAiCommentEngine aiEngine,
    TimeProvider clock)
{
    public async Task<ReportResult<DraftView>> ReadAsync(ReportScope scope, CancellationToken ct)
    {
        if (await classroom.ReadAsync(scope, ct) is null) return new(null, 404);
        return new(View(await repository.DraftAsync(scope, ct) ?? new(scope)));
    }

    public async Task<ReportResult<DraftView>> SaveAsync(ReportScope scope, DraftInput input, CancellationToken ct)
    {
        if (await classroom.ReadAsync(scope, ct) is null) return new(null, 404);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, 400, errors);
        return await repository.WithScopeLockAsync(scope, async () =>
        {
            var draft = await repository.DraftAsync(scope, ct);
            if ((draft?.Version ?? 0) != input.Version) return new ReportResult<DraftView>(null, 409, Message: "Bản nháp đã thay đổi. Tải lại trước khi lưu.");
            if (draft is null) { draft = new(scope); repository.Add(draft); }
            draft.Save(input, clock.GetUtcNow());
            await repository.SaveAsync(ct);
            return new(View(draft));
        }, ct);
    }

    public async Task<ReportResult<AiCommentProposal>> GenerateAiCommentAsync(ReportScope scope, CancellationToken ct)
    {
        var classroomSnapshot = await classroom.ReadAsync(scope, ct);
        if (classroomSnapshot is null) return new(null, 404);

        var requestKey = $"comment:{scope.ClassId}:{scope.StudentId}:{scope.Month:yyyy-MM}";
        var reservation = await quota.ReserveAsync(scope.OwnerId, AiTaskKind.Comment, requestKey, units: 1, ct);
        if (reservation.Outcome == AiReservationOutcome.Denied)
        {
            var msg = reservation.DeniedReason switch
            {
                "ai_disabled" => "Tài khoản chưa được kích hoạt quyền AI. Vui lòng liên hệ Admin.",
                "no_quota" or "exhausted" => "Hạn mức lượt nhận xét AI của kỳ này đã hết.",
                "expired" => "Thời hạn dịch vụ của tài khoản đã hết.",
                _ => "Không thể sử dụng AI lúc này (" + reservation.DeniedReason + ")."
            };
            return new(null, 403, Message: msg);
        }

        try
        {
            var snapshot = await coordinator.CaptureAsync(scope, ct);
            var sessions = snapshot.Value?.Classroom.Sessions ?? classroomSnapshot.Sessions;
            var assessments = snapshot.Value?.Learning.Assessments ?? Array.Empty<ReportAssessment>();

            var proposal = await aiEngine.GenerateCommentAsync(
                classroomSnapshot.Profile.Name,
                classroomSnapshot.ClassName,
                classroomSnapshot.Subject,
                scope.Month,
                sessions,
                assessments,
                ct);

            if (reservation.ReservationId.HasValue)
            {
                var committed = await quota.CommitAsync(reservation.ReservationId.Value, ct);
                if (!committed)
                    return new(null, 403, Message: "Không thể trừ lượt AI do tài khoản bị hạn chế.");
            }

            return new(proposal);
        }
        catch (Exception)
        {
            if (reservation.ReservationId.HasValue)
                await quota.ReleaseAsync(reservation.ReservationId.Value, ct);
            return new(null, 502, Message: "Không thể kết nối dịch vụ AI. Lượt đã được hoàn trả, bạn vẫn có thể tự soạn nhận xét.");
        }
    }

    public static DraftView View(MonthlyReportDraft draft) => new(draft.ClassId, draft.StudentId, draft.Month.ToString("yyyy-MM"), draft.DisplayName,
        draft.ParentDisplayName, draft.ContactDisplay, draft.Comment, draft.Roadmap, draft.Emoji, draft.Version, draft.SavedAt);
}
