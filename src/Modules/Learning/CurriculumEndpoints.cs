using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Learning;

/// <summary>Class curriculum routes; the shapes follow ui/src/personalization-api.ts.</summary>
public static class CurriculumEndpoints
{
    public static void MapCurriculum(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}/curriculum").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(LearningAccess.Require);
        group.MapGet("/", async (Guid classId, HttpContext http, CurriculumService service) =>
            Respond(await service.GetAsync(LearningAccess.Owner(http), classId, http.RequestAborted)));
        group.MapPut("/", async (Guid classId, CurriculumInput input, HttpContext http, CurriculumService service) =>
            Respond(await service.ConfigureAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted)));
        group.MapPost("/skills", async (Guid classId, SkillInput input, HttpContext http, CurriculumService service) =>
            Respond(await service.AddSkillAsync(LearningAccess.Owner(http), classId, input, http.RequestAborted), 201));
        group.MapPut("/skills/{skillId:guid}", async (Guid classId, Guid skillId, SkillInput input, HttpContext http, CurriculumService service) =>
            Respond(await service.EditSkillAsync(LearningAccess.Owner(http), classId, skillId, input, http.RequestAborted)));
    }

    private static IResult Respond(LearningResult<ClassCurriculum> result, int success = 200) => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        409 => Results.Conflict(new { message = "Chương trình đã thay đổi ở phiên khác. Tải lại trước khi lưu; nội dung đang nhập được giữ nguyên." }),
        _ => Results.Json(View(result.Value), statusCode: success)
    };

    internal static object View(ClassCurriculum? item) => item is null
        ? new
        {
            configured = false, version = 0, subject = "", grade = "", term = "", textbook = "",
            rule = new { threshold = ClassCurriculum.DefaultThreshold, minTasks = ClassCurriculum.DefaultMinTasks, requiredAttempts = ClassCurriculum.DefaultAttempts, version = 0 },
            skills = Array.Empty<object>()
        }
        : new
        {
            configured = true, item.Version, item.Subject, item.Grade, item.Term, item.Textbook,
            rule = new { threshold = item.MasteryThreshold, minTasks = item.MinTasks, requiredAttempts = item.RequiredAttempts, version = item.RuleVersion },
            skills = item.Skills.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.CreatedAt).Select(SkillView)
        };

    private static object SkillView(Skill skill) => new
    {
        skill.Id, skill.Code, skill.Name, skill.Strand, taught = skill.IsTaught,
        taughtOn = skill.TaughtOn?.ToString("yyyy-MM-dd"), skill.Archived, skill.Criteria, skill.Source, skill.Status,
        history = skill.Events.OrderBy(x => x.CurriculumVersion).ThenBy(x => x.At).Select(x => new { x.Action, x.At, x.Detail })
    };
}

/// <summary>Shared access rule of Learning routes: reads need CanRead (expired accounts keep history); writes need CanWrite.</summary>
internal static class LearningAccess
{
    public static async ValueTask<object?> Require(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
        return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context)
            : Results.Json(new { message = "Tài khoản không có quyền thực hiện thao tác này.", status = access.Status }, statusCode: 403);
    }
    public static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
