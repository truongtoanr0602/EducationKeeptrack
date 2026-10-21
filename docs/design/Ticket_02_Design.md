# Ticket #2 — Đăng nhập, danh sách và tạo lớp

## Phạm vi được giao

Người dùng đồng ý hoàn thiện thiết kế Stitch cho ticket #2 trước khi triển khai ứng dụng. Nguồn: project `7871005430122021919`, [ticket #2](https://github.com/truongtoanr0602/EducationKeeptrack/issues/2), spec Education Keeptrack 1.0.

Kết quả cần có là bộ mẫu UI để xem xét trên điện thoại và máy tính, không phải chức năng xác thực/API đã hoạt động. Giữ nguyên mẫu cũ để đối chiếu; các bản mới là thiết kế cần duyệt.

## Hành trình

Đăng nhập → Lớp học của tôi → Tạo lớp → Lưu → Lớp mới xuất hiện trong danh sách. Đăng xuất trở về đăng nhập; phiên hết hạn yêu cầu đăng nhập lại.

## Thiết kế lựa chọn

- Tiếp nối phong cách project hiện có: xanh đậm, nền sáng, accent xanh ngọc tiết chế; font Literata/Manrope. Đây là cách giữ nhất quán trong lượt thiết kế, chưa bổ sung quyết định thương hiệu toàn sản phẩm.
- Đăng nhập dùng nhãn trung tính “Tài khoản” và “Mật khẩu”, hiện/ẩn mật khẩu, nút đăng nhập. Cách xác thực thực tế còn cần quyết định khi triển khai.
- Danh sách chỉ hiển thị tên lớp, môn học và mô tả; có nút tạo lớp và đăng xuất. Không bịa số học sinh, học phí hay lịch khi các tính năng đó chưa triển khai.
- Form tạo lớp gồm tên và môn bắt buộc, mô tả tùy chọn; lưu/hủy; về danh sách sau lưu thành công. Đây là ràng buộc form đề xuất để xem xét thiết kế.
- Bỏ Google login, dùng thử 30 ngày, hotline, AES-256 và các lời hứa chưa có trong spec; icon chức năng tối giản.

## Trạng thái cần đối chiếu

| Màn hình | Trạng thái |
| --- | --- |
| Đăng nhập | Bình thường, đang xác thực, sai thông tin, phiên hết hạn |
| Danh sách lớp | Có lớp, chưa có lớp, đang tải, tải lỗi/thử lại |
| Tạo lớp | Form mới, thiếu trường, đang lưu, lưu lỗi/thử lại, thành công |
| Đăng xuất | Hành động rõ và quay lại đăng nhập |

## Tiêu chí rà soát

Có screenshot/HTML từ Stitch, nội dung tiếng Việt, bố cục desktop/mobile dễ đọc và không tràn ngang. Nhãn đi kèm icon, focus rõ, cho phép zoom. Các trạng thái không được giả định có chỉ vì prompt đã yêu cầu; kiểm tra sản phẩm trả về và ghi thiếu nếu cần bổ sung.

Tạo thiết kế không hoàn thành ticket #2: dữ liệu, API, phân quyền hai giáo viên, Docker và kiểm thử tích hợp vẫn thuộc bước triển khai sau. Không tự đánh dấu đã duyệt thiết kế hoặc đóng GitHub issue.
