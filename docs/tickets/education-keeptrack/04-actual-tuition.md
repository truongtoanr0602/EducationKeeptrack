# T04: Xem học phí theo buổi thực học và đơn giá hiệu lực

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên đặt đơn giá và mở bảng phí tháng có từng buổi làm căn cứ đối chiếu.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 6, 36; AC-05, AC-06.

## Acceptance criteria

- [ ] Lưu đơn giá theo ngày hiệu lực và phí từng buổi tại Billing; Classroom chỉ cung cấp hợp đồng/sự kiện, không đọc trực tiếp schema khác.
- [ ] API/UI tính tổng đơn giá các buổi thực tế có mặt; vắng/chưa điểm danh/hủy/lịch dự kiến không sinh phí; học bù tính một lần.
- [ ] 8 buổi x 150.000 đồng cho 1.200.000 đồng; thay giá giữa tháng giữ đúng đơn giá từng buổi cũ.
- [ ] Sửa điểm danh cập nhật tổng hợp có phiên bản; sự kiện phát bền vững và xử lý lặp an toàn, có trạng thái chờ cập nhật ở UI.
- [ ] Kiểm thử API với dữ liệu thật: đổi giá, sự kiện lặp/dừng tiến trình rồi khôi phục, không tính trùng; UI tải lại bảng phí khớp dữ liệu.

## Blocked by

- #4: Lập lịch và điểm danh buổi học trực quan

