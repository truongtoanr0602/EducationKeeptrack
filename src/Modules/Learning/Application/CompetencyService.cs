using EducationKeeptrack.Contracts;
using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

public sealed record StudentSkillResult(Guid StudentId, Guid SkillId, ConfirmedResult Result);

public interface ICompetencyRepository
{
    /// <summary>Confirmed final results of the class, one per student, skill and assessment.</summary>
    Task<IReadOnlyList<StudentSkillResult>> ListConfirmedAsync(string owner, Guid classId, CancellationToken ct);
}

public sealed record CompetencySkill(Skill Skill, SkillCompetency Result);
public sealed record StudentCompetency(Guid StudentId, string Name, IReadOnlyList<CompetencySkill> Skills);
public sealed record CompetencyProfile(bool Configured, MasteryRule? Rule, int CurriculumVersion, IReadOnlyList<StudentCompetency> Students);

public sealed class CompetencyService(ICurriculumRepository curricula, ICompetencyRepository evidence, IClassroomLearningSource classroom, LessonService lessons, TimeProvider clock)
{
    /// <summary>Skills counted as taught: marked by the teacher or covered by a lesson whose session was held.</summary>
    public Task<IReadOnlySet<Guid>> TaughtSkillsAsync(string owner, Guid classId, ClassCurriculum? curriculum, CancellationToken ct) =>
        lessons.TaughtSkillsAsync(owner, classId, curriculum, ct);

    /// <summary>Confirmed results of the class; the model reads attempts from here, never anything unconfirmed.</summary>
    public Task<IReadOnlyList<StudentSkillResult>> ConfirmedAsync(string owner, Guid classId, CancellationToken ct) =>
        evidence.ListConfirmedAsync(owner, classId, ct);

    public async Task<LearningResult<CompetencyProfile>> ProfileAsync(string owner, Guid classId, CancellationToken ct)
    {
        var roster = await classroom.ReadRosterAsync(owner, classId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct);
        if (roster is null) return new(null, 404);
        var curriculum = await curricula.FindAsync(owner, classId, false, ct);
        if (curriculum is null) return new(new(false, null, 0, []));
        var rule = new MasteryRule(curriculum.MasteryThreshold, curriculum.MinTasks, curriculum.RequiredAttempts, curriculum.RuleVersion);
        var results = (await evidence.ListConfirmedAsync(owner, classId, ct)).ToLookup(x => (x.StudentId, x.SkillId));
        var skills = curriculum.GetEffectiveSkills().OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToList();
        var taught = await lessons.TaughtSkillsAsync(owner, classId, curriculum, ct);
        var students = roster.Students.Select(student => new StudentCompetency(student.Id, student.Name,
            skills.Select(skill => new CompetencySkill(skill,
                Competency.Evaluate(taught.Contains(skill.Id), rule, results[(student.Id, skill.Id)].Select(x => x.Result)))).ToList())).ToList();
        return new(new(true, rule, curriculum.Version, students));
    }
}
