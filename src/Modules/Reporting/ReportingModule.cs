using System.Globalization;
using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Application;
using EducationKeeptrack.Reporting.Domain;
using EducationKeeptrack.Reporting.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace EducationKeeptrack.Reporting;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, string connection, IConfiguration config)
    {
        services.AddDbContext<ReportingDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "reporting")));
        services.AddScoped<IReportingRepository, ReportingRepository>(); services.AddScoped<DraftService>(); services.AddScoped<ReportSourceCoordinator>(); services.AddScoped<PreviewService>(); services.AddScoped<ExportService>();
        services.Configure<ReportingOptions>(config.GetSection("Reporting"));
        services.AddSingleton<DownloadTokens>(); services.AddScoped<DownloadService>();
        services.AddSingleton<IReportHtmlTemplate, ReportHtmlTemplate>(); services.AddSingleton<IReportPdfRenderer, ChromiumPdfRenderer>(); services.AddSingleton<IReportObjectStore, S3ReportStore>();
        return services;
    }
    public static void MapReporting(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reporting/classes/{classId:guid}/students/{studentId:guid}/months/{month}").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!DateOnly.TryParseExact((string?)http.Request.RouteValues["month"] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) || month.Year == 9999 || month == default)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Tháng không hợp lệ (yyyy-MM)."] });
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            // Export/preview metadata are read operations; only saving draft requires business write access.
            if (!access.CanRead || (HttpMethods.IsPut(http.Request.Method) && !access.CanWrite))
                return Results.Json(new { message = "Tài khoản không được thực hiện thao tác này.", status = access.Status }, statusCode: 403);
            return await next(context);
        });
        group.MapPost("/exports", async (Guid classId, Guid studentId, string month, ExportInput input, HttpContext http, ExportService service) => Respond(await service.ExportAsync(Scope(http, classId, studentId, month), input, http.RequestAborted)));
        group.MapPost("/exports/{exportId:guid}/download-link", async (Guid classId,Guid studentId,string month,Guid exportId,HttpContext http,DownloadService service) =>
        {
            http.Response.Headers.CacheControl="no-store";
            return Respond(await service.IssueAsync(Scope(http,classId,studentId,month),exportId,http.RequestAborted));
        });
        group.MapGet("/exports/{exportId:guid}/file", async (Guid classId,Guid studentId,string month,Guid exportId,string? token,HttpContext http,DownloadService service) =>
        {
            http.Response.Headers.CacheControl="no-store"; http.Response.Headers.XContentTypeOptions="nosniff";
            var result=await service.OpenAsync(Scope(http,classId,studentId,month),exportId,token??"",http.RequestAborted);
            return result.Value is { } file ? Results.File(file.Stream,"application/pdf",file.FileName,enableRangeProcessing:false) : Respond(result);
        });
        group.MapGet("/exports", async (Guid classId, Guid studentId, string month, HttpContext http, IReportingRepository repo,IClassroomReportSource classroom) =>
        {
            var scope = Scope(http, classId, studentId, month); var result = new List<ExportView>();
            if(await classroom.ReadAsync(scope,http.RequestAborted) is null) return Results.NotFound();
            foreach (var export in await repo.ExportsAsync(scope, http.RequestAborted)) { var preview = await repo.PreviewAsync(scope, export.PreviewId, http.RequestAborted); if (preview is not null) result.Add(ExportService.View(export, preview)); }
            return Results.Ok(result);
        });
        group.MapGet("/previews/{previewId:guid}/html", async (Guid classId, Guid studentId, string month, Guid previewId, HttpContext http, IReportingRepository repo, IBillingReportSource billing, IReportHtmlTemplate template, TimeProvider clock) =>
        {
            var scope = Scope(http, classId, studentId, month); var preview = await repo.PreviewAsync(scope, previewId, http.RequestAborted);
            if (preview is null) return Results.NotFound(); if (preview.ExpiresAt <= clock.GetUtcNow()) return Results.StatusCode(410);
            try { var payment = await billing.PaymentAsync(scope, preview.Content().Sources.Billing, http.RequestAborted);
                http.Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'";
                return Results.Content(template.Render(preview, payment), "text/html; charset=utf-8");
            } catch (ReportSourceChangedException) { return Results.Conflict(new { message = "Nguon thanh toan da thay doi. Vui long lam moi." }); }
        });
        group.MapPost("/previews", async (Guid classId, Guid studentId, string month, HttpContext http, PreviewService service) => Respond(await service.CreateAsync(Scope(http, classId, studentId, month), http.RequestAborted)));
        group.MapGet("/draft", async (Guid classId, Guid studentId, string month, HttpContext http, DraftService service) => Respond(await service.ReadAsync(Scope(http, classId, studentId, month), http.RequestAborted)));
        group.MapPut("/draft", async (Guid classId, Guid studentId, string month, DraftInput input, HttpContext http, DraftService service) => Respond(await service.SaveAsync(Scope(http, classId, studentId, month), input, http.RequestAborted)));
    }
    internal static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    internal static ReportScope Scope(HttpContext http, Guid classId, Guid studentId, string month) => new(Owner(http), classId, studentId, DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture));
    internal static IResult Respond<T>(ReportResult<T> result) => result.Status switch
    {
        200 => Results.Ok(result.Value), 201 => Results.Json(result.Value, statusCode: 201),
        400 => Results.ValidationProblem(result.Errors ?? new() { ["report"] = [result.Message ?? "Thông tin báo cáo không hợp lệ."] }),
        404 => Results.NotFound(), _ => Results.Json(new { message = result.Message ?? "Không thể hoàn tất báo cáo. Vui lòng làm mới hoặc thử lại." }, statusCode: result.Status)
    };
}
