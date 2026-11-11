namespace EducationKeeptrack.ServiceAccess.Application;

public sealed record Outcome(int Status, object? Body = null)
{
    public static Outcome Ok(object? body = null) => new(200, body);
    public static Outcome Conflict(string message, object? extra = null) => new(409, new { message, detail = extra });
    public static Outcome Invalid(string field, string message) => new(400, new Dictionary<string, string[]> { [field] = [message] });
    public static readonly Outcome NotFound = new(404);
}
