using EducationKeeptrack.Contracts;
using EducationKeeptrack.Identity.Application;
using EducationKeeptrack.Identity.Domain;
using EducationKeeptrack.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connection, bool development)
    {
        services.AddDbContext<IdentityDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
        services.AddIdentityCore<Account>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 12;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<IdentityDatabase>().AddDefaultTokenProviders();
        services.AddScoped<ILoginSessions, SessionService>();
        services.AddScoped<IAccountState, AccountStateProvider>();
        services.AddAuthentication("session").AddCookie("session", o =>
        {
            o.Cookie.Name = "ekt.session";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            o.Cookie.SecurePolicy = development ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            o.ExpireTimeSpan = TimeSpan.FromHours(24);
            o.SlidingExpiration = false;
            o.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; },
                OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; },
                OnValidatePrincipal = async c =>
                {
                    var sessions = c.HttpContext.RequestServices.GetRequiredService<ILoginSessions>();
                    if (!await sessions.ValidateAsync(c.Principal!, false, c.HttpContext.RequestAborted)) c.RejectPrincipal();
                }
            };
        });
        services.AddAuthorization();
        return services;
    }
}
