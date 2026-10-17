using EducationKeeptrack.Contracts;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace EducationKeeptrack.ServiceAccess;

public static class ServiceAccessModule
{
    public static IServiceCollection AddServiceAccessModule(this IServiceCollection services, string connection)
    {
        services.AddDbContext<ServiceAccessDatabase>(o => o.UseNpgsql(connection, p => p.MigrationsHistoryTable("__EFMigrationsHistory", "service_access")));
        services.AddScoped<IAccountAccess, ProvisionedAccountAccess>();
        return services;
    }
}
