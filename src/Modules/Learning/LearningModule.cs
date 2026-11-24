using System.Security.Claims;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Application;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EducationKeeptrack.Learning;

public static class LearningModule
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services, string connection, IConfiguration? config = null)
    {
        services.AddDbContext<LearningDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "learning")));
        if (config is not null) services.Configure<LearningStorageOptions>(config.GetSection("Learning"));
        services.AddSingleton<PhotoTokens>();
        services.AddSingleton<ILearningPhotoStore, S3LearningPhotoStore>();
        services.AddScoped<IAiGradingEngine, SimulationAiGradingEngine>();
        services.AddScoped<ILearningRepository, LearningRepository>();
        services.AddScoped<LearningService>();
        services.AddScoped<ICurriculumRepository, CurriculumRepository>();
        services.AddScoped<CurriculumService>();
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<MaterialService>();
        services.AddScoped<ISkillResultRepository, SkillResultRepository>();
        services.AddScoped<SkillResultService>();
        services.AddScoped<ICompetencyRepository, CompetencyRepository>();
        services.AddScoped<CompetencyService>();
        // The seam for a language model that words plan goals (BR-AI-15). The template explainer needs no provider.
        services.AddSingleton<IPlanExplainer, TemplatePlanExplainer>();
        services.AddScoped<IReviewPlanRepository, ReviewPlanRepository>();
        services.AddScoped<ReviewPlanService>();
        services.AddScoped<ILearningReportSource, LearningReportSource>();
        services.AddHostedService<LearningResultDispatcher>();
        services.AddSingleton<BankTokens>();
        services.AddScoped<IQuestionBankRepository, QuestionBankRepository>();
        services.AddScoped<LessonService>();
        services.AddScoped<ImportService>();
        services.AddScoped<QuestionBankService>();
        services.AddScoped<ExamService>();
        services.AddScoped<ExamTakeService>();
        // Students reach exams without an account; each address gets a fixed budget of requests (ADR-0006).
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(ExamTakeEndpoints.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static void MapLearning(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCurriculum();
        endpoints.MapMaterials();
        endpoints.MapSkillResults();
        endpoints.MapCompetency();
        endpoints.MapReviewPlans();
        endpoints.MapBank();
        endpoints.MapExamTake();

        var group = endpoints.MapGroup("/api/learning/classes/{classId:guid}/assessments").RequireAuthorization(p => p.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context)
                : Results.Json(new { message = "Tài khoản không có quyền thực hiện thao tác này.", status = access.Status }, statusCode: 403);
        });

        group.MapPost("/", async (Guid classId, AssessmentInput input, HttpContext http, LearningService service) =>
        {
            var result = await service.CreateAsync(Owner(http), classId, input, http.RequestAborted);
            return result.Status == 201 ? Results.Created($"/api/learning/classes/{classId}/assessments/{result.Value!.Id}", View(result.Value)) : Respond(result, View);
        });

        group.MapGet("/", async (Guid classId, HttpContext http, LearningService service) =>
            Respond(await service.ListAsync(Owner(http), classId, http.RequestAborted), items => items.Select(x => new { x.Id, x.Title, x.Date, x.DefaultMethod, x.Rubric, x.RubricVersion })));

        group.MapPut("/{id:guid}/rubric", async (Guid classId, Guid id, RubricInput input, HttpContext http, LearningService service) =>
            Respond(await service.UpdateRubricAsync(Owner(http), classId, id, input, http.RequestAborted), View));

        group.MapGet("/progress/{studentId:guid}", async (Guid classId, Guid studentId, DateOnly? from, DateOnly? to, HttpContext http, LearningService service) =>
            Respond(await service.ProgressAsync(Owner(http), classId, studentId, from, to, http.RequestAborted), ProgressView));

        group.MapGet("/{id:guid}", async (Guid classId, Guid id, HttpContext http, LearningService service) =>
            Respond(await service.FindAsync(Owner(http), classId, id, http.RequestAborted), View));

        group.MapPut("/{id:guid}/submissions/{studentId:guid}", async (Guid classId, Guid id, Guid studentId, ResultInput input, HttpContext http, LearningService service) =>
            Respond(await service.RecordAsync(Owner(http), classId, id, studentId, input, http.RequestAborted), SubmissionView));

        group.MapPut("/{id:guid}/submissions/{studentId:guid}/method", async (Guid classId, Guid id, Guid studentId, MethodInput input, HttpContext http, LearningService service) =>
            Respond(await service.ChangeMethodAsync(Owner(http), classId, id, studentId, input, http.RequestAborted), SubmissionView));

        group.MapPost("/{id:guid}/submissions/{studentId:guid}/photos", async (Guid classId, Guid id, Guid studentId, HttpRequest request, HttpContext http, LearningService service) =>
        {
            if (!request.HasFormContentType || request.Form.Files.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Vui lòng chọn tệp ảnh để tải lên."] });
            var file = request.Form.Files[0];
            using var stream = file.OpenReadStream();
            var result = await service.UploadPhotoAsync(Owner(http), classId, id, studentId, stream, file.ContentType, file.Length, http.RequestAborted);
            return result.Status == 201 ? Results.Created(result.Value!.DownloadUrl, result.Value) : Respond(result, x => x);
        }).DisableAntiforgery();

        group.MapGet("/{id:guid}/submissions/{studentId:guid}/photos", async (Guid classId, Guid id, Guid studentId, HttpContext http, LearningService service) =>
            Respond(await service.ListPhotosAsync(Owner(http), classId, id, studentId, http.RequestAborted), x => x));

        group.MapDelete("/{id:guid}/submissions/{studentId:guid}/photos/{photoId:guid}", async (Guid classId, Guid id, Guid studentId, Guid photoId, HttpContext http, LearningService service) =>
            Respond(await service.DeletePhotoAsync(Owner(http), classId, id, studentId, photoId, http.RequestAborted), SubmissionView));

        group.MapPut("/{id:guid}/submissions/{studentId:guid}/photos/reorder", async (Guid classId, Guid id, Guid studentId, ReorderPhotosInput input, HttpContext http, LearningService service) =>
            Respond(await service.ReorderPhotosAsync(Owner(http), classId, id, studentId, input, http.RequestAborted), SubmissionView));

        group.MapPost("/{id:guid}/submissions/{studentId:guid}/ai-grade", async (Guid classId, Guid id, Guid studentId, HttpContext http, LearningService service) =>
            Respond(await service.GradeWithAiAsync(Owner(http), classId, id, studentId, http.RequestAborted), SubmissionView));

        group.MapPost("/{id:guid}/submissions/{studentId:guid}/ai-fallback", async (Guid classId, Guid id, Guid studentId, HttpContext http, LearningService service) =>
            Respond(await service.AiFallbackAsync(Owner(http), classId, id, studentId, http.RequestAborted), SubmissionView));

        endpoints.MapGet("/api/learning/photos/{token}", async (string token, HttpContext http, LearningService service) =>
        {
            http.Response.Headers.CacheControl = "private, max-age=900";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            var (stream, contentType, status) = await service.GetPhotoFileAsync(token, http.RequestAborted);
            if (status == 401) return Results.Unauthorized();
            if (status == 404) return Results.NotFound();
            if (status != 200 || stream is null) return Results.StatusCode(status);
            return Results.File(stream, contentType);
        });
    }

    private static IResult Respond<T>(LearningResult<T> result, Func<T, object> view) where T : class => result.Status switch
    {
        404 => Results.NotFound(),
        400 => Results.ValidationProblem(result.Errors!),
        403 => Results.Json(new { message = result.Errors?.Values.FirstOrDefault()?.FirstOrDefault() ?? "Không có quyền thực hiện." }, statusCode: 403),
        409 => Results.Conflict(new { message = "Bài nộp đã thay đổi hoặc đã được xử lý. Tải lại trước khi lưu; nội dung đang nhập được giữ nguyên." }),
        502 => Results.Json(new { message = result.Errors?.Values.FirstOrDefault()?.FirstOrDefault() ?? "Dịch vụ bên ngoài không khả dụng." }, statusCode: 502),
        _ => Results.Ok(view(result.Value!))
    };

    private static object ProgressView(StudentProgress p) => new
    {
        p.StudentId, p.StudentName, from = p.From.ToString("yyyy-MM-dd"), to = p.To.ToString("yyyy-MM-dd"), p.EnoughForTrend, p.Completeness,
        points = p.Points.Select(x => new { x.AssessmentId, x.Title, date = x.Date.ToString("yyyy-MM-dd"), x.Method, x.Status, x.Completion, x.Correctness, x.Plotted, x.Confirmed, x.Reason })
    };

    private static object View(Assessment item) => new
    {
        item.Id,
        item.Title,
        item.Date,
        item.DefaultMethod,
        item.Rubric,
        item.RubricVersion,
        submissions = item.Submissions.OrderBy(x => x.StudentName).ThenBy(x => x.StudentId).Select(SubmissionView)
    };

    private static object SubmissionView(Submission item) => new
    {
        item.StudentId,
        item.StudentName,
        item.Method,
        item.Status,
        item.Completion,
        item.Correctness,
        item.Version,
        item.AiCompletion,
        item.AiCorrectness,
        item.AiConfidence,
        item.AiReasoning,
        item.AiWarning,
        item.AiStatus,
        item.AiProposedAt,
        photos = item.Photos.OrderBy(x => x.SequenceIndex).Select(x => new
        {
            x.Id,
            x.SequenceIndex,
            x.ContentType,
            x.ByteLength,
            x.CreatedAt
        }),
        finalResult = item.History.Where(x => x.Action == "confirmed").OrderByDescending(x => x.Version).FirstOrDefault(),
        history = item.History.OrderBy(x => x.Version)
    };

    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
