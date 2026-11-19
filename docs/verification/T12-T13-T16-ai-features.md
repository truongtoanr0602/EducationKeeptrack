# Nghiệm thu T12, T13, T16 — Ảnh bài nộp và tính năng AI

**Ngày lập:** 19/11/2026  
**Tickets:**
- T12: [Tải và quản lý ảnh bài nộp (#13)](https://github.com/truongtoanr0602/EducationKeeptrack/issues/13)
- T13: [AI chấm bài và giáo viên xác nhận (#14)](https://github.com/truongtoanr0602/EducationKeeptrack/issues/14)
- T16 / T37: [AI nhận xét báo cáo tháng (#16 / #41)](https://github.com/truongtoanr0602/EducationKeeptrack/issues/17)

---

## 1. Hành vi đã triển khai và kiểm chứng

### T12: Chuẩn bị hướng dẫn chấm và tải ảnh bài nộp riêng tư (#13)
- **Hướng dẫn chấm (Rubric):** Cho phép nhập, cập nhật hướng dẫn chấm cho bài kiểm tra (`Assessments.Rubric`), quản lý phiên bản `RubricVersion`.
- **Lưu trữ ảnh riêng tư trong Cloudflare R2:** Tích hợp `ILearningPhotoStore` (sử dụng S3 API với `BasicAWSCredentials`), lưu ảnh bài nộp dưới prefix `photos/` trong bucket R2 private.
- **Quản lý ảnh theo học sinh:**
  - Entity `SubmissionPhoto` lưu trữ: Id, SubmissionId, PhotoKey, SequenceIndex, ContentType, ByteLength, Sha256, CreatedAt.
  - Hỗ trợ tải nhiều trang ảnh (tối đa 10 trang/bài nộp), kiểm tra định dạng MIME (`image/jpeg`, `image/png`, `image/webp`), giới hạn kích thước ≤ 5MB/ảnh.
  - Sắp xếp thứ tự các trang ảnh (reorder) với index tự động điều chỉnh.
  - Xóa ảnh khỏi database và xóa object trên R2.
- **Bảo mật truy cập:**
  - Token truy cập ảnh có thời hạn (`PhotoTokens`) qua ASP.NET Core Data Protection, không lộ URL hoặc S3 key trực tiếp ra bên ngoài.
  - Endpoint stream ảnh `/api/learning/photos/{token}` kiểm tra token hợp lệ trước khi trả dữ liệu.

### T13: AI chấm bài và giáo viên sửa xác nhận hai mức (#14)
- **Engine AI:** Thiết kế `IAiGradingEngine` với `SimulationAiGradingEngine` hỗ trợ kiểm thử không tốn token và mở rộng sang nhà cung cấp thật (Gemini/DeepSeek).
- **Hạn mức AI Quota:** Tích hợp trực tiếp với `IAiQuota` (`AiTaskKind.Grading`):
  - Giữ lượt (`ReserveAsync`) trước khi xử lý.
  - Trừ lượt (`CommitAsync`) khi đề xuất hợp lệ.
  - Hoàn trả lượt (`ReleaseAsync`) khi AI lỗi hoặc dữ liệu không đủ căn cứ.
- **Hai chỉ số 4 mức:** Đề xuất mức hoàn thành và làm đúng độc lập theo quy tắc nghiệp vụ (0, 25, 50, 75, 100).
- **Quy trình phê duyệt của giáo viên:**
  - Kết quả AI chỉ lưu dưới dạng bản nháp đề xuất (`AiCompletion`, `AiCorrectness`, `AiReasoning`, `AiStatus = 'proposed'`).
  - Giáo viên xem ảnh bài nộp song song với đề xuất, có toàn quyền chỉnh sửa và xác nhận kết quả cuối.
  - Chức năng **AI Fallback** (`ai-fallback`): Cho phép chuyển bài về tự chấm an toàn mà không làm mất dữ liệu lịch sử.

### T16 / T37: AI phác thảo nhận xét và lộ trình trên báo cáo tháng (#16, #41)
- **Engine nhận xét AI:** Thiết kế `IAiCommentEngine` với `SimulationAiCommentEngine`.
- **Tổng hợp dữ liệu nguồn:** Tự động lấy số liệu điểm danh thực tế trong tháng và kết quả các bài kiểm tra đã xác nhận.
- **Tuân thủ quy tắc BR-AI-20:**
  - Độ dài 3–5 câu, giọng văn trung tính, mang tính khích lệ và có gợi ý đồng hành cho gia đình.
  - Nghiêm cấm và tự động loại bỏ các từ tiêu cực ("yếu", "kém", "chậm", "dở", "lười").
- **Hạn mức AI Quota:** Kiểm tra và trừ lượt loại `AiTaskKind.Comment` từ ServiceAccess.
- **UI biên tập:** Giáo viên có thể bấm "AI soạn nhận xét & lộ trình" ngay trên màn hình Báo cáo tháng, xem và chỉnh sửa trước khi lưu bản nháp và xuất PDF.

---

## 2. Kiểm thử và minh chứng

- **Unit & Integration Tests:**
  - `SubmissionPhotosTests.cs`: Kiểm tra cập nhật rubric, upload nhiều trang ảnh, list với signed download URL, xóa và re-index thứ tự.
  - `AiGradingTests.cs`: Kiểm tra ràng buộc phải có ảnh và rubric, đề xuất AI hợp lệ, trừ/hoàn quota, và chuyển về tự chấm an toàn.
  - `AiCommentTests.cs`: Kiểm tra sinh nhận xét trung tính, tổng hợp điểm danh/bài thi, không chứa từ tiêu cực.
- **Giao diện người dùng (UI):**
  - Màn hình Đánh giá (`Learning.tsx`): Nhập rubric, chọn cách chấm AI/tự chấm.
  - Chi tiết bài nộp (`SubmissionForm.tsx`): Tải ảnh, xem thumbnail, đổi thứ tự, modal xem ảnh gốc, nút gọi AI chấm bài và hiển thị đề xuất AI.
  - Màn hình Báo cáo (`ReportDraftForm.tsx`): Nút "AI soạn nhận xét & lộ trình", tích hợp điền tự động vào trường comment và roadmap.
