using System.Security.Claims;
using EducationKeeptrack.Classroom.Application;
using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Classroom;

public static class SessionEndpoints
{
    public static void MapSessions(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/classes/{classId:guid}").RequireAuthorization(policy => policy.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context)
                : Results.Json(new { message = access.Status == "expired" ? "Quyền sử dụng đã hết hạn. Bạn chỉ có thể xem dữ liệu đã lưu." : "Quyền sử dụng hiện tại không cho phép thao tác này.", status = access.Status }, statusCode: 403);
        });
        group.MapGet("/schedule", async (Guid classId, HttpContext http, SessionService service) => Respond(await service.ScheduleAsync(Owner(http), classId, http.RequestAborted)));
        group.MapPut("/schedule", async (Guid classId, ScheduleInput input, HttpContext http, SessionService service) => Respond(await service.SaveScheduleAsync(Owner(http), classId, input, http.RequestAborted)));
        group.MapGet("/sessions", async (Guid classId, DateOnly from, DateOnly to, HttpContext http, SessionService service) => Respond(await service.ListAsync(Owner(http), classId, new(from, to), http.RequestAborted)));
        group.MapPost("/sessions/generate", async (Guid classId, GenerateSessionsInput input, HttpContext http, SessionService service) => Respond(await service.GenerateAsync(Owner(http), classId, input, http.RequestAborted)));
        group.MapPost("/sessions", async (Guid classId, SessionInput input, HttpContext http, SessionService service) =>
        {
            var result = await service.CreateAsync(Owner(http), classId, input, http.RequestAborted);
            return result.Failure is null ? Results.Created($"/api/classes/{classId}/sessions/{result.Value!.Id}", result.Value) : Respond(result);
        });
        group.MapPut("/sessions/{id:guid}", async (Guid classId, Guid id, SessionStatusInput input, HttpContext http, SessionService service) => Respond(await service.ChangeStatusAsync(Owner(http), classId, id, input, http.RequestAborted)));
        group.MapGet("/sessions/{id:guid}/attendance", async (Guid classId, Guid id, HttpContext http, SessionService service) => Respond(await service.AttendanceAsync(Owner(http), classId, id, http.RequestAborted)));
        group.MapPut("/sessions/{id:guid}/attendance/{studentId:guid}", async (Guid classId, Guid id, Guid studentId, AttendanceInput input, HttpContext http, SessionService service) => Respond(await service.MarkAsync(Owner(http), classId, id, studentId, input, http.RequestAborted)));
        group.MapGet("/attendance-missing", async (Guid classId, DateOnly from, DateOnly to, HttpContext http, SessionService service) => Respond(await service.MissingAsync(Owner(http), classId, new(from, to), http.RequestAborted)));
        group.MapGet("/sessions/{id:guid}/history", async (Guid classId, Guid id, HttpContext http, SessionService service) => Respond(await service.HistoryAsync(Owner(http), classId, id, http.RequestAborted)));
    }
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IResult Respond<T>(ClassroomResult<T> result) where T : class => result.Failure switch
    {
        ClassroomFailure.NotFound => Results.NotFound(),
        ClassroomFailure.Invalid => Results.ValidationProblem(result.Errors!),
        ClassroomFailure.Conflict => Results.Conflict(new { message = result.Message }),
        _ => Results.Ok(result.Value)
    };
}
