# T24: Khôi phục mật khẩu và đổi email có thu hồi phiên

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1 (v1.1).

## What to build

Giáo viên quên mật khẩu hoặc đổi email có thể xác minh qua email, đăng nhập lại và giữ quyền dịch vụ đã mua.

Lát cắt gồm lưu dữ liệu, API, UI và kiểm thử hành vi; giữ ranh giới Identity/ServiceAccess và tách phí dịch vụ khỏi học phí. Đối chiếu AC-37.

## Acceptance criteria

- [ ] Lưu token một lần có hạn và phiên thu hồi được; API/UI quên/đặt lại mật khẩu và đổi/xác minh email mới.
- [ ] Đổi email chỉ hiệu lực sau xác minh mới, giữ ID, kỳ và hạn mức; không làm quyền dịch vụ mất theo khóa email.
- [ ] Phản hồi yêu cầu khôi phục không tiết lộ tài khoản tồn tại; token dùng rồi/hết hạn không tái sử dụng.
- [ ] Đặt lại mật khẩu/đổi email thành công thu hồi các phiên cũ ở server; UI yêu cầu đăng nhập lại, chống CSRF cho thao tác từ phiên.
- [ ] Test API với email mô phỏng, hai tài khoản, token lặp/hết hạn, email đã dùng, phiên cũ, ID và quyền trước/sau; test UI lỗi/thành công.
- [ ] Hạn token cụ thể cấu hình khi triển khai; không tự tuyên bố đã có nhà cung cấp email production.

## Blocked by

- #24: Giáo viên đăng ký và Admin đối chiếu thanh toán để cấp quyền


