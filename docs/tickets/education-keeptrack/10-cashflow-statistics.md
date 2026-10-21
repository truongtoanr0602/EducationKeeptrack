# T10: Thống kê tiền thực thu và công nợ theo kỳ học

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên chuyển giữa báo cáo tiền theo ngày nhận và bảng công nợ theo kỳ trên UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 40; AC-26.

## Acceptance criteria

- [ ] Lưu/đọc dữ liệu phân bổ và phiên bản tổng hợp theo hợp đồng Billing; API lọc khoảng ngày thực nhận hoặc kỳ nghĩa vụ rõ ràng.
- [ ] Nhận 500.000 ngày 05/11 trả nợ tháng 10: thực thu tháng 11 tăng 500.000, nợ tháng 10 giảm 500.000; không tính thu hai lần.
- [ ] UI hai góc nhìn có nhãn, tổng và chi tiết bấm đối chiếu được tới giao dịch/kỳ; ngày nhận theo Asia/Ho_Chi_Minh.
- [ ] Sửa/hủy giao dịch phản ánh vào thống kê hiện hành; bảo lưu sử dụng không cộng như tiền mới nhận.
- [ ] Test API qua ranh giới tháng/múi giờ, sửa/hủy và lọc của hai tài khoản; test UI số tổng bằng tổng chi tiết.

## Blocked by

- #10: Sửa hoặc hủy khoản nhận tiền và đối chiếu lịch sử

