namespace EducationKeeptrack.Contracts;

public record AccountAccess(bool CanRead, bool CanWrite, bool CanUseAi, string Status);

// ServiceAccess owns this policy. T01 uses controlled development grants;
// approval/period/quota workflows extend that module in subsequent slices.
public interface IAccountAccess
{
    Task<AccountAccess> GetAsync(string accountId, CancellationToken cancellationToken);
}

public record AccountState(bool Exists, bool EmailConfirmed, bool IsBlocked);
public interface IAccountState
{
    Task<AccountState> GetAsync(string accountId, CancellationToken cancellationToken);
}
