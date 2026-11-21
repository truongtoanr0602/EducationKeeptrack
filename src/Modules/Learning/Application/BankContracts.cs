using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Application;

/// <summary>Storage of curriculum lessons, the question bank and exams; all reads are scoped to owner and class.</summary>
public interface IQuestionBankRepository
{
    /// <summary>The curriculum with skills and lessons; `track` must be true when the caller changes and saves it.</summary>
    Task<ClassCurriculum?> FindCurriculumAsync(string owner, Guid classId, bool track, CancellationToken ct);
    Task<List<LessonSessionLink>> ListLinksAsync(string owner, Guid classId, CancellationToken ct);
    Task<List<Question>> ListQuestionsAsync(string owner, Guid classId, bool track, CancellationToken ct);
    Task<Question?> FindQuestionAsync(string owner, Guid classId, Guid id, CancellationToken ct);
    Task<List<QuestionImage>> ListImagesAsync(string owner, Guid classId, CancellationToken ct);
    Task<QuestionImage?> FindImageAsync(Guid id, CancellationToken ct);
    Task<List<ExamBlueprint>> ListBlueprintsAsync(string owner, Guid classId, CancellationToken ct);
    Task<List<Exam>> ListExamsAsync(string owner, Guid classId, CancellationToken ct);
    Task<Exam?> FindExamAsync(string owner, Guid classId, Guid id, bool track, CancellationToken ct);
    Task<Exam?> FindExamByTokenAsync(string token, bool track, CancellationToken ct);
    Task<Assessment?> FindAssessmentAsync(string owner, Guid classId, Guid id, CancellationToken ct);
    Task<AssessmentSkillSet?> FindSkillSetAsync(string owner, Guid classId, Guid assessmentId, CancellationToken ct);
    void Add(IEnumerable<object> entities);
    void Remove(object entity);
    /// <summary>False when a concurrent change won (stale version or a unique key taken).</summary>
    Task<bool> SaveAsync(CancellationToken ct);
}

/// <summary>Counts of one import or copy, and every problem found. Nothing is written while Issues is not empty.</summary>
public sealed record ImportReport(bool Committed, bool CurriculumCreated, int SkillsAdded, int SkillsUpdated, int LessonsAdded, int LessonsUpdated,
    int QuestionsAdded, int QuestionsUpdated, IReadOnlyList<ImportIssue> Issues, IReadOnlyList<string> MissingImages);
