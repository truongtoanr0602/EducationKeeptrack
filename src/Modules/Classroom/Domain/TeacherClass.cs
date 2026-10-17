namespace EducationKeeptrack.Classroom.Domain;

public sealed class TeacherClass
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public string? Description { get; private set; }
    private TeacherClass() { }
    public TeacherClass(string ownerId, ClassInput input)
    {
        OwnerId = ownerId;
        Update(input);
    }
    public void Update(ClassInput input)
    {
        Name = input.Name.Trim();
        Subject = input.Subject.Trim();
        Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
    }
}

public record ClassInput(string Name, string Subject, string? Description)
{
    public const int NameLimit = 120, SubjectLimit = 80, DescriptionLimit = 500;
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > NameLimit) errors["name"] = ["Tên lớp bắt buộc, tối đa 120 ký tự."];
        if (string.IsNullOrWhiteSpace(Subject) || Subject.Length > SubjectLimit) errors["subject"] = ["Môn học bắt buộc, tối đa 80 ký tự."];
        if (Description?.Length > DescriptionLimit) errors["description"] = ["Mô tả tối đa 500 ký tự."];
        return errors;
    }
}
