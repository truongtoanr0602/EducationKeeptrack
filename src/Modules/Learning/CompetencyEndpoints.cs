using EducationKeeptrack.Learning.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Learning;

/// <summary>Competency profile route; the shape follows ui/src/personalization-api.ts.</summary>
public static class CompetencyEndpoints
{
    public static void MapCompetency(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}/competency").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(LearningAccess.Require);
        group.MapGet("/", async (Guid classId, HttpContext http, CompetencyService service) =>
        {
            var result = await service.ProfileAsync(LearningAccess.Owner(http), classId, http.RequestAborted);
            return result.Status == 404 ? Results.NotFound() : Results.Ok(View(result.Value!));
        });
    }

    private static object View(CompetencyProfile profile) => new
    {
        profile.Configured,
        rule = profile.Rule is { } rule ? new { rule.Threshold, rule.MinTasks, rule.RequiredAttempts, rule.Version } : null,
        profile.CurriculumVersion,
        students = profile.Students.Select(student => new
        {
            student.StudentId, student.Name,
            skills = student.Skills.Select(x => new
            {
                skillId = x.Skill.Id, x.Skill.Code, x.Skill.Name, x.Skill.Strand, x.Result.State, x.Result.MissingAttempts,
                evidence = x.Result.Evidence.Select(e => new
                {
                    e.AssessmentId, e.Title, date = e.Date.ToString("yyyy-MM-dd"), e.Status, e.TaskCount, e.Correct, e.Counted, e.MeetsThreshold, e.Excluded
                })
            })
        })
    };
}
