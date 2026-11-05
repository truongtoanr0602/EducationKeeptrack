using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Learning;

public static class PersonalizedLearningEndpoints
{
    public static void MapPersonalizedLearning(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context) : Results.Json(new { message = "Tài khoản không có quyền thực hiện thao tác này." }, statusCode: 403);
        });
        group.MapGet("/profile", async (Guid classId, HttpContext h, PersonalizedLearningService s) => Respond(await s.ReadProfile(Owner(h), classId, h.RequestAborted)));
        group.MapPut("/profile", async (Guid classId, ProfileInput input, HttpContext h, PersonalizedLearningService s) => Respond(await s.SaveProfile(Owner(h), classId, input, h.RequestAborted)));
        group.MapPost("/observations", async (Guid classId, ObservationInput input, HttpContext h, PersonalizedLearningService s) => Respond(await s.Observe(Owner(h), classId, input, h.RequestAborted)));
        group.MapPost("/evidence", async (Guid classId, EvidenceInput input, HttpContext h, PersonalizedLearningService s) => Respond(await s.RecordEvidence(Owner(h), classId, input, h.RequestAborted)));
        group.MapGet("/progress", async (Guid classId, DateOnly date, HttpContext h, PersonalizedLearningService s) => Respond(await s.Progress(Owner(h), classId, date, h.RequestAborted)));
        group.MapGet("/plans", async (Guid classId, HttpContext h, PlanService s) => Respond(await s.List(Owner(h), classId, h.RequestAborted)));
        group.MapGet("/plans/{id:guid}", async (Guid classId, Guid id, HttpContext h, PlanService s) => Respond(await s.Read(Owner(h), classId, id, h.RequestAborted)));
        group.MapPost("/plans", async (Guid classId, PlanInput input, HttpContext h, PlanService s) => Respond(await s.Save(Owner(h), classId, null, input, h.RequestAborted)));
        group.MapPut("/plans/{id:guid}", async (Guid classId, Guid id, PlanInput input, HttpContext h, PlanService s) => Respond(await s.Save(Owner(h), classId, id, input, h.RequestAborted)));
        group.MapPost("/plans/{id:guid}/approve", async (Guid classId, Guid id, PlanVersion input, HttpContext h, PlanService s) => Respond(await s.Approve(Owner(h), classId, id, input, h.RequestAborted)));
        group.MapPost("/plans/generate", async (Guid classId, HttpContext h, PersonalizedLearningService s) =>
            await s.Roster(Owner(h), classId, s.Today, h.RequestAborted) is null ? Results.NotFound() : Results.Json(new { message = "AI tạo kế hoạch chưa được bật: cần nhà cung cấp và chính sách hạn mức riêng. Bạn vẫn có thể tạo kế hoạch thủ công." }, statusCode: 503));
    }
    internal static string Owner(HttpContext h) => h.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    internal static IResult Respond(PersonalResult result) => result.Status switch
    {
        404 => Results.NotFound(),
        409 => Results.Conflict(new { message = result.Message ?? "Dữ liệu đã thay đổi hoặc mã yêu cầu đã dùng. Tải lại để xem phiên bản mới; giữ nội dung đang nhập." }),
        400 => Results.BadRequest(new { message = result.Message }),
        _ => Results.Json(result.Value, statusCode: result.Status)
    };
}
