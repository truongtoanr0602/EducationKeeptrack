using EducationKeeptrack.Billing.Infrastructure;
using EducationKeeptrack.Classroom.Infrastructure;
using EducationKeeptrack.Identity.Infrastructure;
using EducationKeeptrack.Learning.Infrastructure;
using EducationKeeptrack.Reporting.Infrastructure;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ApiTests;

// Every module's model must match its migration snapshot. EF refuses to migrate otherwise (PendingModelChangesWarning),
// which turns every API test and the server start-up red with a message that does not say which module is at fault.
// This names the module. It compares the model with the snapshot only; no database is contacted.
public sealed class ModelSnapshotTests
{
    public static TheoryData<Type> Contexts =>
    [
        typeof(IdentityDatabase), typeof(ClassroomDatabase), typeof(BillingDatabase),
        typeof(LearningDatabase), typeof(ReportingDatabase), typeof(ServiceAccessDatabase)
    ];

    [Theory]
    [MemberData(nameof(Contexts))]
    public void The_model_matches_the_migration_snapshot(Type contextType)
    {
        var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(contextType);
        var builder = (DbContextOptionsBuilder)Activator.CreateInstance(builderType)!;
        builder.UseNpgsql("Host=127.0.0.1;Database=snapshot_check_unused;Username=unused;Password=unused");
        using var context = (DbContext)Activator.CreateInstance(contextType, builder.Options)!;
        Assert.False(context.Database.HasPendingModelChanges(),
            $"{contextType.Name} has model changes without a migration. Run: dotnet ef migrations add <Name> " +
            "--project src/Modules/<Module> --startup-project src/Host --context " + contextType.Name);
    }
}
