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

namespace EducationKeeptrack.Reporting;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, string connection)
    {
        services.AddDbContext<ReportingDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "reporting")));
        services.AddScoped<IReportingRepository, ReportingRepository>(); services.AddScoped<DraftService>(); services.AddScoped<ReportSourceCoordinator>(); services.AddScoped<PreviewService>(); return services;
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
