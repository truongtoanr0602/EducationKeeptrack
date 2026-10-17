using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Learning;

public static class LearningModule
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services, string connection)
    {
        services.AddDbContext<LearningDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "learning")));
        services.AddScoped<ILearningRepository, LearningRepository>();
        services.AddScoped<LearningService>();
        services.AddHostedService<LearningResultDispatcher>();
        return services;
    }
    public static void MapLearning(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}/assessments").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context)
                : Results.Json(new { message = "Tài khoản không có quyền thực hiện thao tác này.", status = access.Status }, statusCode: 403);
        });
        group.MapPost("/", async (Guid classId, AssessmentInput input, HttpContext http, LearningService service) =>
        {
            var result = await service.CreateAsync(Owner(http), classId, input, http.RequestAborted);
            return result.Status == 201 ? Results.Created($"/api/learning/classes/{classId}/assessments/{result.Value!.Id}", View(result.Value)) : Respond(result, View);
        });
        group.MapGet("/", async (Guid classId, HttpContext http, LearningService service) =>
            Respond(await service.ListAsync(Owner(http), classId, http.RequestAborted), items => items.Select(x => new { x.Id, x.Title, x.Date, x.DefaultMethod })));
        group.MapGet("/{id:guid}", async (Guid classId, Guid id, HttpContext http, LearningService service) =>
            Respond(await service.FindAsync(Owner(http), classId, id, http.RequestAborted), View));
        group.MapPut("/{id:guid}/submissions/{studentId:guid}", async (Guid classId, Guid id, Guid studentId, ResultInput input, HttpContext http, LearningService service) =>
            Respond(await service.RecordAsync(Owner(http), classId, id, studentId, input, http.RequestAborted), SubmissionView));
        group.MapPut("/{id:guid}/submissions/{studentId:guid}/method", async (Guid classId, Guid id, Guid studentId, MethodInput input, HttpContext http, LearningService service) =>
            Respond(await service.ChangeMethodAsync(Owner(http), classId, id, studentId, input, http.RequestAborted), SubmissionView));
    }
    private static IResult Respond<T>(LearningResult<T> result, Func<T, object> view) where T : class => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        409 => Results.Conflict(new { message = "Bài nộp đã thay đổi hoặc đã được xử lý. Tải lại trước khi lưu; nội dung đang nhập được giữ nguyên." }),
        _ => Results.Ok(view(result.Value!))
    };
    private static object View(Assessment item) => new { item.Id, item.Title, item.Date, item.DefaultMethod, submissions = item.Submissions.OrderBy(x => x.StudentName).ThenBy(x => x.StudentId).Select(SubmissionView) };
    private static object SubmissionView(Submission item) => new { item.StudentId, item.StudentName, item.Method, item.Status, item.Completion, item.Correctness, item.Version,
        finalResult = item.History.Where(x => x.Action == "confirmed").OrderByDescending(x => x.Version).FirstOrDefault(), history = item.History.OrderBy(x => x.Version) };
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
