namespace EducationKeeptrack.ServiceAccess.Domain;

// T01 controlled provisioning only; not a purchasable period/plan/approval workflow.
public sealed class DevelopmentGrant
{
    public required string AccountId { get; set; }
    public bool Approved { get; set; }
    public DateTimeOffset Until { get; set; }
}
