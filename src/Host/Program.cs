using EducationKeeptrack.Identity;
using EducationKeeptrack.Identity.Endpoints;
using EducationKeeptrack.Identity.Infrastructure;
using EducationKeeptrack.Identity.Application;
using Microsoft.EntityFrameworkCore;
using EducationKeeptrack.Classroom;
using EducationKeeptrack.Classroom.Infrastructure;
using EducationKeeptrack.ServiceAccess;
using EducationKeeptrack.ServiceAccess.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Database outside source control.");
builder.Services.AddSingleton(TimeProvider.System);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32768);
builder.Services.AddClassroomModule(connection);
builder.Services.AddServiceAccessModule(connection);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    if (builder.Configuration["ReverseProxy:KnownProxy"] is { } proxy) o.KnownProxies.Add(IPAddress.Parse(proxy));
});
builder.Services.AddIdentityModule(connection, builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "ekt.csrf";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
var keys = builder.Configuration["DataProtection:KeyPath"];
if (keys is not null) builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));
var app = builder.Build();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseExceptionHandler(handler => handler.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { message = "Không thể hoàn tất yêu cầu. Vui lòng thử lại." });
    }));
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDatabase>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ClassroomDatabase>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ServiceAccessDatabase>().Database.MigrateAsync();
    return;
}
if (args.Contains("--provision-teacher"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo provisioning is Development-only.");
    using var scope = app.Services.CreateScope();
    var until = DateTimeOffset.Parse(builder.Configuration["Provision:Until"] ?? throw new InvalidOperationException("Set explicit Provision__Until UTC timestamp."));
    if (until <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Demo grant must end in the future.");
    var accountId = await ProvisionTeacher.RunAsync(scope.ServiceProvider,
        builder.Configuration["Provision:Email"] ?? throw new InvalidOperationException("Set Provision__Email."),
        builder.Configuration["Provision:Password"] ?? throw new InvalidOperationException("Set Provision__Password."));
    var grants = scope.ServiceProvider.GetRequiredService<ServiceAccessDatabase>();
    grants.DevelopmentGrants.Add(new DevelopmentGrant { AccountId = accountId, Approved = true, Until = until.ToUniversalTime() });
    await grants.SaveChangesAsync();
    return;
}
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Phiên biểu mẫu đã hết hạn. Vui lòng thử lại." }); return; }
        if (context.User.Identity?.IsAuthenticated == true && context.Request.Path != "/api/auth/logout")
            await context.RequestServices.GetRequiredService<ILoginSessions>().ValidateAsync(context.User, true, context.RequestAborted);
    }
    await next();
});
app.MapAuth();
app.MapClassroom();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program;
