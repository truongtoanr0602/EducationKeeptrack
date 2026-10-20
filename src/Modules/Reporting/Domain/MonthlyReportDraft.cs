using EducationKeeptrack.Contracts;

namespace EducationKeeptrack.Reporting.Domain;

public record DraftInput(string? DisplayName, string? ParentDisplayName, string? ContactDisplay, string? Comment, string? Roadmap, string? Emoji, long Version)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (DisplayName?.Length > 160 || ParentDisplayName?.Length > 160) errors["displayName"] = ["Tên hiển thị tối đa 160 ký tự."];
        if (ContactDisplay?.Length > 500) errors["contactDisplay"] = ["Thông tin liên hệ tối đa 500 ký tự."];
        if (Comment?.Length > 10000 || Roadmap?.Length > 10000) errors["content"] = ["Nhận xét và lộ trình mỗi phần tối đa 10.000 ký tự."];
        if (Emoji is not ("none" or "🙂" or "⭐" or "📘")) errors["emoji"] = ["Emoji không được hỗ trợ."];
        if (Version < 0) errors["version"] = ["Phiên bản không hợp lệ."];
        return errors;
    }
}
public sealed class MonthlyReportDraft
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateOnly Month { get; private set; }
    public string? DisplayName { get; private set; }
    public string? ParentDisplayName { get; private set; }
    public string? ContactDisplay { get; private set; }
    public string Comment { get; private set; } = "";
    public string Roadmap { get; private set; } = "";
    public string Emoji { get; private set; } = "none";
    public long Version { get; private set; }
    public DateTimeOffset? SavedAt { get; private set; }
    private MonthlyReportDraft() { }
    public MonthlyReportDraft(ReportScope scope) { OwnerId = scope.OwnerId; ClassId = scope.ClassId; StudentId = scope.StudentId; Month = scope.Month; }
    public void Save(DraftInput input, DateTimeOffset now)
    {
        DisplayName = Clean(input.DisplayName); ParentDisplayName = Clean(input.ParentDisplayName); ContactDisplay = Clean(input.ContactDisplay);
        Comment = input.Comment?.Trim() ?? ""; Roadmap = input.Roadmap?.Trim() ?? ""; Emoji = input.Emoji!; Version++; SavedAt = now;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
