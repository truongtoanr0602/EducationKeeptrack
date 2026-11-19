using EducationKeeptrack.Contracts;
using EducationKeeptrack.Reporting.Application;

namespace EducationKeeptrack.Reporting.Infrastructure;

public sealed class SimulationAiCommentEngine : IAiCommentEngine
{
    private static readonly string[] ProhibitedWords = ["yếu", "kém", "chậm", "dở", "lười", "tệ"];

    public Task<AiCommentProposal> GenerateCommentAsync(
        string studentName,
        string className,
        string subject,
        DateOnly month,
        IReadOnlyList<ReportSession> sessions,
        IReadOnlyList<ReportAssessment> assessments,
        CancellationToken ct)
    {
        int heldSessions = sessions.Count(x => x.SessionStatus == "held");
        int presentSessions = sessions.Count(x => x.SessionStatus == "held" && x.AttendanceStatus == "present");
        int assessedCount = assessments.Count(x => x.HasConfirmedResult);

        string comment;
        string roadmap;

        if (heldSessions == 0)
        {
            comment = $"Trong tháng {month:MM/yyyy}, lớp chưa có buổi học chính thức diễn ra. Học sinh {studentName} duy trì liên lạc và sẵn sàng cho các buổi học tiếp theo.";
            roadmap = "Tiếp tục chuẩn bị sách vở và tham gia đầy đủ các buổi học theo thời khóa biểu của lớp.";
        }
        else
        {
            var avgCorrectness = assessedCount > 0 ? assessments.Where(x => x.HasConfirmedResult && x.Correctness.HasValue).Average(x => x.Correctness!.Value) : 75;
            var attendanceText = presentSessions == heldSessions
                ? $"tham gia đầy đủ {presentSessions}/{heldSessions} buổi học với tinh thần học tập nghiêm túc"
                : $"tham gia {presentSessions}/{heldSessions} buổi học trong tháng";

            string performanceText;
            if (assessedCount == 0)
            {
                performanceText = "Học sinh bước đầu làm quen với các dạng bài mới và hoàn thành các bài tập được giao trên lớp.";
            }
            else if (avgCorrectness >= 80)
            {
                performanceText = $"Kết quả các bài kiểm tra đạt mức tốt (trung bình {Math.Round(avgCorrectness)}%), học sinh nắm chắc kiến thức trọng tâm và trình bày bài khoa học.";
            }
            else
            {
                performanceText = "Học sinh nắm được phương pháp làm bài cơ bản, tích cực phát biểu và có tiến bộ rõ rệt qua từng tuần học.";
            }

            var familyAdvice = "Gia đình nên tiếp tục động viên em duy trì thói quen ôn bài 15-20 phút mỗi ngày trước buổi học.";
            comment = $"Trong tháng {month:MM/yyyy}, em {studentName} đã {attendanceText}. {performanceText} {familyAdvice}";

            roadmap = assessedCount > 0 && avgCorrectness < 75
                ? $"1. Củng cố các dạng bài trọng tâm của môn {subject}.\n2. Tăng cường luyện tập các bài tập vừa sức để nâng cao độ chính xác khi tính toán.\n3. Chuẩn bị trước bài học cho tháng tới theo hướng dẫn của giáo viên."
                : $"1. Nâng cao các dạng bài tư duy và mở rộng của môn {subject}.\n2. Duy trì thói quen tự kiểm tra lại bài làm trước khi nộp.\n3. Tiếp tục phát huy tinh thần tự giác trong học tập.";
        }

        // Enforce BR-AI-20 prohibition
        foreach (var word in ProhibitedWords)
        {
            if (comment.Contains(word, StringComparison.OrdinalIgnoreCase))
                comment = comment.Replace(word, "cần rèn luyện thêm", StringComparison.OrdinalIgnoreCase);
        }

        return Task.FromResult(new AiCommentProposal(comment, roadmap));
    }
}
