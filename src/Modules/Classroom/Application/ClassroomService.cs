using EducationKeeptrack.Classroom.Domain;
namespace EducationKeeptrack.Classroom.Application;

public interface IClassRepository
{
    Task<IReadOnlyList<TeacherClass>> ListAsync(string owner, CancellationToken ct);
    Task<TeacherClass?> FindAsync(string owner, Guid id, CancellationToken ct);
    Task SaveAsync(TeacherClass item, CancellationToken ct);
}

public sealed class ClassroomService(IClassRepository repository)
{
    public Task<IReadOnlyList<TeacherClass>> ListAsync(string owner, CancellationToken ct) => repository.ListAsync(owner, ct);
    public Task<TeacherClass?> FindAsync(string owner, Guid id, CancellationToken ct) => repository.FindAsync(owner, id, ct);
    public async Task<TeacherClass> CreateAsync(string owner, ClassInput input, CancellationToken ct)
    {
        var item = new TeacherClass(owner, input);
        await repository.SaveAsync(item, ct);
        return item;
    }
}
