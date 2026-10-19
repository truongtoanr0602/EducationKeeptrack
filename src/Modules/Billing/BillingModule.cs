using System.Security.Claims;
using EducationKeeptrack.Billing.Application;
using EducationKeeptrack.Billing.Domain;
using EducationKeeptrack.Billing.Infrastructure;
using EducationKeeptrack.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Billing;

public static class BillingModule
{
    public static IServiceCollection AddBillingModule(this IServiceCollection services, string connection)
    {
        services.AddDbContext<BillingDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "billing")));
        services.AddScoped<IBillingRepository, BillingRepository>(); services.AddScoped<BillingService>();
        services.AddScoped<PaymentService>(); services.AddScoped<AdjustmentService>();
        services.AddScoped<PaymentQrService>();
        services.AddHttpClient<IVietQrGenerator, VietQrGenerator>(client => client.Timeout = TimeSpan.FromSeconds(8));
        services.AddScoped<IClassroomChangeHandler, BillingClassroomHandler>(); services.AddHostedService<BillingWorker>();
        return services;
    }
    public static void MapBilling(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/billing/classes/{classId:guid}").RequireAuthorization(policy => policy.RequireRole("Teacher"));
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context)
                : Results.Json(new { message = access.Status == "expired" ? "Quyền sử dụng đã hết hạn. Bạn chỉ có thể xem học phí đã lưu." : "Tài khoản hiện không được truy cập học phí.", status = access.Status }, statusCode: 403);
        });
        group.MapGet("/rates", async (Guid classId, HttpContext http, BillingService service) => Respond(await service.RatesAsync(Owner(http), classId, http.RequestAborted)));
        group.MapPost("/rates", async (Guid classId, RateInput input, HttpContext http, BillingService service) =>
        {
            var result = await service.AddRateAsync(Owner(http), classId, input, http.RequestAborted);
            return result.Failure is null ? Results.Created($"/api/billing/classes/{classId}/rates", result.Value) : Respond(result);
        });
        group.MapPost("/adjustments", async (Guid classId, AdjustmentInput input, HttpContext http, AdjustmentService service) =>
        {
            var result = await service.AddAsync(Owner(http), classId, input, http.RequestAborted);
            return result.Failure is null ? Results.Created($"/api/billing/classes/{classId}/tuition?month={input.Month}", result.Value) : Respond(result);
        });
        group.MapGet("/tuition", async (Guid classId, string month, HttpContext http, BillingService service) => Respond(await service.ReportAsync(Owner(http), classId, month, http.RequestAborted)));
        group.MapGet("/payments", async (Guid classId, string month, HttpContext http, PaymentService service) => Respond(await service.ReportAsync(Owner(http), classId, month, http.RequestAborted)));
        group.MapPost("/payments/{receiptId:guid}/corrections", async (Guid classId, Guid receiptId, ReceiptCorrectionInput input, HttpContext http, PaymentService service) =>
            Respond(await service.CorrectAsync(Owner(http), classId, receiptId, input, http.RequestAborted)));
        group.MapPost("/transfer-request", async (Guid classId, TransferInput input, HttpContext http, PaymentQrService service) => Respond(await service.CreateAsync(Owner(http), classId, input, http.RequestAborted)));
        var recipient = endpoints.MapGroup("/api/billing/recipient").RequireAuthorization(policy => policy.RequireRole("Teacher"));
        recipient.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var access = await http.RequestServices.GetRequiredService<IAccountAccess>().GetAsync(Owner(http), http.RequestAborted);
            return (HttpMethods.IsGet(http.Request.Method) ? access.CanRead : access.CanWrite) ? await next(context)
                : Results.Json(new { message = "Tài khoản hiện không được thay đổi thông tin nhận tiền." }, statusCode: 403);
        });
        recipient.MapGet("", async (HttpContext http, PaymentQrService service) => Respond(await service.RecipientAsync(Owner(http), http.RequestAborted)));
        recipient.MapPost("", async (RecipientInput input, HttpContext http, PaymentQrService service) => Respond(await service.SaveAsync(Owner(http), input, http.RequestAborted)))
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1500000));
        group.MapPost("/payments", async (Guid classId, ReceiptInput input, HttpContext http, PaymentService service) =>
        {
            var result = await service.RecordAsync(Owner(http), classId, input, http.RequestAborted);
            return result.Failure is null ? Results.Created($"/api/billing/classes/{classId}/payments", result.Value) : Respond(result);
        });
    }
    private static string Owner(HttpContext http) => http.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IResult Respond<T>(BillingResult<T> result) where T : class => result.Failure switch
    {
        BillingFailure.NotFound => Results.NotFound(), BillingFailure.Invalid => Results.ValidationProblem(result.Errors!),
        BillingFailure.Conflict => Results.Conflict(new { message = result.Message }), _ => Results.Ok(result.Value)
    };
}
