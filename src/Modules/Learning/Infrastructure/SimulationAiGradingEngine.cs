using EducationKeeptrack.Learning.Application;

namespace EducationKeeptrack.Learning.Infrastructure;

public sealed class SimulationAiGradingEngine : IAiGradingEngine
{
    public Task<AiGradingProposal> GradeAsync(string rubric, IReadOnlyList<byte[]> photos, CancellationToken ct)
    {
        if (photos.Count == 0)
            throw new InvalidOperationException("Không có ảnh bài làm để đánh giá.");

        var totalBytes = photos.Sum(x => (long)x.Length);
        int completion = (int)(totalBytes % 4) switch
        {
            0 => 100,
            1 => 75,
            2 => 50,
            _ => 75
        };
        int correctness = (int)((rubric.Length + totalBytes) % 4) switch
        {
            0 => 100,
            1 => 75,
            2 => 50,
            _ => 50
        };

        var reasoning = $"AI đã đối chiếu {photos.Count} trang bài nộp với hướng dẫn chấm. Học sinh nắm vững nội dung và phương pháp giải, trình bày đầy đủ các bước theo yêu cầu.";
        string? warning = photos.Count > 1 && totalBytes < 50_000 ? "Ảnh có độ phân giải thấp, giáo viên nên kiểm tra chi tiết các bước tính." : null;

        return Task.FromResult(new AiGradingProposal(completion, correctness, 0.88, reasoning, warning));
    }
}
