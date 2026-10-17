using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class LearningResultDispatcher(IServiceScopeFactory scopes, TimeProvider clock, ILogger<LearningResultDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var offset = 0;
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LearningDatabase>();
                var heads = db.ResultRevisions.AsNoTracking().Where(x => x.Action == "confirmed" && x.DispatchedAt == null
                    && !db.ResultRevisions.Any(earlier => earlier.SubmissionId == x.SubmissionId && earlier.Action == "confirmed" && earlier.Version < x.Version && earlier.DispatchedAt == null))
                    .OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).Select(x => x.Id);
                var pending = await heads.Skip(offset).Take(100).ToArrayAsync(stoppingToken);
                if (pending.Length == 0 && offset != 0) { offset = 0; pending = await heads.Take(100).ToArrayAsync(stoppingToken); }
                offset += pending.Length;
                foreach (var id in pending)
                {
                    try { await DispatchAsync(id, stoppingToken); }
                    catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Learning confirmation {EventId} remains pending", id); }
                }
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(e, "Cannot read Learning outbox; retrying"); }
        }
    }
    private async Task DispatchAsync(Guid id, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDatabase>();
        var submissionId = await db.ResultRevisions.Where(x => x.Id == id).Select(x => x.SubmissionId).SingleAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var locked = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({submissionId.ToString()}, 3)) AS \"Value\"").SingleAsync(ct);
        if (!locked) return;
        var revision = await db.ResultRevisions.SingleOrDefaultAsync(x => x.Id == id && x.DispatchedAt == null, ct);
        if (revision is null || await db.ResultRevisions.AnyAsync(x => x.SubmissionId == submissionId && x.Action == "confirmed" && x.Version < revision.Version && x.DispatchedAt == null, ct)) return;
        var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == submissionId, ct);
        var assessment = await db.Assessments.AsNoTracking().SingleAsync(x => x.Id == submission.AssessmentId, ct);
        var change = new LearningResultChange(revision.Id, assessment.OwnerId, assessment.ClassId, assessment.Id, submission.StudentId,
            revision.Version, revision.Status, revision.Completion, revision.Correctness, revision.IncludedInStatistics, revision.RecordedAt);
        foreach (var handler in scope.ServiceProvider.GetServices<ILearningResultChangeHandler>()) await handler.HandleAsync(change, ct);
        revision.Acknowledge(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
