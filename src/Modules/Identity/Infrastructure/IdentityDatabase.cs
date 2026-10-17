using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class IdentityDatabase(DbContextOptions<IdentityDatabase> options) : IdentityDbContext<Account>(options)
{
    public DbSet<LoginSession> Sessions => Set<LoginSession>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("identity");
        model.Entity<LoginSession>().HasKey(x => x.Id);
        model.Entity<LoginSession>().HasIndex(x => x.AccountId);
        model.Entity<LoginSession>().HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
    }
}
