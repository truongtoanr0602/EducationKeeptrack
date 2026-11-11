using System.Security.Claims;
using EducationKeeptrack.ServiceAccess.Application;
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
        group.MapGet("/teachers", async (HttpContext http, ServiceAdministration admin) => Results.Ok(await admin.TeachersAsync(http.RequestAborted)));
        group.MapGet("/teachers/{id}", async (string id, HttpContext http, ServiceAdministration admin) =>
            await admin.TeacherAsync(id, http.RequestAborted) is { } teacher ? Results.Ok(teacher) : Results.NotFound());
        group.MapGet("/payment-instructions", async (HttpContext http, ServiceAdministration admin) => Results.Ok(await admin.InstructionsAsync(http.RequestAborted)));
        group.MapPut("/payment-instructions", async (ServiceAdministration.InstructionsInput input, HttpContext http, ServiceAdministration admin) =>
            Respond(await admin.SaveInstructionsAsync(Actor(http), input, http.RequestAborted)));
        group.MapPost("/payment-requests/{id:guid}/verification", async (Guid id, ServiceAdministration.NoteInput input, HttpContext http, ServiceAdministration admin) =>
            Respond(await admin.VerifyRequestAsync(Actor(http), id, input, http.RequestAborted)));
        group.MapPost("/teachers/{id}/period-preview", async (string id, PreviewInput input, HttpContext http, ServiceAdministration admin) =>
            Respond(await admin.PreviewAsync(id, input.Days, http.RequestAborted)));
        group.MapPost("/payment-requests/{id:guid}/approve", async (Guid id, ServiceAdministration.ApproveInput input, HttpContext http, ServiceAdministration admin) =>
            Respond(await admin.ApproveAsync(Actor(http), id, input, http.RequestAborted)));
        group.MapPost("/payment-requests/{id:guid}/reject", async (Guid id, ServiceAdministration.ReasonInput input, HttpContext http, ServiceAdministration admin) =>
            Respond(await admin.RejectAsync(Actor(http), id, input, http.RequestAborted)));
        group.MapPost("/teachers/{id}/periods", async (string id, PeriodAdministration.GrantInput input, HttpContext http, PeriodAdministration periods) =>
            Respond(await periods.GrantAsync(Actor(http), id, input, http.RequestAborted)));
        group.MapPut("/periods/{id:guid}", async (Guid id, PeriodAdministration.PeriodEdit input, HttpContext http, PeriodAdministration periods) =>
            Respond(await periods.UpdateAsync(Actor(http), id, input, http.RequestAborted)));
        group.MapPost("/teachers/{id}/lock", async (string id, PeriodAdministration.ReasonOnly input, HttpContext http, PeriodAdministration periods) =>
            Respond(await periods.SetLockedAsync(Actor(http), id, true, input.Reason, http.RequestAborted)));
        group.MapPost("/teachers/{id}/unlock", async (string id, PeriodAdministration.ReasonOnly input, HttpContext http, PeriodAdministration periods) =>
            Respond(await periods.SetLockedAsync(Actor(http), id, false, input.Reason, http.RequestAborted)));
        return group;
    }

    internal static Actor Actor(HttpContext http) =>
        new(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!, http.User.FindFirstValue(ClaimTypes.Name)!);

    internal static IResult Respond(Outcome outcome) => outcome.Status switch
    {
        400 when outcome.Body is IDictionary<string, string[]> errors => Results.ValidationProblem(errors),
        404 => Results.NotFound(),
        _ => Results.Json(outcome.Body, statusCode: outcome.Status)
    };

    public record PreviewInput(int Days);
}
