# T16: AI phác thảo nhận xét và lộ trình trên báo cáo tháng

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên yêu cầu AI soạn nhận xét ngay trên bản xem trước, chỉnh lời và đưa nội dung đã duyệt vào PDF.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 32, 33, 34, 35; AC-09, AC-23, AC-24.

## Acceptance criteria

- [ ] Lưu lượt sinh nhận xét, snapshot đầu vào và bản AI gốc riêng lời biên tập; dùng điểm danh và kết quả cuối đã xác nhận từ mọi cách chấm.
- [ ] Không gọi lại OCR/chấm ảnh; thiếu dữ liệu được nêu rõ, không tạo lỗi/thành tích thiếu căn cứ.
- [ ] UI có tiến độ/lỗi, sửa/duyệt và chủ động tạo lại; AI lỗi vẫn tự viết/xuất PDF. Không tự gửi phụ huynh.
- [ ] Sửa dữ liệu đánh giá/điểm danh làm nhận xét lỗi thời, không ghi đè lời đã sửa; kết quả nhận xét về cho phiên bản cũ không thay bản hiện hành.
- [ ] Test API mô phỏng AI, dữ liệu chỉ tự chấm, thay nguồn khi đang sinh và giữ lời giáo viên; dựng PDF kiểm tra chỉ nội dung đã duyệt. Nhà cung cấp/chất lượng/dữ liệu AI phải chốt trước vận hành thật.

- [ ] Nhận tác vụ AI phải kiểm tra quyền/hạn mức ServiceAccess và giữ lượt đúng loại/kỳ; thành công trừ, lỗi/hủy hoàn, phản hồi lặp không tính hai lần. Tắt AI/hết hạn cho lượt đã nhận hoàn tất bản nháp; khóa vô hiệu kết quả về muộn. Không tự đặt cách tính lượt cảnh báo chưa được chốt.

## Blocked by

- #12: Soạn nhận xét và xuất báo cáo tháng PDF


- #27: Admin bật tắt AI và giáo viên xem hạn mức theo kỳ

