# T01: Đăng nhập và tạo lớp riêng của giáo viên

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên đăng nhập, tạo lớp và xem lại danh sách lớp riêng trên UI sau khi tải lại trang.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 1, 2, 50; AC-01, AC-30.

## Acceptance criteria

- [ ] Lưu tài khoản và lớp có phạm vi sở hữu; API lấy danh tính từ phiên xác thực, không tin ID giáo viên client gửi.
- [ ] UI đăng nhập/đăng xuất, danh sách và tạo lớp hoạt động end-to-end; hai giáo viên không đọc/sửa lớp của nhau bằng ID trực tiếp.
- [ ] Khởi tạo .NET Modular Monolith với Identity/Classroom có schema và DbContext riêng; Dùng React + TypeScript + Vite, PostgreSQL và ASP.NET Core Identity/email-mật khẩu/cookie HttpOnly cùng origin theo quyết định đã chốt; chọn phiên bản tương thích khi triển khai.
- [ ] Có Docker Compose chạy UI/backend/dữ liệu với volume; pipeline Jenkins build và chạy kiểm thử của lát cắt, secrets ngoài mã nguồn.
- [ ] Kiểm thử API với dữ liệu thật, phiên hết hạn/không hợp lệ và hai tài khoản; demo tạo lớp và tải lại UI. Các lát cắt sau mở rộng nền này, không dựng module rỗng trước.

- [ ] Giáo viên idle 2 giờ/absolute 24 giờ; Admin 30 phút/8 giờ; CSRF cho ghi, cookie Secure khi triển khai HTTPS, phiên server có thể thu hồi. Không lưu token ở localStorage hoặc tự bật Google login từ mockup.
- [ ] Chuẩn bị hợp đồng kiểm tra quyền đọc/ghi/AI theo tài khoản; không coi đăng nhập thành công là đã được duyệt dịch vụ. Các luồng đăng ký/phê duyệt/kỳ/hạn mức được hoàn thiện ở ticket bổ sung trước khi mở đăng ký production.
- [ ] Khi dựng UI từ Stitch, bỏ QA, số liệu tự sinh và các lời hứa ngoài spec; đồng nhất nhãn/mô tả/zoom theo checklist thiết kế mới nhất. Không coi HTML prototype là xác thực hoặc lưu thật.

## Blocked by

None (can start immediately).


