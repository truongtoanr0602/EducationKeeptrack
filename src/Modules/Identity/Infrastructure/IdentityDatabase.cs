using EducationKeeptrack.Identity.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Identity.Infrastructure;

public sealed class IdentityDatabase(DbContextOptions<IdentityDatabase> options) : IdentityDbContext<Account>(options)
{
    public DbSet<LoginSession> Sessions => Set<LoginSession>();
    public DbSet<EmailToken> EmailTokens => Set<EmailToken>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.HasDefaultSchema("identity");
        model.Entity<LoginSession>().HasKey(x => x.Id);
        model.Entity<LoginSession>().HasIndex(x => x.AccountId);
        model.Entity<LoginSession>().HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
        model.Entity<EmailToken>().ToTable("email_tokens").HasKey(x => x.Id);
        model.Entity<EmailToken>().HasIndex(x => x.TokenHash).IsUnique();
        model.Entity<EmailToken>().HasIndex(x => new { x.AccountId, x.Purpose });
        model.Entity<EmailToken>().Property(x => x.TokenHash).HasMaxLength(64);
        model.Entity<EmailToken>().Property(x => x.NewEmail).HasMaxLength(256);
        model.Entity<EmailToken>().HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId);
    }
}
