# Cộng dồn kết quả đề kiểm tra online trong hồ sơ năng lực

**Trạng thái: accepted — 20/11/2026.** Thuộc issue #82 (cha #77). Sửa quyết định trên #34 (11/11/2026) cho riêng kết quả từ đề online.

## Bối cảnh

Theo luật trên #34, một kết quả theo kỹ năng chỉ hợp lệ khi có ít nhất `MinTasks` nhiệm vụ (mặc định 5) trong một bài. Luật xét `RequiredAttempts` lần gần nhất (mặc định 2), mỗi lần xét riêng và không cộng dồn. Một đề kiểm tra online 10 câu trải trên nhiều kỹ năng chỉ có 1–2 câu cho mỗi kỹ năng chính, nên theo luật này kết quả đề online không bao giờ được tính: học sinh mãi ở "chưa đủ dữ liệu".

## Quyết định

- Kết quả theo kỹ năng có thêm **nguồn**: `manual` (giáo viên chấm) hoặc `exam` (bài đánh giá được tạo khi mở một đề kiểm tra online, chấm theo đáp án rồi giáo viên xác nhận).
- Nguồn `manual` giữ nguyên luật #34.
- Nguồn `exam` được **cộng dồn**:
  - lấy trọn các kết quả mới nhất đến khi đủ `MinTasks × RequiredAttempts` câu;
  - có ít nhất `MinTasks` câu thì kết luận một lần: tỉ lệ đúng từ `Threshold` trở lên là **đạt**, dưới là **chưa đạt**;
  - chưa đủ `MinTasks` câu thì là **chưa đủ dữ liệu**.
- Một kỹ năng có kết luận ở cả hai nguồn thì lấy kết luận của nguồn có bằng chứng mới hơn. Bằng chứng luôn liệt kê đủ cả hai nguồn và ghi rõ dòng nào được tính.
- Kỹ năng được tính "đã dạy" khi giáo viên đánh dấu, hoặc khi buổi chương trình chứa kỹ năng đó đã gắn với một buổi học đã diễn ra.
- **Sửa 24/11/2026 (ADR 0008, #111):** với câu có điểm lẻ và câu tự luận chấm tay, `MinTasks` vẫn đếm theo **số câu**, nhưng tỉ lệ của kỹ năng = **tổng điểm đạt / tổng điểm tối đa** của các câu thuộc kỹ năng đó (không còn đếm câu đúng nguyên). Câu tự luận chỉ được tính sau khi giáo viên chấm; chưa chấm thì kỹ năng đó ở trạng thái chờ chấm. Đề cũ coi mỗi câu 1 điểm nên kết quả không đổi. **Làm tròn:** kết quả kỹ năng đã lưu giữ số nguyên "số câu đúng" (và `MinTasks`, `Threshold`, suy luận kiến thức đều dùng số nguyên), nên điểm được quy về số câu tương đương: `Correct = làm tròn(điểm đạt / điểm tối đa × số câu)`, không vượt số câu. Với đề cũ (mỗi câu 1 điểm) kết quả giống hệt trước; với điểm lẻ, kết quả lưu có thể lệch tối đa nửa câu so với tỉ lệ điểm chính xác, còn điểm chính xác vẫn thấy ở kết quả của đề.
- Migration `LessonsQuestionBankExams` tăng `RuleVersion` của mọi chương trình thêm 1, để hồ sơ và kế hoạch ghi được là chúng dùng luật mới.

## Hệ quả

- Một kỹ năng có thể có kết luận sau 3–5 đề online thay vì không bao giờ.
- Một câu sai ảnh hưởng ít hơn vì được cộng với các câu khác. Ngưỡng `Threshold` của lớp vẫn là ngưỡng duy nhất.
