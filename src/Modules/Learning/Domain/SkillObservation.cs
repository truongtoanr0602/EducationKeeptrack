namespace EducationKeeptrack.Learning.Domain;

public sealed class SkillObservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public Guid SkillId { get; set; }
    public DateOnly Date { get; set; }
    public string Level { get; set; } = "";
    public string? Note { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
