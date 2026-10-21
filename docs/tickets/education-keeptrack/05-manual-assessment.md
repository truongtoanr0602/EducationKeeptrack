# T05: Tự chấm bài bằng hai chỉ số bốn mức

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên tạo bài đánh giá, chọn tự chấm và ghi/xác nhận hai mức đánh giá trên UI từng học sinh.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 13, 14, 15, 16, 17, 18, 23; AC-03, AC-18, AC-20.

## Acceptance criteria

- [ ] Lưu bài đánh giá, bài nộp duy nhất theo bài–học sinh, cách chấm và lịch sử kết quả cuối tại Learning.
- [ ] UI hai nhóm nút độc lập 25/50/75/100 cho lượng bài hoàn thành và kết quả làm đúng; không nhập phần trăm tự do hoặc điểm 8/10 như chỉ số chính.
- [ ] Không làm bài tính 0 cả hai; không có câu đúng chỉ tính 0 kết quả; không giao/chưa đánh giá loại khỏi trung bình/cảnh báo. Ô trống không tự thành 0.
- [ ] Chọn mặc định cho lượt và đổi riêng bài trước xử lý; tự chấm không gọi AI. Có nhãn AI chưa khả dụng cho đến khi lát cắt AI hoàn tất, không tạo chức năng giả.
- [ ] API sửa/xác nhận lưu lịch sử và phiên bản; UI xem lại được kết quả đã xác nhận. Test API trạng thái đặc biệt, hai tài khoản và tranh chấp; test UI đủ hai nhóm bốn nút.

## Blocked by

- #4: Lập lịch và điểm danh buổi học trực quan

