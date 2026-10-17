using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Classroom.Application;
using EducationKeeptrack.Classroom.Domain;
using EducationKeeptrack.Classroom.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Classroom;

public static class ClassroomModule
{
    public static IServiceCollection AddClassroomModule(this IServiceCollection services, string connection)
    {
        services.AddDbContext<ClassroomDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "classroom")));
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<ClassroomService>();
        services.AddScoped<IRosterRepository, RosterRepository>();
        services.AddScoped<RosterService>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<SessionService>();
        services.AddScoped<IClassroomBillingSource, ClassroomBillingSource>();
        services.AddHostedService<ClassroomChangeDispatcher>();
        return services;
    }

    public static void MapClassroom(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/classes").RequireAuthorization(policy => policy.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            var allowed = HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite;
            return allowed ? await next(context) : Results.Json(new { message = AccessMessage(access.Status), status = access.Status }, statusCode: 403);
        });
        group.MapGet("/", async (HttpContext http, ClassroomService classes) =>
            Results.Ok((await classes.ListAsync(Owner(http), http.RequestAborted)).Select(View)));
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, ClassroomService classes) =>
        {
            var item = await classes.FindAsync(Owner(http), id, http.RequestAborted);
            return item is null ? Results.NotFound() : Results.Ok(View(item));
        });
        group.MapPost("/", async (ClassInput input, HttpContext http, ClassroomService classes) =>
        {
            var errors = input.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var item = await classes.CreateAsync(Owner(http), input, http.RequestAborted);
            return Results.Created($"/api/classes/{item.Id}", View(item));
        });
        // Direct-ID edits share the same tenant filter; no client-provided owner accepted.
        group.MapPut("/{id:guid}", async (Guid id, ClassInput input, HttpContext http, IClassRepository repository) =>
        {
            var item = await repository.FindAsync(Owner(http), id, http.RequestAborted);
            if (item is null) return Results.NotFound();
            var errors = input.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            item.Update(input);
            await repository.SaveAsync(item, http.RequestAborted);
            return Results.Ok(View(item));
        });
    }
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static object View(TeacherClass item) => new { item.Id, item.Name, item.Subject, item.Description };
    private static string AccessMessage(string status) => status switch
    {
        "expired" => "Quyền sử dụng đã hết hạn. Bạn vẫn có thể xem lớp đã lưu.",
        "unverified" => "Tài khoản chưa xác minh email.",
        "pending" => "Tài khoản đang chờ duyệt quyền sử dụng.",
        _ => "Tài khoản hiện không được truy cập."
    };
}
