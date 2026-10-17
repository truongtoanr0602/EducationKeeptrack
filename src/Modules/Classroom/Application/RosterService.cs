using EducationKeeptrack.Classroom.Domain;
namespace EducationKeeptrack.Classroom.Application;

public enum RosterFailure { NotFound, Invalid, Conflict }
public record RosterResult<T>(T? Value, RosterFailure? Failure = null, string? Message = null, Dictionary<string, string[]>? Errors = null) where T : class;
public record StudentView(Guid Id, string Name, string? ParentName, string? Phone, string? Email, string? Notes, int Version)
{
    public static StudentView From(Student student) => new(student.Id, student.Name, student.ParentName, student.Phone, student.Email, student.Notes, student.Version);
}
public record EnrollmentHistoryView(string Status, DateOnly EffectiveDate, DateTimeOffset RecordedAt, int Version);
public record EnrollmentView(Guid Id, Guid StudentId, DateOnly StartDate, DateOnly? EndDate, string Status, int Version, StudentView Student, IReadOnlyList<EnrollmentHistoryView> History)
{
    public static EnrollmentView From(Enrollment item, Student student) => new(item.Id, item.StudentId, item.StartDate, item.EndDate, item.Status, item.Version, StudentView.From(student), item.History.OrderBy(x => x.Version).Select(x => new EnrollmentHistoryView(x.Status, x.EffectiveDate, x.RecordedAt, x.Version)).ToArray());
}
public record EnrollmentWithStudent(Enrollment Enrollment, Student Student);

public interface IRosterRepository
{
    Task<IReadOnlyList<Student>> ListStudentsAsync(string owner, CancellationToken ct);
    Task<Student?> FindStudentAsync(string owner, Guid id, CancellationToken ct);
    Task<bool> ClassExistsAsync(string owner, Guid classId, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentWithStudent>> ListEnrollmentsAsync(string owner, Guid classId, CancellationToken ct);
    Task<Enrollment?> FindEnrollmentAsync(string owner, Guid classId, Guid id, CancellationToken ct);
    Task<bool> HasOverlappingEnrollmentAsync(string owner, Guid classId, Guid studentId, DateOnly startDate, CancellationToken ct);
    void Add(Student student);
    void Add(Enrollment enrollment);
    Task<bool> TrySaveAsync(CancellationToken ct);
    Task<RosterResult<EnrollmentView>> WithEnrollmentLockAsync(Guid studentId, Func<Task<RosterResult<EnrollmentView>>> action, CancellationToken ct);
}

public sealed class RosterService(IRosterRepository repository, TimeProvider clock)
{
    public async Task<IReadOnlyList<StudentView>> ListStudentsAsync(string owner, CancellationToken ct) =>
        (await repository.ListStudentsAsync(owner, ct)).Select(StudentView.From).ToArray();
    public async Task<StudentView?> FindStudentAsync(string owner, Guid id, CancellationToken ct)
    {
        var student = await repository.FindStudentAsync(owner, id, ct);
        return student is null ? null : StudentView.From(student);
    }
    public async Task<RosterResult<StudentView>> SaveStudentAsync(string owner, Guid? id, StudentInput input, CancellationToken ct)
    {
        var student = id is null ? null : await repository.FindStudentAsync(owner, id.Value, ct);
        if (id is not null && student is null) return new(null, RosterFailure.NotFound);
        var errors = input.Validate();
        if (errors.Count > 0) return new(null, RosterFailure.Invalid, Errors: errors);
        if (student is null)
        {
            student = new Student(owner, input);
            repository.Add(student);
        }
        else if (!student.TryUpdate(input)) return new(null, RosterFailure.Conflict, "Hồ sơ đã thay đổi. Vui lòng tải lại danh sách trước khi lưu tiếp.");
        return await repository.TrySaveAsync(ct) ? new(StudentView.From(student))
            : new(null, RosterFailure.Conflict, "Hồ sơ đã thay đổi. Vui lòng tải lại danh sách trước khi lưu tiếp.");
    }
    public async Task<RosterResult<IReadOnlyList<EnrollmentView>>> ListEnrollmentsAsync(string owner, Guid classId, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(owner, classId, ct)) return new(null, RosterFailure.NotFound);
        var rows = await repository.ListEnrollmentsAsync(owner, classId, ct);
        return new(rows.Select(x => EnrollmentView.From(x.Enrollment, x.Student)).ToArray());
    }
    public async Task<RosterResult<EnrollmentView>> EnrollAsync(string owner, Guid classId, EnrollmentInput input, CancellationToken ct)
    {
        var student = await repository.FindStudentAsync(owner, input.StudentId, ct);
        if (student is null || !await repository.ClassExistsAsync(owner, classId, ct)) return new(null, RosterFailure.NotFound);
        if (input.StartDate == default) return new(null, RosterFailure.Invalid, Errors: new() { ["startDate"] = ["Vui lòng nhập ngày bắt đầu."] });
        return await repository.WithEnrollmentLockAsync(input.StudentId, async () =>
        {
            if (await repository.HasOverlappingEnrollmentAsync(owner, classId, input.StudentId, input.StartDate, ct))
                return new(null, RosterFailure.Conflict, "Học sinh đã có đăng ký đang hiệu lực hoặc trùng thời gian trong lớp này.");
            var item = new Enrollment(owner, classId, input.StudentId, input.StartDate, clock.GetUtcNow());
            repository.Add(item);
            return await repository.TrySaveAsync(ct) ? new(EnrollmentView.From(item, student))
                : new(null, RosterFailure.Conflict, "Đăng ký đã thay đổi hoặc trùng thời gian. Vui lòng tải lại danh sách.");
        }, ct);
    }
    public async Task<RosterResult<EnrollmentView>> ChangeStatusAsync(string owner, Guid classId, Guid id, EnrollmentStatusInput input, CancellationToken ct)
    {
        var item = await repository.FindEnrollmentAsync(owner, classId, id, ct);
        if (item is null) return new(null, RosterFailure.NotFound);
        var problem = item.TryChange(input, clock.GetUtcNow());
        if (problem is not null) return new(null, problem.IsConflict ? RosterFailure.Conflict : RosterFailure.Invalid, problem.Message, problem.Errors);
        if (!await repository.TrySaveAsync(ct)) return new(null, RosterFailure.Conflict, "Đăng ký đã được thay đổi. Vui lòng tải lại danh sách.");
        var student = await repository.FindStudentAsync(owner, item.StudentId, ct);
        return new(EnrollmentView.From(item, student!));
    }
}
