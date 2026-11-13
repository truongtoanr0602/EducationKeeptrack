namespace EducationKeeptrack.Learning.Domain;

/// <summary>One assessment on a student's chart, taken from the latest confirmed result only.</summary>
public sealed record ProgressPoint(Guid AssessmentId, string Title, DateOnly Date, string Method, string Status,
    int? Completion, int? Correctness, bool Plotted, bool Confirmed, string Reason);

public sealed record ProgressCompleteness(int Total, int Plotted, int NotAssigned, int Unconfirmed, int Unassessed);

public sealed record StudentProgress(Guid StudentId, string StudentName, DateOnly From, DateOnly To,
    IReadOnlyList<ProgressPoint> Points, ProgressCompleteness Completeness, bool EnoughForTrend);

/// <summary>
/// Builds the two-indicator series (completion and correctness). Unconfirmed drafts never count; not doing the work or having
/// no correct answer is a real 0 on that indicator, while "not assigned" and "not yet assessed" are gaps, not zeros.
/// </summary>
public static class ProgressSeries
{
    /// <summary>A trend line needs at least two plotted points; this is a drawing minimum, not an alert threshold.</summary>
    public const int MinimumForTrend = 2;

    public static StudentProgress Build(Guid studentId, string name, DateOnly from, DateOnly to, IEnumerable<Assessment> assessments)
    {
        var points = new List<ProgressPoint>();
        foreach (var assessment in assessments.Where(x => x.Date >= from && x.Date <= to).OrderBy(x => x.Date).ThenBy(x => x.Id))
        {
            var submission = assessment.Submissions.SingleOrDefault(x => x.StudentId == studentId);
            if (submission is null) continue; // student was not in the class when this assessment was created
            var final = submission.History.Where(x => x.Action == "confirmed").OrderByDescending(x => x.Version).FirstOrDefault();
            points.Add(final is null
                ? new(assessment.Id, assessment.Title, assessment.Date, submission.Method, "unassessed", null, null, false, false, "Chưa có kết quả được xác nhận")
                : Point(assessment, final));
        }
        var plotted = points.Count(x => x.Plotted);
        var completeness = new ProgressCompleteness(points.Count, plotted,
            points.Count(x => x.Status == "unassigned"),
            points.Count(x => !x.Confirmed),
            points.Count(x => x.Confirmed && x.Status == "unassessed"));
        return new(studentId, name, from, to, points, completeness, plotted >= MinimumForTrend);
    }

    private static ProgressPoint Point(Assessment assessment, ResultRevision final) => final.Status switch
    {
        "assessed" => new(assessment.Id, assessment.Title, assessment.Date, final.Method, final.Status, final.Completion, final.Correctness, true, true, "Đã đánh giá"),
        "no_correct" => new(assessment.Id, assessment.Title, assessment.Date, final.Method, final.Status, final.Completion, 0, true, true, "Không có câu đúng: làm đúng tính 0"),
        "not_done" => new(assessment.Id, assessment.Title, assessment.Date, final.Method, final.Status, 0, 0, true, true, "Không làm bài: cả hai chỉ số tính 0"),
        "unassigned" => new(assessment.Id, assessment.Title, assessment.Date, final.Method, final.Status, null, null, false, true, "Không giao bài: không tính vào biểu đồ"),
        _ => new(assessment.Id, assessment.Title, assessment.Date, final.Method, "unassessed", null, null, false, true, "Chưa đánh giá: không tính vào biểu đồ"),
    };
}
