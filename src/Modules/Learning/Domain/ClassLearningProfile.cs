using System.Text.Json;
namespace EducationKeeptrack.Learning.Domain;

public sealed record LearningSkill(Guid Id, string Title, bool Taught);
public sealed class ClassLearningProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public Guid ClassId { get; set; }
    public string Textbook { get; set; } = "";
    public string Semester { get; set; } = "";
    public string SkillsJson { get; set; } = "[]";
    public int Version { get; set; }
    public LearningSkill[] ReadSkills() => JsonSerializer.Deserialize<LearningSkill[]>(SkillsJson) ?? [];
}
