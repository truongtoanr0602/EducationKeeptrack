using System.Security.Cryptography;
using System.Text.Json;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Application;

public record ExportView(Guid Id, string State, DateTimeOffset CreatedAt, long ByteLength, string? Sha256, long DraftVersion, string SourceStamp, IReadOnlyList<string> Warnings);
public sealed class ExportService(IReportingRepository repository, ReportSourceCoordinator coordinator, IBillingReportSource billing,
    IReportHtmlTemplate template, IReportPdfRenderer renderer, IReportObjectStore store, IAccountAccess access, TimeProvider clock)
{
    public async Task<ReportResult<ExportView>> ExportAsync(ReportScope scope, ExportInput input, CancellationToken ct)
    {
        if (input.IdempotencyKey == Guid.Empty || input.PreviewId == Guid.Empty || input.DraftVersion < 0) return new(null, 400, Message: "Thiếu preview, phiên bản hoặc khóa chống gửi lặp.");
        Guid? attempt = null;
        ReportPreview? preview = null;
        var reservation = await repository.WithExportLockAsync(scope.OwnerId,input.IdempotencyKey,async () =>
        {
            var existing = await repository.ExportByKeyAsync(scope.OwnerId,input.IdempotencyKey,ct);
            if (existing is not null && !existing.Matches(scope,input)) return Conflict("Khóa chống lặp đã dùng cho lệnh khác.");
            preview = await repository.PreviewAsync(scope,input.PreviewId,ct);
            if (preview is null) return new ReportResult<ExportView>(null,404);
            if (existing?.State == "ready") return new(View(existing,preview));
            if (existing?.LeaseUntil > clock.GetUtcNow() && existing.State == "pending") return Conflict("export-in-progress");
            if (!await ValidAsync(scope,preview,input.DraftVersion,ct)) return Stale();
            if (existing is null) { existing = new(scope,input,clock.GetUtcNow()); repository.Add(existing); }
            attempt = existing.Acquire(clock.GetUtcNow());
            await repository.SaveAsync(ct);
            return new ReportResult<ExportView>(null,202);
        },ct);
        if (reservation.Status != 202) return reservation;
        try
        {
            var payment = await billing.PaymentAsync(scope,preview!.Content().Sources.Billing,ct);
            var pdf = await renderer.RenderAsync(template.Render(preview,payment),ct);
            if (!await ValidAsync(scope,preview,input.DraftVersion,ct)) { await FailAsync(); return Stale(); }
            var key = $"reports/{ReportStamp.Of(scope.OwnerId)}/{input.IdempotencyKey:N}/{attempt:N}.pdf";
            await store.PutAsync(key,pdf,ct);
            return await repository.WithExportLockAsync(scope.OwnerId,input.IdempotencyKey,async () =>
            {
                var current = (await repository.ExportByKeyAsync(scope.OwnerId,input.IdempotencyKey,ct))!;
                if (!current.Owns(attempt!.Value,clock.GetUtcNow())) return Conflict("export-in-progress");
                if (!(await access.GetAsync(scope.OwnerId,ct)).CanRead) { current.Fail(); await repository.SaveAsync(ct); return new ReportResult<ExportView>(null,403); }
                if (!await ValidAsync(scope,preview,input.DraftVersion,ct)) { current.Fail(); await repository.SaveAsync(ct); return Stale(); }
                current.Complete(key,Convert.ToHexString(SHA256.HashData(pdf)),pdf.LongLength,JsonSerializer.Serialize(payment.Warnings),clock.GetUtcNow());
                await repository.SaveAsync(ct); return new ReportResult<ExportView>(View(current,preview));
            },ct);
        }
        catch (ReportSourceChangedException) { await FailAsync(); return Stale(); }
        catch (Exception e) when (e is ReportStorageException or ReportRenderException)
        { await FailAsync(); return new(null,503,Message:e.Message); }
        catch { await FailAsync(); throw; }

        async Task FailAsync()
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await repository.WithExportLockAsync(scope.OwnerId,input.IdempotencyKey,async () =>
            {
                var current = await repository.ExportByKeyAsync(scope.OwnerId,input.IdempotencyKey,cleanup.Token);
                if (current?.Owns(attempt!.Value,clock.GetUtcNow()) == true) { current.Fail(); await repository.SaveAsync(cleanup.Token); }
                return true;
            },cleanup.Token);
        }
    }
    public async Task<bool> ValidAsync(ReportScope scope, ReportPreview preview, long version, CancellationToken ct) => preview.ExpiresAt > clock.GetUtcNow() && preview.DraftVersion == version
        && await repository.DraftVersionAsync(scope,ct) == version && await coordinator.ValidateAsync(scope,preview.Content().Sources,ct);
    private static ReportResult<ExportView> Conflict(string message) => new(null,409,Message:message);
    private static ReportResult<ExportView> Stale() => Conflict("Nguồn hoặc bản nháp đã thay đổi, hoặc preview hết hạn. Vui lòng làm mới.");
    public static ExportView View(ReportExport export, ReportPreview preview) => new(export.Id,export.State,export.CreatedAt,export.ByteLength,export.Sha256,export.DraftVersion,preview.Content().Sources.Stamp,JsonSerializer.Deserialize<string[]>(export.WarningsJson)!);
}
