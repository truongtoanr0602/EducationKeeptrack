using System.ComponentModel.DataAnnotations;
namespace EducationKeeptrack.Classroom.Domain;

public sealed class Student
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? ParentName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Notes { get; private set; }
    private Student() { }
    public Student(string owner, StudentInput input) { OwnerId = owner; Update(input); }
    public void Update(StudentInput input)
    {
        Name = input.Name.Trim();
        ParentName = Clean(input.ParentName);
        Phone = Clean(input.Phone);
        Email = Clean(input.Email);
        Notes = Clean(input.Notes);
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public record StudentInput(string Name, string? ParentName, string? Phone, string? Email, string? Notes)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 120) errors["name"] = ["Tên học sinh bắt buộc, tối đa 120 ký tự."];
        if (ParentName?.Length > 120) errors["parentName"] = ["Tên phụ huynh tối đa 120 ký tự."];
        if (Phone?.Length > 30 || (!string.IsNullOrWhiteSpace(Phone) && !new PhoneAttribute().IsValid(Phone.Trim()))) errors["phone"] = ["Số điện thoại không hợp lệ, tối đa 30 ký tự."];
        if (Email?.Length > 254 || (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email.Trim()))) errors["email"] = ["Email liên hệ không hợp lệ, tối đa 254 ký tự."];
        if (Notes?.Length > 1000) errors["notes"] = ["Ghi chú tối đa 1000 ký tự."];
        return errors;
    }
}
