using System.Globalization;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Application;

public record PreviewView(Guid Id, long DraftVersion, string SourceStamp, DateTimeOffset ExpiresAt, bool IsExportable, string TotalDueVnd,
    IReadOnlyList<string> Warnings, ReportSourceSnapshot Sources);
public sealed class PreviewService(IReportingRepository repository, ReportSourceCoordinator coordinator, TimeProvider clock)
{
    public async Task<ReportResult<PreviewView>> CreateAsync(ReportScope scope, CancellationToken ct)
    {
        var sources = await coordinator.CaptureAsync(scope, ct); if (sources.Value is null) return new(null, sources.Status, sources.Errors, sources.Message);
        var draft = await repository.DraftAsync(scope, ct) ?? new(scope);
        var preview = new ReportPreview(scope, DraftService.View(draft), sources.Value, clock.GetUtcNow()); repository.Add(preview); await repository.SaveAsync(ct);
        return new(new(preview.Id, draft.Version, sources.Value.Stamp, preview.ExpiresAt, true,
            (sources.Value.Billing.Finance.OldDebtVnd + sources.Value.Billing.Finance.PeriodDebtVnd).ToString("0", CultureInfo.InvariantCulture), [], sources.Value));
    }
}
