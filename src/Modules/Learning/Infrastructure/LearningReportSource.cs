using System.Data;
using EducationKeeptrack.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class LearningReportSource(LearningDatabase db, IClassroomReportSource classroom) : ILearningReportSource
{
    public async Task<LearningReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct)
    {
        if (await classroom.ReadAsync(scope, ct) is null) return null;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var end = scope.Month.AddMonths(1);
        var assessments = await db.Assessments.AsNoTracking().Include(x => x.Submissions.Where(s => s.StudentId == scope.StudentId)).ThenInclude(x => x.History)
            .Where(x => x.OwnerId == scope.OwnerId && x.ClassId == scope.ClassId && x.Date >= scope.Month && x.Date < end)
            .OrderBy(x => x.Date).ThenBy(x => x.Id).ToArrayAsync(ct);
        var rows = assessments.Select(a => { var sub = a.Submissions.SingleOrDefault(); var final = sub?.History.Where(x => x.Action == "confirmed").OrderByDescending(x => x.Version).FirstOrDefault();
            return new ReportAssessment(a.Id, a.Date, a.Title, final?.Status ?? "unassessed", final?.Completion, final?.Correctness, final is not null); }).ToArray();
        var stamp = ReportStamp.Of(assessments.Select(a => new { a.Id, a.Title, a.Date, Submissions = a.Submissions.Select(s => new { s.Id, s.Version }).OrderBy(x => x.Id).ToArray() }).ToArray());
        await tx.CommitAsync(ct); return new(stamp, rows);
    }
}
