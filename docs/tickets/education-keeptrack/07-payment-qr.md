# T07: Tạo VietQR với QR tĩnh và thông tin chuyển khoản dự phòng

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên cấu hình tài khoản nhận tiền và xem yêu cầu chuyển khoản đúng tổng dư nợ trên UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 44, 45; AC-07, AC-08, AC-28.

## Acceptance criteria

- [ ] Lưu tài khoản nhận tiền và QR ngân hàng tĩnh riêng tư của giáo viên; API kiểm tra thông tin và UI xem trước người nhận.
- [ ] Ưu tiên VietQR chứa tổng cần thu kể cả nợ cũ và nội dung nhận diện học sinh/kỳ; 700.000 đồng dư nợ tạo đúng số tiền.
- [ ] VietQR lỗi mới dùng QR tĩnh; thiếu cả hai hiển thị đủ ngân hàng/tài khoản/người nhận/tiền/nội dung và cảnh báo, không chặn xem yêu cầu.
- [ ] Số tiền 0 không tạo yêu cầu; tài khoản thiếu/sai báo rõ; nguồn tiền hoặc tài khoản đổi làm mã cũ lỗi thời, không tái dùng mã sai tiền.
- [ ] Test API mô phỏng lỗi QR hai tầng, thay số dư giữa tạo mã, kiểm soát sở hữu QR tĩnh; UI phân biệt loại QR và hướng dẫn tự nhập. Không tự xác nhận tiền khi quét.

## Blocked by

- #7: Ghi nhận thanh toán một phần và chuyển nợ cũ

