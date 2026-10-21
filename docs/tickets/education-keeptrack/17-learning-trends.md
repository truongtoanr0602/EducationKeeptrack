# T17: Xem biểu đồ hai chỉ số và độ đầy đủ dữ liệu

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên mở hồ sơ học sinh, xem biểu đồ mức hoàn thành và làm đúng theo thời gian.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 29; AC-17, AC-18.

## Acceptance criteria

- [ ] Lưu projection phiên bản hồ sơ theo kết quả cuối/điểm danh; API qua hợp đồng module, xử lý sự kiện lặp an toàn.
- [ ] UI hai chuỗi độc lập và khoảng ngày, nhãn cách chấm; nhóm phần trăm không được mô tả thành tỷ lệ chính xác.
- [ ] Không làm/không câu đúng tính 0 đúng chỉ số; không giao/chưa đánh giá loại mẫu, hiện độ đầy đủ.
- [ ] Chưa đủ dữ liệu hiển thị rõ, không suy sa sút do thiếu dữ liệu hoặc nghỉ có phép; ngưỡng cảnh báo thuộc lát cắt riêng.
- [ ] Test API thay kết quả xác nhận rồi cập nhật projection, loại 0/thiếu đúng, giới hạn quyền và UI biểu đồ.

## Blocked by

- #6: Tự chấm bài bằng hai chỉ số bốn mức

