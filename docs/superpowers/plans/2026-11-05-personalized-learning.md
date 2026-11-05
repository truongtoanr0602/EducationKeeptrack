# Personalized Learning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chạy được luồng kỹ năng → nhận xét/bài kiểm tra → theo dõi buổi sau → kế hoạch cá nhân hóa được giáo viên duyệt cho lớp Toán 2 học kỳ 1.

**Architecture:** Mở rộng Learning trong Modular Monolith. Classroom cung cấp dữ liệu qua contract; ServiceAccess giữ quyền và hạn mức. AI dùng adapter riêng; thiếu cấu hình AI thì tính năng thủ công vẫn hoạt động, không giả lập đầu ra thành AI thật.

**Tech Stack:** .NET 10, EF Core/Npgsql, PostgreSQL 18, React 19, TypeScript, Vite, xUnit/API tests và Playwright theo repository.

**Spec:** `docs/Bao_cao_tong_hop_ca_nhan_hoa_Toan_2_HK1.md` và `docs/Bao_cao_kich_ban_AI_EducationKeeptrack.md`.

**Trạng thái thực thi 05/11/2026:** Đã triển khai phần nền tảng thủ công, UI, migrations và kế hoạch có duyệt; 28 API tests và 4 E2E tests desktop/mobile đã qua. AI thật chưa bật vì thiếu provider và chính sách lượt. Việc kiểm tra lại hiện theo ngày, chưa gắn lịch tham dự/hoãn nhiệm vụ. Xem [nhật ký thực thi](2026-11-05-personalized-learning-progress.md) và [hướng dẫn demo](../../Huong_dan_demo_ca_nhan_hoa.md) để biết phạm vi thực tế; checklist dưới là kế hoạch gốc.

## Global Constraints

- Lớp 1–2 học sinh, bộ sách theo lớp; không truy vấn bảng module khác.
- Ba màu là quan sát nhanh; không quy đổi Xanh thành đạt 80%.
- Thiếu dữ liệu/vắng không là điểm 0; chỉ kết quả được xác nhận làm bằng chứng.
- Ngưỡng demo: hai lần hợp lệ gần nhất, mỗi lần ít nhất năm nhiệm vụ, mỗi lần đạt ít nhất 80%. Quy tắc lựa chọn hai lần này là đề xuất cần duyệt trong kế hoạch.
- Kế hoạch tối đa hai kỹ năng/em và không vượt quỹ thời gian; AI chỉ lưu dự thảo.
- Chưa chốt hạn mức tác vụ AI: không bật gọi AI thật cho tài khoản cho đến khi có chính sách riêng; không dùng hạn mức loại khác hoặc mặc định miễn phí.
- Không sửa hoặc đưa các tài liệu/hình chưa được theo dõi của người dùng vào commit triển khai.

## Review Focus

- Truy cập lớp/học sinh ngoài phạm vi giáo viên trả 404; kiểm tra cả GET và POST.
- Ngày nhận xét không thay thế ngày buổi cũ; vắng không tạo nhận xét mới.
- Cập nhật version cũ trả 409, giữ dữ liệu biểu mẫu.
- Hai bài ngắn ba câu không được tính đạt ngưỡng năm nhiệm vụ.
- AI lỗi/dữ liệu nguồn đổi không ghi đè bản giáo viên đã sửa hoặc đã duyệt.

## Task 1: Kỹ năng theo lớp và nền tảng lưu trữ

**Files:** tạo `src/Modules/Learning/Domain/ClassLearningProfile.cs`, `Application/PersonalizedLearningService.cs`, `Infrastructure/PersonalizedLearningRepository.cs`; sửa `LearningDatabase.cs`, `LearningModule.cs`; thêm migration và `tests/ApiTests/PersonalizedLearningTests.cs`.

**Interfaces:** API `GET/PUT /api/learning/classes/{classId}/profile`; dữ liệu gồm textbook, semester, skills (id, title, taught), version. Dùng `IClassroomLearningSource.ReadRosterAsync` xác minh phạm vi lớp trước xử lý.

- [ ] Viết và chạy test thất bại: giáo viên lưu/đọc lại cấu hình sách và kỹ năng, tài khoản khác bị từ chối, bản cũ trả 409.
- [ ] Implement domain, repository, route kiểm tra quyền đọc/ghi và migration; không dùng EnsureCreated.
- [ ] Chạy `dotnet test tests/ApiTests --filter FullyQualifiedName~PersonalizedLearningTests` với PostgreSQL test chuyên dụng; kỳ vọng pass.

## Task 2: Nhận xét, bằng chứng và việc kiểm tra lại

**Files:** tạo `Domain/SkillObservation.cs`, `Domain/SkillEvidence.cs`, `Application/SkillProgress.cs`; mở rộng service/repository/routes Task 1 và tests.

**Interfaces:** `POST /api/learning/classes/{classId}/observations` (studentId, skillId, date, level red/amber/green, note); `POST .../evidence` (studentId, skillId, date, total, correct, confirmed); `GET .../progress?date=YYYY-MM-DD` trả latestObservation, evidence, mastery và pendingReview. Nhật ký chỉ thêm để bảo toàn lịch sử; idempotency key cho thao tác tạo khi retry.

- [ ] Viết test thất bại: Xanh không tạo mastery, một 5/5 chưa đủ, hai 4/5 và 5/5 đạt, hai 3/3 chưa đủ; không nhận correct > total.
- [ ] Viết test quyền học sinh, kỹ năng chưa dạy, thiếu/vắng không tạo điểm; mục Đỏ còn mở sang lần xem buổi sau.
- [ ] Implement nhận xét/bằng chứng riêng, quy tắc tính mức đạt và danh sách việc cần theo dõi; ngày nghiệp vụ Việt Nam.
- [ ] Chạy lại test tập trung; kỳ vọng pass trước chuyển UI.

## Task 3: Giao diện giáo viên và demo xuyên buổi

**Files:** tạo `ui/src/PersonalizedLearning.tsx`, `ui/src/personalized-learning-api.ts`; sửa `ui/src/Learning.tsx`, `ui/src/style.css`; tạo `ui/e2e/personalized-learning.spec.ts` theo vị trí/config E2E thực tế của repo.

**Interfaces:** tiêu thụ các route Tasks 1–2; component nhận classroom và me giống Learning hiện tại.

- [ ] Viết E2E kiểm tra cấu hình lớp, lưu hai em khác màu, tải lại vẫn còn, đổi ngày và xem việc ôn; kiểm tra quyền hết hạn chỉ đọc.
- [ ] Implement ba vùng: cấu hình kỹ năng, ghi nhận buổi hiện tại, tiến bộ/việc ôn; nhãn chữ đi kèm màu, không chọn sẵn Xanh.
- [ ] Kiểm tra lỗi/409 giữ input và hiển thị reload; không lưu dữ liệu xác thực trong localStorage.
- [ ] Chạy `npm run build` và Playwright test riêng; kỳ vọng desktop/mobile pass.

## Task 4: Kế hoạch cá nhân hóa và AI adapter

**Files:** tạo `Domain/PersonalizedPlan.cs`, `Application/PlanService.cs`, `Application/IPlanGenerator.cs`, `Infrastructure/PlanWorker.cs`; thêm UI kế hoạch, migrations và `tests/ApiTests/PersonalizedPlanTests.cs`.

**Interfaces:** `POST .../plans` tạo bản nháp thủ công; `POST .../plans/generate` tiếp nhận AI nếu được cấp quyền/hạn mức riêng; `GET .../plans/{id}`; `PUT .../plans/{id}` kèm version; `POST .../plans/{id}/approve` kèm version.

- [ ] Test snapshot đầu vào, thời lượng hợp lệ, tối đa hai ưu tiên/em, sửa version cũ bị chặn và AI không tự approve.
- [ ] Implement kế hoạch thủ công đầy đủ trước; adapter/worker bảo đảm lỗi không ảnh hưởng nội dung đã lưu.
- [ ] Chốt chính sách loại lượt và nhà cung cấp/model trước bật tích hợp thật; không tự chọn chính sách thu phí hoặc gọi tài khoản ngoài.
- [ ] Test tác vụ bị từ chối khi chưa cấu hình/quyền, retry không trừ trùng, khóa vô hiệu kết quả về muộn theo ADR-0003.

## Task 5: Xác minh và chạy demo

- [ ] Chạy các test Learning hiện có và test cá nhân hóa, kiểm tra migration trên database phát triển riêng.
- [ ] Chạy frontend build và E2E xuyên buổi, kiểm tra hai màu khác nhau sau reload và bằng chứng hai lần.
- [ ] Chuẩn bị dữ liệu giả An/Bình theo báo cáo bằng thao tác phát triển có chủ đích; không sửa lớp thật.
- [ ] Báo cáo URL, cách đăng nhập an toàn và trạng thái chính xác của adapter AI; không gọi demo thủ công là AI đã hoạt động.

## Runtime hiện tại

PostgreSQL cục bộ dùng port 55432; database phát triển mới `ekt_personalization_dev`. Chuỗi kết nối nằm trong `.local/personalization-connection` (không commit). Frontend port 5173, backend port 5080. Docker hiện chưa chạy nên môi trường này dùng PostgreSQL bản native.

## Phương thức thực hiện

Đề xuất thực hiện trực tiếp trong phiên hiện tại, theo từng task và test tương ứng. Chưa tạo worktree; không di chuyển file đang mở hoặc tài liệu chưa commit. Sau khi duyệt kế hoạch, có thể làm trên nhánh tính năng tại workspace hiện tại hoặc worktree nếu người dùng muốn.
