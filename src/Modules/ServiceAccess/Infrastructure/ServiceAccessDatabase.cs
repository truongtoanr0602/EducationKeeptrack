using EducationKeeptrack.ServiceAccess.Domain;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Infrastructure;

public sealed class ServiceAccessDatabase(DbContextOptions<ServiceAccessDatabase> options) : DbContext(options)
{
    public DbSet<DevelopmentGrant> DevelopmentGrants => Set<DevelopmentGrant>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("service_access");
        model.Entity<DevelopmentGrant>().HasKey(x => x.AccountId);
        model.Entity<DevelopmentGrant>().Property(x => x.AccountId).HasMaxLength(450);
    }
}
