# T22: Admin đăng nhập hai bước và xem hồ sơ đăng ký

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1 (v1.1).

## What to build

Admin được thiết lập riêng, đăng nhập với TOTP và xem hồ sơ tài khoản giáo viên trong phạm vi quản lý dịch vụ.

Lát cắt gồm lưu dữ liệu, API, UI và kiểm thử hành vi; giữ ranh giới Identity/ServiceAccess và tách phí dịch vụ khỏi học phí. Đối chiếu AC-38.

## Acceptance criteria

- [ ] Lưu vai trò, cấu hình TOTP và phiên Admin; UI/API đăng nhập yêu cầu cả mật khẩu và mã ứng dụng, idle 30 phút/absolute 8 giờ.
- [ ] Admin đầu tiên được tạo qua bước thiết lập riêng với secrets ngoài mã; không có đăng ký/endpoint công khai nâng vai trò Admin.
- [ ] UI danh sách/chi tiết hồ sơ giáo viên; không có quyền mặc định đọc lớp, ảnh bài, PDF hay sổ học phí của giáo viên.
- [ ] Các hành động Admin được kiểm tra role và CSRF ở server; có actor/thời điểm trong lịch sử, không chỉ ẩn nút UI.
- [ ] Test API database thật với Admin chưa 2FA, mã sai, phiên hết hạn, giáo viên gọi API Admin và Admin gọi API dữ liệu học sinh; demo đăng nhập và xem hồ sơ.
- [ ] Quy trình dự phòng mất thiết bị TOTP cần xác nhận trước production, không tự bỏ 2FA hoặc tạo backdoor.

## Blocked by

- #2: Đăng nhập và tạo lớp riêng của giáo viên


