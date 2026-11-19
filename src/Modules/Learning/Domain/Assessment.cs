namespace EducationKeeptrack.Learning.Domain;

public sealed class Assessment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string OwnerId { get; private set; } = "";
    public Guid ClassId { get; private set; }
    public string Title { get; private set; } = "";
    public DateOnly Date { get; private set; }
    public string DefaultMethod { get; private set; } = "manual";
    public string Rubric { get; private set; } = "";
    public int RubricVersion { get; private set; } = 1;
    public List<Submission> Submissions { get; private set; } = [];
    private Assessment() { }
    public Assessment(string owner, Guid classId, string title, DateOnly date, IEnumerable<(Guid Id, string Name)> students, string defaultMethod = "manual", string rubric = "")
    {
        OwnerId = owner; ClassId = classId; Title = title.Trim(); Date = date; DefaultMethod = defaultMethod;
        Rubric = rubric.Trim();
        Submissions = students.Select(x => new Submission(Id, x.Id, x.Name, DefaultMethod)).ToList();
    }
    public void UpdateRubric(string rubric)
    {
        Rubric = rubric.Trim();
        RubricVersion++;
    }
}

public sealed class Submission
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AssessmentId { get; private set; }
    public Guid StudentId { get; private set; }
    public string StudentName { get; private set; } = "";
    public string Method { get; private set; } = "manual";
    public string Status { get; private set; } = "unassessed";
    public int? Completion { get; private set; }
    public int? Correctness { get; private set; }
    public int Version { get; private set; } = 1;
    public List<ResultRevision> History { get; private set; } = [];
    public List<SubmissionPhoto> Photos { get; private set; } = [];
    public int? AiCompletion { get; private set; }
    public int? AiCorrectness { get; private set; }
    public double? AiConfidence { get; private set; }
    public string? AiReasoning { get; private set; }
    public string? AiWarning { get; private set; }
    public string AiStatus { get; private set; } = "none";
    public DateTimeOffset? AiProposedAt { get; private set; }
    private Submission() { }
    public Submission(Guid assessmentId, Guid studentId, string studentName, string method)
    {
        AssessmentId = assessmentId; StudentId = studentId; StudentName = studentName; Method = method;
    }

    public void AddPhoto(SubmissionPhoto photo)
    {
        Photos.Add(photo);
        Version++;
    }
    public void RemovePhoto(Guid photoId)
    {
        Photos.RemoveAll(x => x.Id == photoId);
        for (int i = 0; i < Photos.Count; i++) Photos[i].SetSequenceIndex(i + 1);
        Version++;
    }
    public void ReorderPhotos(IEnumerable<Guid> orderedIds)
    {
        var idList = orderedIds.ToList();
        for (int i = 0; i < idList.Count; i++)
        {
            var p = Photos.FirstOrDefault(x => x.Id == idList[i]);
            p?.SetSequenceIndex(i + 1);
        }
        Photos = Photos.OrderBy(x => x.SequenceIndex).ToList();
        Version++;
    }
    public void SetAiProposal(int completion, int correctness, double confidence, string reasoning, string? warning, DateTimeOffset now)
    {
        AiCompletion = completion;
        AiCorrectness = correctness;
        AiConfidence = confidence;
        AiReasoning = reasoning;
        AiWarning = warning;
        AiStatus = "proposed";
        AiProposedAt = now;
        Version++;
    }
    public void ClearAiProposal()
    {
        AiCompletion = null;
        AiCorrectness = null;
        AiConfidence = null;
        AiReasoning = null;
        AiWarning = null;
        AiStatus = "none";
        AiProposedAt = null;
        Version++;
    }

    public Dictionary<string, string[]> Validate(ResultInput input)
    {
        var errors = new Dictionary<string, string[]>();
        if (input.Status is not ("assessed" or "not_done" or "no_correct" or "unassigned" or "unassessed"))
            errors["status"] = ["Trạng thái đánh giá không hợp lệ."];
        if (input.Status == "assessed" && (!IsLevel(input.Completion) || !IsLevel(input.Correctness)))
            errors["levels"] = ["Chọn riêng hai mức 25%, 50%, 75% hoặc 100%."];
        if (input.Status == "no_correct" && (!IsLevel(input.Completion) || input.Correctness is not (null or 0)))
            errors["levels"] = ["Không có câu đúng: chọn mức hoàn thành; kết quả làm đúng bằng 0."];
        if (input.Status == "not_done" && (input.Completion is not (null or 0) || input.Correctness is not (null or 0)))
            errors["levels"] = ["Không làm bài: cả hai chỉ số bằng 0."];
        if (input.Status is "unassigned" or "unassessed" && (input.Completion is not null || input.Correctness is not null))
            errors["levels"] = ["Không giao/chưa đánh giá phải để trống hai chỉ số."];
        return errors;
    }
    private static bool IsLevel(int? value) => value is 25 or 50 or 75 or 100;
    public void Record(ResultInput input, DateTimeOffset recordedAt)
    {
        Status = input.Status;
        Completion = Status == "not_done" ? 0 : input.Completion;
        Correctness = Status is "not_done" or "no_correct" ? 0 : input.Correctness;
        Version++;
        History.Add(new ResultRevision(Id, Version, Status, Completion, Correctness, Method, input.Confirm ? "confirmed" : "edited", recordedAt));
    }
    public void RecordMethod(string method, DateTimeOffset recordedAt)
    {
        Method = method; Version++;
        History.Add(new ResultRevision(Id, Version, Status, Completion, Correctness, Method, "method_changed", recordedAt));
    }
}

public record ResultInput(string Status, int? Completion, int? Correctness, int Version, bool Confirm);
public sealed class ResultRevision
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubmissionId { get; private set; }
    public int Version { get; private set; }
    public string Status { get; private set; } = "";
    public int? Completion { get; private set; }
    public int? Correctness { get; private set; }
    public string Method { get; private set; } = "";
    public string Action { get; private set; } = "";
    public DateTimeOffset RecordedAt { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public bool IncludedInStatistics => Status is "assessed" or "not_done" or "no_correct";
    private ResultRevision() { }
    public void Acknowledge(DateTimeOffset now) => DispatchedAt = now;
    public ResultRevision(Guid submissionId, int version, string status, int? completion, int? correctness, string method, string action, DateTimeOffset recordedAt)
    {
        SubmissionId = submissionId; Version = version; Status = status; Completion = completion;
        Correctness = correctness; Method = method; Action = action; RecordedAt = recordedAt;
    }
}

public sealed class SubmissionPhoto
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubmissionId { get; private set; }
    public string PhotoKey { get; private set; } = "";
    public int SequenceIndex { get; private set; }
    public string ContentType { get; private set; } = "image/jpeg";
    public long ByteLength { get; private set; }
    public string Sha256 { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    private SubmissionPhoto() { }
    public SubmissionPhoto(Guid submissionId, string photoKey, int sequenceIndex, string contentType, long byteLength, string sha256, DateTimeOffset createdAt)
    {
        SubmissionId = submissionId; PhotoKey = photoKey; SequenceIndex = sequenceIndex;
        ContentType = contentType; ByteLength = byteLength; Sha256 = sha256; CreatedAt = createdAt;
    }
    public void SetSequenceIndex(int index) => SequenceIndex = index;
}
