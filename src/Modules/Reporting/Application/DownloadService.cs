using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.Extensions.Options;

namespace EducationKeeptrack.Reporting.Application;

public record DownloadLinkView(string Url,DateTimeOffset ExpiresAt);
public record ReportFile(Stream Stream,string FileName,long ByteLength);
public sealed class DownloadService(IReportingRepository repository,IReportObjectStore store,DownloadTokens tokens,IAccountAccess access,TimeProvider clock,IOptions<ReportingOptions> options)
{
    public async Task<ReportResult<DownloadLinkView>> IssueAsync(ReportScope scope,Guid id,CancellationToken ct)
    {
        var export=await repository.ExportAsync(scope,id,ct);
        if(export is null) return new(null,404);
        if(!(await access.GetAsync(scope.OwnerId,ct)).CanRead) return new(null,403);
        if(export.State!="ready") return new(null,409,Message:"PDF chưa sẵn sàng.");
        var expires=clock.GetUtcNow().AddMinutes(Math.Clamp(options.Value.DownloadMinutes,1,30));
        var token=tokens.Issue(scope.OwnerId,id,expires);
        return new(new($"/api/reporting/classes/{scope.ClassId}/students/{scope.StudentId}/months/{scope.Month:yyyy-MM}/exports/{id}/file?token={Uri.EscapeDataString(token)}",expires));
    }
    public async Task<ReportResult<ReportFile>> OpenAsync(ReportScope scope,Guid id,string token,CancellationToken ct)
    {
        var export=await repository.ExportAsync(scope,id,ct);
        if(export is null) return new(null,404);
        if(!tokens.Validate(token,scope.OwnerId,id,clock.GetUtcNow()) || !(await access.GetAsync(scope.OwnerId,ct)).CanRead) return new(null,403);
        if(export.State!="ready" || export.ObjectKey is null) return new(null,409,Message:"PDF chưa sẵn sàng.");
        try
        {
            var stream=await store.OpenAsync(export.ObjectKey,ct);
            if(!(await access.GetAsync(scope.OwnerId,ct)).CanRead || !tokens.Validate(token,scope.OwnerId,id,clock.GetUtcNow())) { await stream.DisposeAsync(); return new(null,403); }
            return new(new(stream,$"bao-cao-{scope.Month:yyyy-MM}-{scope.StudentId:N}.pdf",export.ByteLength));
        }
        catch(ReportStorageException e) { return new(null,503,Message:e.Message); }
    }
}
