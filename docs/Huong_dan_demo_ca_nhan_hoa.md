# Hướng dẫn demo cá nhân hóa Toán lớp 2

## 1. Phạm vi đã triển khai

- Cấu hình bộ sách, học kỳ và danh sách kỹ năng theo lớp, đánh dấu đã dạy.
- Ghi nhận nhanh Đỏ/Vàng/Xanh theo học sinh, kỹ năng và ngày; lưu lịch sử riêng.
- Nhập kết quả bài kiểm tra chung hoặc riêng theo kỹ năng; xác nhận bằng chứng độc lập.
- Tính mức đạt từ hai lần hợp lệ gần nhất, mỗi lần ít nhất năm nhiệm vụ và đạt 80% trở lên.
- Hiển thị việc cần kiểm tra lại từ nhận xét Đỏ/Vàng của ngày trước, xem lịch sử và nguồn bằng chứng.
- Tạo khung ôn tập bằng quy tắc đơn giản từ dữ liệu; giao diện ghi rõ không dùng AI.
- Tạo, chỉnh sửa và duyệt kế hoạch thủ công; tối đa hai ưu tiên mỗi em, kiểm tra tổng phút hoạt động, giữ lịch sử và snapshot nguồn.
- Chặn duyệt nếu dữ liệu nguồn thay đổi; phải xem lại và lưu dự thảo mới.

## 2. Cách xem demo cục bộ

1. Mở http://127.0.0.1:5173 và đăng nhập tài khoản demo đã được cung cấp trong cuộc trao đổi. Mật khẩu không ghi vào tài liệu hoặc Git.
2. Chọn lớp **DEMO - Toán 2 HK1 - An và Bình**.
3. Trong phần **Đánh giá học tập**, mở **Cá nhân hóa học tập**.
4. Chọn ngày 05/11/2026 để xem việc ôn nối tiếp các quan sát ngày 04/11.
5. Xem An cần hỗ trợ cộng qua 10, Bình cần củng cố cộng có nhớ; mở lịch sử để xem điểm và nhận xét riêng.
6. Chọn nhận xét sau quan sát hoặc nhập kết quả kiểm tra thực sự mới. Nhấn **Bắt đầu bài kiểm tra mới** khi có một lần làm khác, không dùng nút này để nhân đôi cùng một bài.
7. Dùng **Tạo khung từ dữ liệu theo dõi (không dùng AI)** hoặc mở sửa bản kế hoạch mẫu. Bổ sung học liệu, mục tiêu và nội dung trước khi lưu/duyệt.
8. Thay đổi một kết quả hoặc cấu hình sách rồi thử duyệt bản cũ: hệ thống yêu cầu xem lại nguồn. Mở sửa và lưu bản nháp để cập nhật căn cứ trước duyệt.

Dữ liệu An/Bình là dữ liệu giả. Bộ sách mẫu phải được giáo viên cấu hình và thẩm định lại trước dùng thật. Thao tác demo có lưu database; dùng lớp mẫu để tránh sửa dữ liệu lớp thật.

## 3. Những điểm chưa bật/chưa triển khai

- Chưa gọi nhà cung cấp AI thật; chưa có model, khóa cấu hình và chính sách hạn mức tạo kế hoạch. Endpoint yêu cầu AI trả lỗi rõ ràng. Interface adapter đã có nhưng không đăng ký provider hoặc worker giả.
- Danh sách kiểm tra lại hiện theo ngày; chưa có lịch nhắc gắn buổi tham dự thực tế, hoãn có ngày hẹn hoặc trạng thái nhiệm vụ độc lập. Giáo viên chọn ngày xem, không tự sinh điểm 0 khi vắng.
- Chưa có thư viện câu hỏi/học liệu được quản lý đầy đủ hoặc chấm bài tự động; giáo viên nhập số nhiệm vụ và số đúng theo kỹ năng.
- Chưa có ngưỡng tùy chỉnh, báo cáo cuối khóa riêng, tự đọc ảnh bài làm hay triển khai production.
- Kết quả kỹ năng đang là nhật ký thêm mới; bản nháp không được tính đạt và chưa có luồng sửa/xác nhận lại một bản nháp cũ. Chỉ xác nhận lúc ghi khi kết quả đã được giáo viên kiểm tra.

## 4. API chính

Tất cả nằm dưới `/api/learning/classes/{classId}`, yêu cầu giáo viên sở hữu lớp và quyền đọc/ghi phù hợp. POST/PUT yêu cầu CSRF như các API hiện có.

| Route | Chức năng |
|---|---|
| GET/PUT `/profile` | Cấu hình chương trình và kỹ năng, kiểm tra version |
| POST `/observations` | Thêm nhận xét, requestId chống trùng |
| POST `/evidence` | Thêm bằng chứng, requestId chống trùng |
| GET `/progress?date=YYYY-MM-DD` | Xem mức đạt, lịch sử, việc cần ôn và học sinh theo ngày |
| GET/POST `/plans` | Danh sách/tạo dự thảo |
| GET/PUT `/plans/{id}` | Xem/sửa dự thảo, kiểm tra version |
| POST `/plans/{id}/approve` | Duyệt có kiểm tra dữ liệu nguồn |
| POST `/plans/generate` | Chưa bật, trả 503 với thông báo cấu hình còn thiếu |

## 5. Chạy và kiểm tra

Môi trường phát triển dùng PostgreSQL 18 port 55432, database riêng `ekt_personalization_dev`; backend port 5080 và Vite port 5173. Chuỗi kết nối và mật khẩu chỉ lưu trong thư mục `.local` bị Git bỏ qua.

Chạy migration theo cơ chế hiện có: `dotnet run --project src/Host -- --migrate`, với `ConnectionStrings__Database` được cấu hình ở môi trường. Không tự migrate mỗi lần khởi động và không dùng EnsureCreated.

Kiểm tra backend trên PostgreSQL test chuyên dụng:

```powershell
dotnet test tests/ApiTests --filter 'FullyQualifiedName~PersonalizedLearningTests|FullyQualifiedName~PersonalizedPlanTests|FullyQualifiedName~AssessmentTests'
```

Kiểm tra UI, với DEMO_EMAIL/DEMO_PASSWORD được cấu hình trong môi trường:

```powershell
cd ui
npm run build
npx playwright test e2e/personalized-learning.spec.ts e2e/assessment.spec.ts --workers=1
```

Các kiểm thử trình duyệt tạo lớp/học sinh kiểm thử riêng trong database demo. Không chạy chúng bằng tài khoản lớp thật.
