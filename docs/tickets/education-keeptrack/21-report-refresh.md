# T21: Nhận biết PDF lỗi thời và xuất bản cập nhật

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên thấy PDF nào đã lỗi thời sau sửa dữ liệu, xem thay đổi và xuất/gửi lại bản mới từ UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 49; AC-27, AC-29.

## Acceptance criteria

- [ ] Lưu snapshot phiên bản nguồn và quan hệ các bản xuất; API/UI liệt kê lịch sử, thời điểm và cảnh báo cần cập nhật khi nguồn thay đổi.
- [ ] Thay tiền nhận/điểm danh/kết quả hoặc nội dung liên quan vô hiệu snapshot hiện hành qua hợp đồng sự kiện chung; tiêu thụ được thay đổi từ các lát cắt khác khi chúng tồn tại.
- [ ] Bản mới lấy phí/nhận xét/QR theo dữ liệu hiện hành; không dùng mã cũ sai tiền, không sửa âm thầm file đã gửi; thiếu QR vẫn có fallback.
- [ ] Nguồn đổi lúc xuất thì từ chối bản không nhất quán hoặc yêu cầu tạo lại; chỉ dữ liệu được duyệt đi vào PDF; không tự ghi đè lời giáo viên hay gửi phụ huynh.
- [ ] Test API nguồn thay trong khi render, sự kiện lặp, bản cũ/mới và kiểm tra PDF thật trên UI. Chính sách khóa/mở lại báo cáo đã chốt còn cần xác nhận; không tự thêm điều kiện khóa.

## Blocked by

- #12: Soạn nhận xét và xuất báo cáo tháng PDF

