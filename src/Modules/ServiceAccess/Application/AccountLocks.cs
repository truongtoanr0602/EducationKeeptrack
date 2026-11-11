using EducationKeeptrack.ServiceAccess.Domain;
using EducationKeeptrack.ServiceAccess.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.ServiceAccess.Application;

public static class AccountLocks
{
    // Must run inside a transaction: creates the per-teacher row if needed and holds its row lock until commit.
    public static async Task<ServiceAccount> LockAsync(this ServiceAccessDatabase db, string accountId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO service_access.accounts (\"AccountId\", \"AiEnabled\") VALUES ({accountId}, false) ON CONFLICT DO NOTHING", ct);
        return await db.Accounts.FromSqlInterpolated(
            $"SELECT *, xmin FROM service_access.accounts WHERE \"AccountId\" = {accountId} FOR UPDATE").SingleAsync(ct);
    }

    // Renewal rule: continue from the end of the current/future periods, otherwise start now.
    public static async Task<DateTimeOffset> NextStartAsync(this ServiceAccessDatabase db, string accountId, DateTimeOffset now, CancellationToken ct)
    {
        var latestEnd = await db.Periods.Where(x => x.AccountId == accountId && x.EndsAt > now).MaxAsync(x => (DateTimeOffset?)x.EndsAt, ct);
        return Moment.Of(latestEnd ?? now);
    }
}
