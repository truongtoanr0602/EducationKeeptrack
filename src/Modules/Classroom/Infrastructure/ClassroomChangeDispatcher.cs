using EducationKeeptrack.Classroom.Application;
using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace EducationKeeptrack.Classroom.Infrastructure;

public sealed class ClassroomChangeDispatcher(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ClassroomChangeDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ClassroomDatabase>();
                var pending = await db.SessionChanges.AsNoTracking().Where(x => x.DispatchedAt == null).OrderBy(x => x.RecordedAt).ThenBy(x => x.SessionVersion).Select(x => x.Id).Take(100).ToArrayAsync(stoppingToken);
                foreach (var id in pending)
                {
                    try { await DispatchAsync(id, stoppingToken); }
                    catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Classroom event {EventId} remains pending for retry", id); }
                }
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Cannot read Classroom outbox; retrying on the next tick"); }
        }
    }
    private async Task DispatchAsync(Guid id, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomDatabase>();
        var sessionId = await db.SessionChanges.Where(x => x.Id == id).Select(x => (Guid?)x.SessionId).SingleOrDefaultAsync(ct);
        if (sessionId is null) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({sessionId.Value.ToString()}, 2)) AS \"Value\"").SingleAsync(ct);
        if (!locked) return;
        var change = await db.SessionChanges.SingleOrDefaultAsync(x => x.Id == id && x.DispatchedAt == null, ct);
        if (change is null) return;
        // A failed earlier change must not be overtaken by a newer version of this session.
        if (await db.SessionChanges.AnyAsync(x => x.SessionId == change.SessionId && x.SessionVersion < change.SessionVersion && x.DispatchedAt == null, ct)) return;
        foreach (var handler in scope.ServiceProvider.GetServices<IClassroomChangeHandler>())
            await handler.HandleAsync(SessionService.ChangeView(change), ct);
        change.Acknowledge(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
