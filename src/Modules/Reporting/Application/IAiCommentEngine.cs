using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Reporting.Application;

public record AiCommentProposal(string Comment, string Roadmap);

public interface IAiCommentEngine
{
    Task<AiCommentProposal> GenerateCommentAsync(
        string studentName,
        string className,
        string subject,
        DateOnly month,
        IReadOnlyList<ReportSession> sessions,
        IReadOnlyList<ReportAssessment> assessments,
        CancellationToken ct);
}
