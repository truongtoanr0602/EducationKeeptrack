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
        var existing = await repository.ExportByKeyAsync(scope.OwnerId, input.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (!existing.Matches(scope, input)) return new(null, 409, Message: "Khóa chống lặp đã dùng cho lệnh xuất khác.");
            if (existing.State == "ready") return new(View(existing, (await repository.PreviewAsync(scope, existing.PreviewId, ct))!));
            return new(null, 409, Message: "Bản xuất đang xử lý hoặc cần thử lại.");
        }
        var preview = await repository.PreviewAsync(scope, input.PreviewId, ct);
        if (preview is null) return new(null, 404);
        if (!await ValidAsync(scope, preview, input.DraftVersion, ct)) return Stale();
        var export = new ReportExport(scope, input, clock.GetUtcNow());
        await repository.WithScopeLockAsync(scope, async () => { repository.Add(export); await repository.SaveAsync(ct); return true; }, ct);
        try
        {
            var payment = await billing.PaymentAsync(scope, preview.Content().Sources.Billing, ct);
            var pdf = await renderer.RenderAsync(template.Render(preview, payment), ct);
            if (!await ValidAsync(scope, preview, input.DraftVersion, ct)) { export.Fail(); await repository.SaveAsync(ct); return Stale(); }
            var key = $"reports/{ReportStamp.Of(scope.OwnerId)}/{export.Id:N}/{Guid.NewGuid():N}.pdf";
            await store.PutAsync(key, pdf, ct);
            if (!(await access.GetAsync(scope.OwnerId, ct)).CanRead) { export.Fail(); await repository.SaveAsync(ct); return new(null, 403); }
            if (!await ValidAsync(scope, preview, input.DraftVersion, ct)) { export.Fail(); await repository.SaveAsync(ct); return Stale(); }
            export.Complete(key, Convert.ToHexString(SHA256.HashData(pdf)), pdf.LongLength, JsonSerializer.Serialize(payment.Warnings), clock.GetUtcNow());
            await repository.SaveAsync(ct); return new(View(export, preview));
        }
        catch (ReportSourceChangedException) { export.Fail(); await repository.SaveAsync(ct); return Stale(); }
        catch (Exception e) when (e is ReportStorageException or ReportRenderException)
        { export.Fail(); await repository.SaveAsync(ct); return new(null, 503, Message: e.Message); }
    }
    public async Task<bool> ValidAsync(ReportScope scope, ReportPreview preview, long version, CancellationToken ct) => preview.ExpiresAt > clock.GetUtcNow() && preview.DraftVersion == version
        && await repository.DraftVersionAsync(scope, ct) == version && await coordinator.ValidateAsync(scope, preview.Content().Sources, ct);
    private static ReportResult<ExportView> Stale() => new(null, 409, Message: "Nguồn hoặc bản nháp đã thay đổi, hoặc preview hết hạn. Vui lòng làm mới.");
    public static ExportView View(ReportExport export, ReportPreview preview) => new(export.Id, export.State, export.CreatedAt, export.ByteLength, export.Sha256, export.DraftVersion, preview.Content().Sources.Stamp, JsonSerializer.Deserialize<string[]>(export.WarningsJson)!);
}
