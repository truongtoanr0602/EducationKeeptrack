# T26: Admin bật tắt AI và giáo viên xem hạn mức theo kỳ

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1 (v1.1).

## What to build

Admin cấp quyền/hạn mức AI, giáo viên thấy lượt còn/giữ/đã dùng và tiếp tục làm thủ công nếu không có AI.

Lát cắt gồm lưu dữ liệu, API, UI và kiểm thử hành vi; giữ ranh giới Identity/ServiceAccess và tách phí dịch vụ khỏi học phí. Đối chiếu AC-34, AC-35, AC-36, AC-38.

## Acceptance criteria

- [ ] Lưu cờ AI riêng tài khoản và snapshot hạn mức theo kỳ/từng loại chấm bài, sinh nhận xét; UI/API Admin bật/tắt/cấp/điều chỉnh có lý do và lịch sử.
- [ ] UI giáo viên hiển thị còn dùng/đang giữ/đã dùng; tắt AI hoặc hết lượt không chặn nghiệp vụ thủ công trong kỳ còn hạn, giữ kết quả AI cũ.
- [ ] API nhận AI yêu cầu đã xác minh/duyệt, không khóa, còn kỳ, AI bật và đủ hạn mức; thiếu cấu hình không tự cấp lượt. Cảnh báo AI bị tắt cùng cờ, biểu đồ mô tả vẫn đọc được.
- [ ] Giữ lượt nguyên tử trước gửi, chỉ trừ khi kết quả hợp lệ, hoàn khi lỗi/hủy; khóa chống lặp, không vượt hạn mức khi đồng thời. Lượt thuộc kỳ nhận kể cả hoàn tất sau ranh giới.
- [ ] Tắt/bật không reset; kỳ mới không kế thừa lượt dư, gia hạn sớm không cấp sớm. Chấm lại/tạo lại thành công là lượt mới; thay gói không sửa ngược kỳ.
- [ ] Hết hạn/tắt AI cho lượt đã nhận hoàn tất/lưu nháp; khóa hủy/vô hiệu callback và hoàn giữ. Callback không tự xác nhận kết quả hoặc ghi đè lời sửa.
- [ ] Test API database thật + adapter AI mô phỏng: đồng thời, lặp, sai cấu trúc, lỗi/hủy/restart, đổi kỳ/tắt/khóa và callback muộn; test UI quyền bật/tắt và số dư.
- [ ] Giá/số lượt gói và đơn vị tính lượt cảnh báo AI chưa chốt; không tự thu phí cảnh báo, phần cảnh báo thật phải có quyết định bổ sung.

## Blocked by

- #26: Gia hạn, khóa và thực thi quyền sử dụng có hạn


