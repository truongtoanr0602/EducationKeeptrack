using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Learning;

/// <summary>Material store routes; the shapes follow ui/src/personalization-api.ts.</summary>
public static class MaterialEndpoints
{
    public static void MapMaterials(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}/materials").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(LearningAccess.Require);
        group.MapGet("/", async (Guid classId, HttpContext http, MaterialService service) =>
        {
            var result = await service.ListAsync(LearningAccess.Owner(http), classId, http.RequestAborted);
            return result.Status == 404 ? Results.NotFound() : Results.Ok(result.Value!.Select(View));
        });
        group.MapPost("/", async (Guid classId, MaterialInput input, HttpContext http, MaterialService service) =>
            Respond(await service.CreateAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), 201));
        group.MapPut("/{id:guid}", async (Guid classId, Guid id, MaterialInput input, HttpContext http, MaterialService service) =>
            Respond(await service.EditAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted)));
        group.MapPost("/{id:guid}/approve", async (Guid classId, Guid id, MaterialVersionInput input, HttpContext http, MaterialService service) =>
            Respond(await service.ApproveAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted)));
    }

    private static IResult Respond(LearningResult<Material> result, int success = 200) => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        409 => Results.Conflict(new { message = "Học liệu đã thay đổi ở phiên khác. Tải lại trước khi lưu; nội dung đang nhập được giữ nguyên." }),
        _ => Results.Json(View(result.Value!), statusCode: success)
    };

    private static object View(Material item) => new
    {
        item.Id, item.SkillId, item.Title, item.Content, item.Minutes, item.Difficulty, item.Status, item.Version, item.UpdatedAt
    };
}
