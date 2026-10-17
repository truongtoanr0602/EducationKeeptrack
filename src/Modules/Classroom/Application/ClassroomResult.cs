namespace EducationKeeptrack.Classroom.Application;

public enum ClassroomFailure { NotFound, Invalid, Conflict }
public record ClassroomResult<T>(T? Value, ClassroomFailure? Failure = null, string? Message = null, Dictionary<string, string[]>? Errors = null) where T : class;
