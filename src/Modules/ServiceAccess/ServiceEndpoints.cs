using System.Security.Claims;
using EducationKeeptrack.ServiceAccess.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.ServiceAccess;

// Pending, rejected and expired teachers still reach this profile; business modules gate on IAccountAccess.
public static class ServiceEndpoints
{
    public static RouteGroupBuilder MapServiceProfile(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/service").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.MapGet("/", async (HttpContext http, ServiceDesk desk) => Results.Ok(await desk.ProfileAsync(Owner(http), http.RequestAborted)));
        group.MapPost("/payment-requests", async (ServiceDesk.RequestInput input, HttpContext http, ServiceDesk desk) =>
            AdminEndpoints.Respond(await desk.SubmitAsync(Owner(http), input, http.RequestAborted)));
        return group;
    }

    internal static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
