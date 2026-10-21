# T23: Giáo viên đăng ký và Admin đối chiếu thanh toán để cấp quyền

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1 (v1.1).

## What to build

Giáo viên đăng ký email/mật khẩu, xác minh, xem hướng dẫn thanh toán và gửi đối chiếu; Admin duyệt/từ chối và cấp kỳ đầu từ cùng hồ sơ.

Lát cắt gồm lưu dữ liệu, API, UI và kiểm thử hành vi; giữ ranh giới Identity/ServiceAccess và tách phí dịch vụ khỏi học phí. Đối chiếu AC-31, AC-32, AC-38.

## Acceptance criteria

- [ ] Identity lưu tài khoản/email xác minh; ServiceAccess lưu hồ sơ duyệt, yêu cầu đối chiếu, cấu hình hướng dẫn/liên hệ và kỳ đầu. Không ghi phí dịch vụ vào Billing học sinh.
- [ ] UI đăng ký/xác minh/chờ/từ chối/hướng dẫn và gửi mã giao dịch/ghi chú; server chặn nghiệp vụ lớp trước xác minh và duyệt.
- [ ] UI Admin cấu hình hướng dẫn, đối chiếu tiền bên ngoài, ghi thông tin, duyệt/từ chối có lý do; từ chối không xóa tài khoản.
- [ ] Admin xem trước ngày bắt đầu/kết thúc và cấu hình kỳ trước cấp; không tự suy gói từ số tiền, chưa có trial/cổng tự thanh toán.
- [ ] Gửi lại/duyệt đồng thời một yêu cầu không cấp trùng kỳ; lưu lịch sử người/thời gian/lý do và kiểm soát phiên bản.
- [ ] Test API database thật và email adapter mô phỏng: xác minh sai/hết hạn, giáo viên đổi role/ID, yêu cầu chuyển khoản chưa được duyệt, duyệt lặp, tách phí dịch vụ/học phí; test UI tất cả trạng thái.
- [ ] Tích hợp gửi email thật cần cấu hình môi trường trước public launch; thông tin gói/giá/độ dài kỳ không được tự coi là chính sách đã duyệt.

## Blocked by

- #23: Admin đăng nhập hai bước và xem hồ sơ đăng ký


