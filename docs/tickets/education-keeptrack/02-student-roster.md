# T02: Thêm học sinh và quản lý danh sách theo học

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên thêm học sinh, phụ huynh và cập nhật tình trạng theo học trên màn hình lớp.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 3, 4, 8; AC-01, AC-30.

## Acceptance criteria

- [ ] Lưu hồ sơ, liên hệ và đăng ký lớp có ngày bắt đầu/kết thúc; API và UI thêm/xem/sửa đồng nhất.
- [ ] Ngăn đăng ký trùng đang hiệu lực; chuyển đang học/nghỉ có phép/dừng học giữ lịch sử.
- [ ] Chỉ chọn học sinh thuộc giáo viên hiện tại; tham chiếu chéo tài khoản bị từ chối.
- [ ] UI báo dữ liệu sai/xung đột bằng tiếng Việt và giữ dữ liệu nhập chưa lưu.
- [ ] Kiểm thử API đăng ký trùng, sửa trạng thái và truy cập chéo; tải lại UI vẫn đúng danh sách. Học sinh nhiều lớp chưa chốt, không tự chuyển dư hoặc bảo lưu giữa lớp.

- [ ] Mọi truy cập nghiệp vụ phải kiểm tra ServiceAccess: chưa xác minh/chờ duyệt không truy cập lớp; hết hạn chỉ đọc/xuất dữ liệu cũ; khóa không truy cập. Admin không mặc định đọc dữ liệu học sinh. UI thể hiện đúng quyền, server vẫn là nơi quyết định.

## Blocked by

- #2: Đăng nhập và tạo lớp riêng của giáo viên


- #26: Gia hạn, khóa và thực thi quyền sử dụng có hạn

