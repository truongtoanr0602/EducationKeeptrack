using System.Text.Json;
using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
namespace EducationKeeptrack.Learning.Application;

public sealed record ProfileInput(string? Textbook, string? Semester, LearningSkill[]? Skills, int Version);
public sealed record ObservationInput(Guid StudentId, Guid SkillId, DateOnly Date, string? Level, string? Note, Guid RequestId);
public sealed record EvidenceInput(Guid StudentId, Guid SkillId, DateOnly Date, string? Title, int Total, int Correct, bool Confirmed, Guid RequestId);
public sealed record PersonalResult(int Status, object? Value = null, string? Message = null);

public sealed class PersonalizedLearningService(PersonalizedLearningRepository repository, IClassroomLearningSource classroom, TimeProvider clock)
{
    public DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
    public Task<LearningRosterSource?> Roster(string owner, Guid classId, DateOnly date, CancellationToken ct) => classroom.ReadRosterAsync(owner, classId, date, ct);
    private static object View(ClassLearningProfile p) => new { p.Textbook, p.Semester, skills = p.ReadSkills(), p.Version };
    public async Task<PersonalResult> ReadProfile(string owner, Guid classId, CancellationToken ct)
    {
        if (await Roster(owner, classId, Today, ct) is null) return new(404);
        var p = await repository.Profile(owner, classId, ct);
        return new(200, p is null ? new { textbook = "", semester = "Toán 2 - HK1", skills = Array.Empty<LearningSkill>(), version = 0 } : View(p));
    }
    public async Task<PersonalResult> SaveProfile(string owner, Guid classId, ProfileInput input, CancellationToken ct)
    {
        if (await Roster(owner, classId, Today, ct) is null) return new(404);
        if (string.IsNullOrWhiteSpace(input.Textbook) || input.Textbook.Length > 120 || string.IsNullOrWhiteSpace(input.Semester) || input.Semester.Length > 120
            || input.Skills is not { Length: > 0 and <= 100 } || input.Skills.Any(x => x is null || x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 160)
            || input.Skills.Select(x => x.Id).Distinct().Count() != input.Skills.Length) return new(400, Message: "Nhập bộ sách, học kỳ và 1–100 kỹ năng có mã riêng, tên tối đa 160 ký tự.");
        var p = await repository.Profile(owner, classId, ct);
        if ((p?.Version ?? 0) != input.Version) return new(409);
        if (p is not null && p.ReadSkills().Any(x => !input.Skills.Any(s => s.Id == x.Id))) return new(400, Message: "Giữ kỹ năng đã lưu để bảo toàn lịch sử; có thể bỏ đánh dấu đã dạy.");
        if (p is null) { p = new() { OwnerId = owner, ClassId = classId }; repository.Add(p); }
        p.Textbook = input.Textbook.Trim(); p.Semester = input.Semester.Trim(); p.SkillsJson = JsonSerializer.Serialize(input.Skills.Select(x => x with { Title = x.Title.Trim() })); p.Version++;
        return await repository.Save(ct) ? new(200, View(p)) : new(409);
    }
    private async Task<PersonalResult?> ValidateTarget(string owner, Guid classId, Guid studentId, Guid skillId, DateOnly date, CancellationToken ct)
    {
        if (date == default || date > Today) return new(400, Message: "Chọn ngày đã diễn ra, không ở tương lai.");
        var roster = await Roster(owner, classId, date, ct);
        if (roster is null || !roster.Students.Any(x => x.Id == studentId)) return new(404);
        var profile = await repository.Profile(owner, classId, ct);
        var skill = profile?.ReadSkills().SingleOrDefault(x => x.Id == skillId);
        return skill is null || !skill.Taught ? new(400, Message: "Chọn kỹ năng đã được đánh dấu đã dạy của lớp.") : null;
    }
    public async Task<PersonalResult> Observe(string owner, Guid classId, ObservationInput input, CancellationToken ct)
    {
        var invalid = await ValidateTarget(owner, classId, input.StudentId, input.SkillId, input.Date, ct);
        if (invalid is not null) return invalid;
        if (input.Level is not ("red" or "amber" or "green") || input.Note?.Length > 500 || input.RequestId == Guid.Empty) return new(400, Message: "Chọn nhận xét hợp lệ, ghi chú tối đa 500 ký tự và mã yêu cầu.");
        var existing = (await repository.Observations(owner, classId, ct)).SingleOrDefault(x => x.RequestId == input.RequestId);
        if (existing is not null) return Match(existing, input) ? new(201, existing) : new(409);
        var item = new SkillObservation { OwnerId = owner, ClassId = classId, StudentId = input.StudentId, SkillId = input.SkillId, Date = input.Date, Level = input.Level, Note = input.Note?.Trim(), RequestId = input.RequestId, RecordedAt = clock.GetUtcNow() };
        repository.Add(item);
        if (await repository.Save(ct)) return new(201, item);
        existing = (await repository.Observations(owner, classId, ct)).SingleOrDefault(x => x.RequestId == input.RequestId);
        return existing is not null && Match(existing, input) ? new(201, existing) : new(409);
    }
    private static bool Match(SkillObservation x, ObservationInput i) => x.StudentId == i.StudentId && x.SkillId == i.SkillId && x.Date == i.Date && x.Level == i.Level && x.Note == i.Note?.Trim();
    private static bool Match(SkillEvidence x, EvidenceInput i) => x.StudentId == i.StudentId && x.SkillId == i.SkillId && x.Date == i.Date && x.Title == i.Title?.Trim() && x.Total == i.Total && x.Correct == i.Correct && x.Confirmed == i.Confirmed;
    public async Task<PersonalResult> RecordEvidence(string owner, Guid classId, EvidenceInput input, CancellationToken ct)
    {
        var invalid = await ValidateTarget(owner, classId, input.StudentId, input.SkillId, input.Date, ct);
        if (invalid is not null) return invalid;
        if (input.Total is < 1 or > 1000 || input.Correct < 0 || input.Correct > input.Total || string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 160 || input.RequestId == Guid.Empty) return new(400, Message: "Nhập tên bài, tổng 1–1000 nhiệm vụ và số đúng không vượt tổng.");
        var existing = (await repository.Evidence(owner, classId, ct)).SingleOrDefault(x => x.RequestId == input.RequestId);
        if (existing is not null) return Match(existing, input) ? new(201, existing) : new(409);
        var item = new SkillEvidence { OwnerId = owner, ClassId = classId, StudentId = input.StudentId, SkillId = input.SkillId, Date = input.Date, Title = input.Title.Trim(), Total = input.Total, Correct = input.Correct, Confirmed = input.Confirmed, RequestId = input.RequestId, RecordedAt = clock.GetUtcNow() };
        repository.Add(item);
        if (await repository.Save(ct)) return new(201, item);
        existing = (await repository.Evidence(owner, classId, ct)).SingleOrDefault(x => x.RequestId == input.RequestId);
        return existing is not null && Match(existing, input) ? new(201, existing) : new(409);
    }
    public async Task<PersonalResult> Progress(string owner, Guid classId, DateOnly date, CancellationToken ct)
    {
        if (date == default) return new(400, Message: "Chọn ngày theo dõi.");
        var roster = await Roster(owner, classId, date, ct);
        if (roster is null) return new(404);
        var profile = await repository.Profile(owner, classId, ct);
        var observations = await repository.Observations(owner, classId, ct);
        var evidence = await repository.Evidence(owner, classId, ct);
        var rows = roster.Students.SelectMany(student => (profile?.ReadSkills() ?? []).Select(skill =>
        {
            var history = observations.Where(x => x.StudentId == student.Id && x.SkillId == skill.Id && x.Date <= date).ToArray();
            var results = evidence.Where(x => x.StudentId == student.Id && x.SkillId == skill.Id && x.Date <= date).ToArray();
            var latest = history.FirstOrDefault();
            return new { studentId = student.Id, studentName = student.Name, skillId = skill.Id, skillTitle = skill.Title, skill.Taught, latestObservation = latest,
                observations = history, evidence = results, mastery = skill.Taught ? SkillProgress.Mastery(results) : "not_taught",
                pendingReview = skill.Taught && latest is { Level: "red" or "amber" } && latest.Date < date };
        })).ToArray();
        return new(200, new { date, students = roster.Students, rows });
    }
}
