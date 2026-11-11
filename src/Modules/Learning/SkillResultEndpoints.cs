using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
namespace EducationKeeptrack.Learning;

/// <summary>Per-skill results of an assessment; the shapes follow ui/src/personalization-api.ts.</summary>
public static class SkillResultEndpoints
{
    public static void MapSkillResults(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}/assessments/{assessmentId:guid}").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(LearningAccess.Require);
        group.MapGet("/skills", async (Guid classId, Guid assessmentId, HttpContext http, SkillResultService service) =>
            Respond(await service.GetAsync(LearningAccess.Owner(http), classId, assessmentId, http.RequestAborted)));
        group.MapPut("/skills", async (Guid classId, Guid assessmentId, SkillMapInput input, HttpContext http, SkillResultService service) =>
            Respond(await service.SetSkillsAsync(LearningAccess.Owner(http), classId, assessmentId, input, http.RequestAborted)));
        group.MapPut("/skill-results/{studentId:guid}/{skillId:guid}", async (Guid classId, Guid assessmentId, Guid studentId, Guid skillId, SkillResultInput input, HttpContext http, SkillResultService service) =>
        {
            var result = await service.RecordAsync(LearningAccess.Owner(http), classId, assessmentId, studentId, skillId, input, http.RequestAborted);
            return result.Status switch
            {
                404 => Results.NotFound(),
                400 => Results.ValidationProblem(result.Errors!),
                409 => Conflict(),
                _ => result.Value is null ? Results.NotFound() : Results.Ok(RowView(result.Value))
            };
        });
    }

    private static IResult Conflict() => Results.Conflict(new { message = "Kết quả đã thay đổi ở phiên khác. Tải lại trước khi lưu; nội dung đang nhập được giữ nguyên." });

    private static IResult Respond(LearningResult<SkillResultsData> result) => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        409 => Results.Conflict(new { message = "Danh sách kỹ năng của bài đã thay đổi ở phiên khác. Tải lại trước khi lưu; lựa chọn đang nhập được giữ nguyên." }),
        _ => Results.Ok(View(result.Value!))
    };

    private static object View(SkillResultsData data)
    {
        var set = data.Set;
        var byId = data.Skills.ToDictionary(x => x.Id);
        var skills = (set?.Skills ?? []).Where(x => byId.ContainsKey(x.SkillId)).OrderBy(x => byId[x.SkillId].Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => new { x.SkillId, byId[x.SkillId].Code, byId[x.SkillId].Name, x.TaskCount, locked = set!.IsLocked(x.SkillId) });
        var results = (set?.Results ?? []).Where(x => byId.ContainsKey(x.SkillId)).OrderBy(x => x.StudentName).ThenBy(x => x.StudentId)
            .ThenBy(x => byId[x.SkillId].Code, StringComparer.OrdinalIgnoreCase).Select(RowView);
        return new
        {
            assessmentId = data.Assessment.Id, title = data.Assessment.Title, date = data.Assessment.Date.ToString("yyyy-MM-dd"),
            version = set?.Version ?? 0, skills, results
        };
    }

    private static object RowView(SkillResult row) => new
    {
        row.StudentId, row.StudentName, row.SkillId, row.Status, row.Correct, row.Note, row.Version,
        finalResult = row.GetFinalResult() is { } final ? RevisionView(final) : null,
        history = row.History.OrderBy(x => x.Version).Select(RevisionView)
    };

    private static object RevisionView(SkillRevision x) => new { x.Version, x.Status, x.TaskCount, x.Correct, x.Note, x.Action, x.RecordedAt };
}
