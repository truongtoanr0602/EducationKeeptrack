using System.Security.Claims;
using EducationKeeptrack.Classroom.Application;
using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Classroom;

public static class RosterEndpoints
{
    public static void MapRoster(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api").RequireAuthorization(policy => policy.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite)
                ? await next(context)
                : Results.Json(new { message = access.Status == "expired" ? "Quyền sử dụng đã hết hạn. Bạn chỉ có thể xem dữ liệu đã lưu." : "Quyền sử dụng hiện tại không cho phép thao tác này.", status = access.Status }, statusCode: 403);
        });
        group.MapGet("/students", async (HttpContext http, RosterService roster) =>
            Results.Ok(await roster.ListStudentsAsync(Owner(http), http.RequestAborted)));
        group.MapGet("/students/{id:guid}", async (Guid id, HttpContext http, RosterService roster) =>
        {
            var student = await roster.FindStudentAsync(Owner(http), id, http.RequestAborted);
            return student is null ? Results.NotFound() : Results.Ok(student);
        });
        group.MapPost("/students", async (StudentInput input, HttpContext http, RosterService roster) =>
        {
            var result = await roster.SaveStudentAsync(Owner(http), null, input, http.RequestAborted);
            return Respond(result, result.Value is { } student ? $"/api/students/{student.Id}" : null);
        });
        group.MapPut("/students/{id:guid}", async (Guid id, StudentInput input, HttpContext http, RosterService roster) =>
            Respond(await roster.SaveStudentAsync(Owner(http), id, input, http.RequestAborted)));
        group.MapGet("/classes/{classId:guid}/enrollments", async (Guid classId, HttpContext http, RosterService roster) =>
            Respond(await roster.ListEnrollmentsAsync(Owner(http), classId, http.RequestAborted)));
        group.MapPost("/classes/{classId:guid}/enrollments", async (Guid classId, EnrollmentInput input, HttpContext http, RosterService roster) =>
        {
            var result = await roster.EnrollAsync(Owner(http), classId, input, http.RequestAborted);
            return Respond(result, result.Value is { } item ? $"/api/classes/{classId}/enrollments/{item.Id}" : null);
        });
        group.MapPut("/classes/{classId:guid}/enrollments/{id:guid}", async (Guid classId, Guid id, EnrollmentStatusInput input, HttpContext http, RosterService roster) =>
            Respond(await roster.ChangeStatusAsync(Owner(http), classId, id, input, http.RequestAborted)));
    }
    private static IResult Respond<T>(RosterResult<T> result, string? location = null) where T : class => result.Failure switch
    {
        RosterFailure.NotFound => Results.NotFound(),
        RosterFailure.Invalid => Results.ValidationProblem(result.Errors!),
        RosterFailure.Conflict => Results.Conflict(new { message = result.Message }),
        _ => location is null ? Results.Ok(result.Value) : Results.Created(location, result.Value)
    };
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
