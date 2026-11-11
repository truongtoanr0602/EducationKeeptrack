using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Application;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.ServiceAccess;

public static class ServiceAccessModule
{
    public static IServiceCollection AddServiceAccessModule(this IServiceCollection services, string connection)
    {
        services.AddDbContext<ServiceAccessDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "service_access")));
        services.AddScoped<IAccountAccess, AccessPolicy>();
        services.AddScoped<ServiceDesk>();
        services.AddScoped<ServiceAdministration>();
        services.AddScoped<PeriodAdministration>();
        services.AddScoped<AiQuotaLedger>();
        services.AddScoped<IAiQuota>(p => p.GetRequiredService<AiQuotaLedger>());
        return services;
    }
}
