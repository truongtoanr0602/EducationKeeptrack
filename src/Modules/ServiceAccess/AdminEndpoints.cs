using System.Security.Claims;
using EducationKeeptrack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.ServiceAccess;

// Admin manages service access only: no route here reads classes, students, photos, PDFs or tuition.
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapServiceAdmin(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        group.MapGet("/teachers", async (HttpContext http, IAccountDirectory directory, IAccountAccess access) =>
        {
            var rows = new List<object>();
            foreach (var teacher in await directory.ListTeachersAsync(http.RequestAborted))
                rows.Add(Summary(teacher, await access.GetAsync(teacher.Id, http.RequestAborted)));
            return Results.Ok(rows);
        });
        group.MapGet("/teachers/{id}", async (string id, HttpContext http, IAccountDirectory directory, IAccountAccess access) =>
        {
            var teacher = await directory.FindTeacherAsync(id, http.RequestAborted);
            return teacher is null ? Results.NotFound() : Results.Ok(Summary(teacher, await access.GetAsync(id, http.RequestAborted)));
        });
        return group;
    }

    internal static string Actor(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static object Summary(AccountSummary teacher, AccountAccess access) => new
    {
        teacher.Id, teacher.Email, teacher.EmailConfirmed, teacher.IsBlocked, teacher.RegisteredAt, status = access.Status
    };
}
