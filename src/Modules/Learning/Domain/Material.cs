namespace EducationKeeptrack.Learning.Domain;

// Request shapes follow ui/src/Personalization.tsx (MaterialsTab).
public record MaterialInput(Guid SkillId, string? Title, string? Content, int Minutes, string? Difficulty, int Version);
public record MaterialVersionInput(int Version);

public static class MaterialStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";
}

/// <summary>
/// A practice material of the teacher's own store for one skill. New and edited materials are drafts;
/// only approved materials may be placed into review plans. Every change keeps a revision so a plan
/// that cited a version can still show what it cited.
/// </summary>
public sealed class Material
{
    public static readonly string[] Difficulties = ["basic", "standard", "advanced"];
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid SkillId { get; private set; }
    public string Title { get; private set; } = "";
    public string Content { get; private set; } = "";
    public int Minutes { get; private set; }
    public string Difficulty { get; private set; } = "standard";
    public string Status { get; private set; } = MaterialStatus.Draft;
    public int Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public List<MaterialRevision> Revisions { get; private set; } = [];
    private Material() { }
    public Material(string owner, Guid classId, MaterialInput input, DateTimeOffset now)
    {
        OwnerId = owner; ClassId = classId; CreatedAt = UpdatedAt = now;
        Assign(input);
    }

    public static Dictionary<string, string[]> Validate(MaterialInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200) errors["title"] = ["Nhập tên học liệu (tối đa 200 ký tự)."];
        if (string.IsNullOrWhiteSpace(input.Content) || input.Content.Trim().Length > 2000) errors["content"] = ["Nhập nội dung hoặc đường dẫn (tối đa 2000 ký tự)."];
        if (input.Minutes is < 1 or > 240) errors["minutes"] = ["Thời lượng từ 1 đến 240 phút."];
        if (input.Difficulty is null || !Difficulties.Contains(input.Difficulty)) errors["difficulty"] = ["Chọn độ khó: cơ bản, vừa hoặc nâng cao."];
        return errors;
    }

    /// <summary>Applies an edit; returns the revision to store, or null when nothing changed.</summary>
    public MaterialRevision? Edit(MaterialInput input, string actor, DateTimeOffset now)
    {
        var title = input.Title!.Trim(); var content = input.Content!.Trim();
        if (input.SkillId == SkillId && title == Title && content == Content && input.Minutes == Minutes && input.Difficulty == Difficulty) return null;
        Assign(input);
        Status = MaterialStatus.Draft; ApprovedAt = null; // content changed, so it needs approval again
        return Touch("edited", actor, now);
    }

    public MaterialRevision Approve(string actor, DateTimeOffset now)
    {
        Status = MaterialStatus.Approved; ApprovedAt = now;
        return Touch("approved", actor, now);
    }

    public MaterialRevision Created(string actor, DateTimeOffset now) => new(this, "created", actor, now);

    private MaterialRevision Touch(string action, string actor, DateTimeOffset now)
    {
        Version++; UpdatedAt = now;
        return new(this, action, actor, now);
    }
    private void Assign(MaterialInput input)
    {
        SkillId = input.SkillId; Title = input.Title!.Trim(); Content = input.Content!.Trim(); Minutes = input.Minutes; Difficulty = input.Difficulty!;
    }
}

/// <summary>Snapshot of a material at one version.</summary>
public sealed class MaterialRevision
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MaterialId { get; private set; }
    public int Version { get; private set; }
    public string Action { get; private set; } = "";
    public string ActorId { get; private set; } = "";
    public DateTimeOffset At { get; private set; }
    public Guid SkillId { get; private set; }
    public string Title { get; private set; } = "";
    public string Content { get; private set; } = "";
    public int Minutes { get; private set; }
    public string Difficulty { get; private set; } = "";
    public string Status { get; private set; } = "";
    private MaterialRevision() { }
    public MaterialRevision(Material material, string action, string actor, DateTimeOffset at)
    {
        MaterialId = material.Id; Version = material.Version; Action = action; ActorId = actor; At = at;
        SkillId = material.SkillId; Title = material.Title; Content = material.Content; Minutes = material.Minutes;
        Difficulty = material.Difficulty; Status = material.Status;
    }
}
