# T08: Điều chỉnh tăng giảm học phí có lý do

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên thêm khoản điều chỉnh và thấy dư nợ thay đổi có giải thích trên bảng phí.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 37, 46; AC-29.

## Acceptance criteria

- [ ] Lưu khoản điều chỉnh có chiều tăng/giảm, số tiền chính xác, lý do/người/thời điểm; API bắt buộc lý do và có phiên bản.
- [ ] UI thêm điều chỉnh và xem lịch sử; không sửa trực tiếp tổng tiền trên bản xem trước.
- [ ] Tính lại nghĩa vụ và phân bổ bị ảnh hưởng một cách nguyên tử; không dư nợ âm, phần dư ghi riêng chờ chính sách.
- [ ] Phát thay đổi phiên bản bảng phí để yêu cầu thanh toán/QR/báo cáo tiêu thụ được; không gọi trực tiếp module khác để sửa dữ liệu.
- [ ] Test API giảm phí dưới số đã thu, tăng phí sau đủ tiền, khóa chống lặp/tranh chấp; UI sau tải lại hiển thị nguồn và tổng khớp.

## Blocked by

- #7: Ghi nhận thanh toán một phần và chuyển nợ cũ

