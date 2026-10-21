# T03: Lập lịch và điểm danh buổi học trực quan

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên thiết lập lịch tuần, mở buổi thực tế và điểm danh ngay trên danh sách học sinh.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 5, 7, 9, 10, 11, 12; AC-01, AC-02.

## Acceptance criteria

- [ ] Lưu lịch nhiều ngày/giờ trong tuần, buổi dự kiến/đã diễn ra/hủy; tạo học bù không trùng buổi và không sửa lịch sử khi đổi lịch.
- [ ] Buổi mới mặc định chưa điểm danh; API lưu trạng thái theo cặp buổi–học sinh duy nhất, lý do vắng và nghỉ có phép.
- [ ] UI dòng có mặt nổi bật; toàn dòng vắng bôi xám còn đọc được, có nhãn chữ; sửa trạng thái được.
- [ ] UI liệt kê/nhắc điểm danh còn thiếu trước tổng kết; thời gian buổi xác định theo Asia/Ho_Chi_Minh.
- [ ] Kiểm thử API buổi hủy/học bù/điểm danh trùng và hai tab sửa cùng phiên bản; kiểm tra UI màu, nhãn, toàn dòng xám.

## Blocked by

- #3: Thêm học sinh và quản lý danh sách theo học

