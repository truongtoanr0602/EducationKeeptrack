using System.Text.Json;
using EducationKeeptrack.Learning.Domain;
using EducationKeeptrack.Learning.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace EducationKeeptrack.Learning.Application;

public sealed record PlanInput(string? Title, DateOnly Date, int Minutes, string? Content, PlanPriority[]? Priorities, PlanActivity[]? Activities, int Version = 0);
public sealed record PlanVersion(int Version);
public sealed class PlanService(LearningDatabase db, PersonalizedLearningService learning, TimeProvider clock)
{
    private Task<PersonalizedPlan?> Find(string owner, Guid classId, Guid id, CancellationToken ct) => db.PersonalizedPlans.SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId && x.Id == id, ct);
    public async Task<PersonalResult> List(string owner, Guid classId, CancellationToken ct)
    {
        if (await learning.Roster(owner, classId, learning.Today, ct) is null) return new(404);
        return new(200, await db.PersonalizedPlans.AsNoTracking().Where(x => x.OwnerId == owner && x.ClassId == classId).OrderByDescending(x => x.Date).ThenByDescending(x => x.RecordedAt).ToArrayAsync(ct));
    }
    public async Task<PersonalResult> Read(string owner, Guid classId, Guid id, CancellationToken ct)
    {
        if (await learning.Roster(owner, classId, learning.Today, ct) is null) return new(404);
        var plan = await Find(owner, classId, id, ct);
        return plan is null ? new(404) : new(200, plan);
    }
    public async Task<PersonalResult> Save(string owner, Guid classId, Guid? id, PlanInput input, CancellationToken ct)
    {
        var roster = await learning.Roster(owner, classId, input.Date == default ? learning.Today : input.Date, ct);
        if (roster is null) return new(404);
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 160 || string.IsNullOrWhiteSpace(input.Content) || input.Content.Length > 10000 || input.Date == default || input.Minutes is < 1 or > 480)
            return new(400, Message: "Nhập tên, ngày, nội dung tối đa 10000 ký tự và quỹ 1–480 phút.");
        var profile = await db.ClassLearningProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
        if (profile is null) return new(400, Message: "Lưu cấu hình bộ sách và kỹ năng trước khi tạo kế hoạch.");
        var priorities = input.Priorities ?? [];
        var activities = input.Activities ?? [];
        if (priorities.Length > 200 || priorities.Any(x => x is null) || priorities.Distinct().Count() != priorities.Length || priorities.GroupBy(x => x.StudentId).Any(x => x.Count() > 2)
            || priorities.Any(x => !roster.Students.Any(s => s.Id == x.StudentId) || !profile.ReadSkills().Any(s => s.Id == x.SkillId && s.Taught)))
            return new(400, Message: "Mỗi học sinh tối đa hai kỹ năng đã dạy, thuộc đúng lớp, không trùng.");
        if (activities.Length > 100 || activities.Any(x => x is null || string.IsNullOrWhiteSpace(x.Title) || x.Title.Length > 160 || x.Minutes is < 1 or > 480) || activities.Sum(x => (long)x.Minutes) > input.Minutes)
            return new(400, Message: "Tổng thời lượng hoạt động không được vượt quỹ phút.");
        var plan = id is { } key ? await Find(owner, classId, key, ct) : null;
        if (id is not null && plan is null) return new(404);
        if (plan is not null && plan.Version != input.Version || plan is null && input.Version != 0) return new(409);
        if (plan is null)
        {
            plan = new() { OwnerId = owner, ClassId = classId, RecordedAt = clock.GetUtcNow() };
            db.PersonalizedPlans.Add(plan);
        }
        else { Archive(plan); plan.Version++; }
        plan.Title = input.Title.Trim(); plan.Date = input.Date; plan.Minutes = input.Minutes; plan.Content = input.Content.Trim();
        plan.PrioritiesJson = JsonSerializer.Serialize(priorities); plan.ActivitiesJson = JsonSerializer.Serialize(activities); plan.Status = "draft"; plan.ApprovedAt = null;
        plan.SnapshotJson = await Snapshot(owner, classId, input.Date, profile, ct);
        try { await db.SaveChangesAsync(ct); return new(id is null ? 201 : 200, plan); }
        catch (DbUpdateConcurrencyException) { return new(409); }
    }
    public async Task<PersonalResult> Approve(string owner, Guid classId, Guid id, PlanVersion input, CancellationToken ct)
    {
        if (await learning.Roster(owner, classId, learning.Today, ct) is null) return new(404);
        var plan = await Find(owner, classId, id, ct);
        if (plan is null) return new(404);
        if (plan.Version != input.Version || plan.Status != "draft") return new(409);
        var profile = await db.ClassLearningProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == owner && x.ClassId == classId, ct);
        if (profile is null || plan.SnapshotJson != await Snapshot(owner, classId, plan.Date, profile, ct))
            return new(409, Message: "Dữ liệu nguồn đã thay đổi. Mở sửa, kiểm tra lại mục tiêu và lưu bản nháp mới trước khi duyệt.");
        Archive(plan); plan.Version++; plan.Status = "approved"; plan.ApprovedAt = clock.GetUtcNow();
        try { await db.SaveChangesAsync(ct); return new(200, plan); }
        catch (DbUpdateConcurrencyException) { return new(409); }
    }
    private static void Archive(PersonalizedPlan plan)
    {
        var history = JsonSerializer.Deserialize<List<JsonElement>>(plan.HistoryJson) ?? [];
        history.Add(JsonSerializer.SerializeToElement(new { plan.Version, plan.Title, plan.Date, plan.Minutes, plan.Content, plan.Status, plan.ApprovedAt, plan.PrioritiesJson, plan.ActivitiesJson, plan.SnapshotJson }));
        plan.HistoryJson = JsonSerializer.Serialize(history);
    }
    private async Task<string> Snapshot(string owner, Guid classId, DateOnly date, ClassLearningProfile profile, CancellationToken ct)
    {
        var progress = await learning.Progress(owner, classId, date, ct);
        return JsonSerializer.Serialize(new { profile = new { profile.Textbook, profile.Semester, profile.Version, skills = profile.ReadSkills() }, progress = progress.Value });
    }
}
