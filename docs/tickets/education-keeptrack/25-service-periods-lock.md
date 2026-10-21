# T25: Gia hạn, khóa và thực thi quyền sử dụng có hạn

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1 (v1.1).

## What to build

Admin cấp/gia hạn/khóa quyền có lịch sử; giáo viên thấy thời hạn, hết hạn chỉ đọc/xuất, bị khóa không truy cập được.

Lát cắt gồm lưu dữ liệu, API, UI và kiểm thử hành vi; giữ ranh giới Identity/ServiceAccess và tách phí dịch vụ khỏi học phí. Đối chiếu AC-32, AC-33, AC-36, AC-37, AC-38.

## Acceptance criteria

- [ ] Lưu kỳ [bắt đầu,kết thúc), snapshot cấu hình và trạng thái khóa trong module sở hữu; API/UI Admin xem trước gia hạn, lý do và lịch sử.
- [ ] Còn hạn nối kỳ tại cuối hiện tại, hết hạn bắt đầu lúc duyệt; gia hạn sớm không kích hoạt kỳ/hạn mức mới sớm, không tự hoàn tiền.
- [ ] UI giáo viên hiển thị chờ/còn hạn/hết hạn; server kiểm tra mọi thao tác đọc/ghi và quyền tải ảnh/PDF, không dựa cờ client. Hết hạn chặn ghi mới nhưng cho xem/xuất dữ liệu đã có.
- [ ] Khóa thu hồi phiên, chặn dữ liệu cũ, hủy/vô hiệu AI đang chạy qua hợp đồng; mở khóa không tự gia hạn. Admin không có quyền mặc định xem dữ liệu giáo viên.
- [ ] Hết hạn giữa thao tác giữ nội dung form và báo rõ; AI đã nhận cho hoàn tất/lưu nháp, không sửa/xác nhận đến khi gia hạn; metadata xuất kỹ thuật không mở quyền sửa nghiệp vụ.
- [ ] Idle/absolute phiên theo role; UI polling không giữ phiên giả vô thời hạn. Thay đổi cấu hình gói không sửa ngược kỳ; sửa kỳ hiện tại phải có lý do/lịch sử.
- [ ] Test API clock điều khiển, các ranh giới kỳ, hai phiên, thu hồi khi khóa, ghi tranh chấp lúc hết hạn và callback giả về muộn; test UI read-only/khóa/gia hạn. Các module sau phải dùng hợp đồng quyền này.

## Blocked by

- #24: Giáo viên đăng ký và Admin đối chiếu thanh toán để cấp quyền


