namespace EducationKeeptrack.Learning.Application;

// Provider integration remains disabled until a provider and a dedicated quota policy exist.
public interface IPlanGenerator
{
    Task<PlanInput> GenerateAsync(string snapshotJson, CancellationToken cancellationToken);
}
