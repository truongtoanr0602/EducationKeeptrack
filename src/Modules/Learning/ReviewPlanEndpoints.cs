using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Learning;

/// <summary>Review plan routes; the shapes follow ui/src/personalization-api.ts.</summary>
public static class ReviewPlanEndpoints
{
    public static void MapReviewPlans(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(LearningAccess.Require);
        group.MapGet("/review-sessions", async (Guid classId, string? weekStart, HttpContext http, ReviewPlanService service) =>
        {
            var result = await service.SessionsAsync(LearningAccess.Owner(http), classId, weekStart, http.RequestAborted);
            return result.Status switch
            {
                404 => Results.NotFound(),
                400 => Results.ValidationProblem(result.Errors!),
                _ => Results.Ok(result.Value!.Select(x => new { x.Id, date = x.Date.ToString("yyyy-MM-dd"), x.StartTime, x.EndTime, x.Status, x.Kind }))
            };
        });
        group.MapGet("/review-plans", async (Guid classId, HttpContext http, ReviewPlanService service) =>
        {
            var result = await service.ListAsync(LearningAccess.Owner(http), classId, http.RequestAborted);
            return result.Status == 404 ? Results.NotFound() : Results.Ok(result.Value!.Select(Summary));
        });
        group.MapGet("/review-plans/{id:guid}", async (Guid classId, Guid id, HttpContext http, ReviewPlanService service) =>
            Respond(await service.GetAsync(LearningAccess.Owner(http), classId, id, http.RequestAborted)));
        group.MapPost("/review-plans", async (Guid classId, PlanCreateInput input, HttpContext http, ReviewPlanService service) =>
            Respond(await service.CreateAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), 201));
        group.MapPut("/review-plans/{id:guid}", async (Guid classId, Guid id, PlanSaveInput input, HttpContext http, ReviewPlanService service) =>
            Respond(await service.SaveAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted)));
        group.MapPost("/review-plans/{id:guid}/approve", async (Guid classId, Guid id, PlanVersionInput input, HttpContext http, ReviewPlanService service) =>
            Respond(await service.ApproveAsync(LearningAccess.Owner(http), classId, id, input, http.RequestAborted)));
    }

    private static IResult Respond(LearningResult<ReviewPlan> result, int success = 200) => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        409 => Results.Conflict(new { message = "Kế hoạch đã thay đổi ở phiên khác hoặc tuần này đã có kế hoạch. Tải lại trước khi lưu; nội dung đang nhập được giữ nguyên." }),
        _ => Results.Json(Detail(result.Value!), statusCode: success)
    };

    private static object Summary(ReviewPlan plan) => new
    {
        plan.Id, weekStart = plan.WeekStart.ToString("yyyy-MM-dd"), plan.Status, plan.Version, plan.ApprovedVersion, plan.UpdatedAt
    };

    private static object Detail(ReviewPlan plan) => new
    {
        plan.Id, weekStart = plan.WeekStart.ToString("yyyy-MM-dd"), plan.Status, plan.Version, plan.ApprovedVersion, plan.UpdatedAt,
        content = plan.GetContent(),
        approvedContent = plan.GetApprovedContent(),
        evidence = plan.GetEvidence(),
        execution = Array.Empty<object>(),
        revisions = plan.Revisions.OrderBy(x => x.Version).Select(x => new { x.Version, x.Action, x.Status, x.RecordedAt })
    };
}
