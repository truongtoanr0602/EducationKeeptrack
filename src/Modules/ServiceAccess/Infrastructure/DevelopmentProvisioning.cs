using EducationKeeptrack.ServiceAccess.Domain;
namespace EducationKeeptrack.ServiceAccess.Infrastructure;

// Development/test fixture only: grants a period without the payment workflow.
public static class DevelopmentProvisioning
{
    public static async Task GrantAsync(ServiceAccessDatabase db, string accountId, DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (await db.Accounts.FindAsync(accountId) is null) db.Accounts.Add(new ServiceAccount { AccountId = accountId });
        db.Periods.Add(new ServicePeriod
        {
            Id = Guid.NewGuid(), AccountId = accountId, StartsAt = Moment.Of(startsAt), EndsAt = Moment.Of(endsAt),
            Label = "Cấp phát triển", CreatedBy = "development-provisioning", CreatedAt = startsAt, Reason = "Dữ liệu phát triển/kiểm thử"
        });
        await db.SaveChangesAsync();
    }
}
