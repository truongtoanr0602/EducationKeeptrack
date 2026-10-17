using EducationKeeptrack.Billing.Application;
using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace EducationKeeptrack.Billing.Infrastructure;

public sealed class BillingWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<BillingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var offset = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BillingDatabase>();
                var refresh = await db.Ledgers.AsNoTracking().Where(x => x.NeedsRefresh).Select(x => new { x.OwnerId, x.ClassId }).ToArrayAsync(stoppingToken);
                foreach (var item in refresh)
                {
                    try { using var work = scopes.CreateScope(); await work.ServiceProvider.GetRequiredService<BillingService>().RefreshAsync(item.OwnerId, item.ClassId, stoppingToken); }
                    catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Billing class {ClassId} refresh remains pending", item.ClassId); }
                }
                var heads = db.Outbox.AsNoTracking().Where(x => x.DispatchedAt == null && !db.Outbox.Any(y => y.ClassId == x.ClassId && y.Version < x.Version && y.DispatchedAt == null)).OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).Select(x => x.Id);
                var pending = await heads.Skip(offset).Take(100).ToArrayAsync(stoppingToken);
                if (pending.Length == 0 && offset != 0) { offset = 0; pending = await heads.Take(100).ToArrayAsync(stoppingToken); }
                offset += pending.Length;
                foreach (var id in pending)
                {
                    try { Guid? next = id; for (var count = 0; next is { } current && count < 100; count++) next = await DispatchAsync(current, stoppingToken); }
                    catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Billing event starting at {EventId} remains pending", id); }
                }
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Billing worker will retry on the next tick"); }
        }
    }
    private async Task<Guid?> DispatchAsync(Guid id, CancellationToken ct)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BillingDatabase>();
        var classId = await db.Outbox.Where(x => x.Id == id).Select(x => (Guid?)x.ClassId).SingleOrDefaultAsync(ct);
        if (classId is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({classId.Value.ToString()}, 4)) AS \"Value\"").SingleAsync(ct)) return null;
        var change = await db.Outbox.SingleOrDefaultAsync(x => x.Id == id && x.DispatchedAt == null, ct);
        if (change is null || await db.Outbox.AnyAsync(x => x.ClassId == change.ClassId && x.Version < change.Version && x.DispatchedAt == null, ct)) return null;
        foreach (var handler in scope.ServiceProvider.GetServices<ITuitionChangeHandler>()) await handler.HandleAsync(change.View(), ct);
        change.Acknowledge(clock.GetUtcNow()); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return await db.Outbox.Where(x => x.ClassId == change.ClassId && x.DispatchedAt == null).OrderBy(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    }
}
