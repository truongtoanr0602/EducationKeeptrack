namespace EducationKeeptrack.Learning.Application;

public record AiGradingProposal(int Completion, int Correctness, double Confidence, string Reasoning, string? Warning);

public interface IAiGradingEngine
{
    Task<AiGradingProposal> GradeAsync(string rubric, IReadOnlyList<byte[]> photos, CancellationToken ct);
}
