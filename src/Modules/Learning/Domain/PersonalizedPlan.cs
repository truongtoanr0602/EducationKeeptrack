namespace EducationKeeptrack.Learning.Domain;

public sealed record PlanPriority(Guid StudentId, Guid SkillId);
public sealed record PlanActivity(string Title, int Minutes);
public sealed class PersonalizedPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public Guid ClassId { get; set; }
    public string Title { get; set; } = "";
    public DateOnly Date { get; set; }
    public int Minutes { get; set; }
    public string Content { get; set; } = "";
    public string PrioritiesJson { get; set; } = "[]";
    public string ActivitiesJson { get; set; } = "[]";
    public string SnapshotJson { get; set; } = "{}";
    public string HistoryJson { get; set; } = "[]";
    public string Status { get; set; } = "draft";
    public int Version { get; set; } = 1;
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
