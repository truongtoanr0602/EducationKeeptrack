# T15: Sửa hướng dẫn và chủ động chấm lại bài AI

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên sửa đáp án, thấy danh sách bài dùng bản cũ và chọn bài cần chấm lại trên UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 27, 28; AC-15, AC-22.

## Acceptance criteria

- [ ] Lưu hướng dẫn phiên bản mới không sửa bản cũ; API/UI đánh dấu bài dựa phiên bản cũ, không tự gọi AI.
- [ ] Chọn từng bài chấm lại; với kết quả giáo viên đã sửa phải cảnh báo/xác nhận thay thế trước tạo lượt.
- [ ] Giữ kết quả chính thức cũ tới khi xác nhận bản thay thế, có nhãn đang chấm lại/cần cập nhật; lưu gốc và lịch sử.
- [ ] Không tạo đầu điểm mới; kết quả chỉ được áp dụng nếu ID lượt/cách chấm/phiên bản còn hiện hành.
- [ ] Test API sửa hướng dẫn dùng chung nhiều học sinh, từ chối thiếu xác nhận, retry/callback cũ và bảo toàn kết quả; UI nhận biết rõ phiên bản.

## Blocked by

- #14: AI chấm bài và giáo viên sửa xác nhận hai mức

