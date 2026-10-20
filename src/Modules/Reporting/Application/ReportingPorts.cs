using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Domain;

namespace EducationKeeptrack.Reporting.Application;

public interface IReportHtmlTemplate { string Render(ReportPreview preview, ReportPayment payment); }
public interface IReportPdfRenderer { Task<byte[]> RenderAsync(string html, CancellationToken ct); }
public interface IReportObjectStore
{
    Task PutAsync(string key, byte[] pdf, CancellationToken ct);
    Task<Stream> OpenAsync(string key, CancellationToken ct);
}
public sealed class ReportStorageException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ReportRenderException(string message, Exception? inner = null) : Exception(message, inner);
