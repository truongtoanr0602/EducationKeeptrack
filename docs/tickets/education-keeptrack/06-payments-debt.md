# T06: Ghi nhận thanh toán một phần và chuyển nợ cũ

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên nhập từng lần nhận tiền và xem nợ cũ, nợ kỳ mới cùng tổng cần thu trên UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 38, 39; AC-08, AC-25.

## Acceptance criteria

- [ ] Lưu giao dịch số tiền dương, ngày nhận, ghi chú và phân bổ theo kỳ; mặc định trả kỳ nợ cũ nhất trước rồi kỳ mới.
- [ ] Nợ cũ 500.000, phí mới 900.000, nhận 700.000: phân bổ 500.000/200.000 và còn 700.000; không sao chép nợ thành phí mới.
- [ ] UI hiển thị lịch sử nhận tiền và chưa trả/trả một phần/đủ; quét QR hay ảnh chuyển khoản không tự đánh dấu thu đủ.
- [ ] Ghi tiền/phân bổ nguyên tử, có khóa chống lặp và kiểm soát phiên bản; hai lần thu đồng thời không dùng dư nợ sai.
- [ ] Test API gửi lặp, tranh chấp, rollback và tổng dư nợ; tiền trả dư lưu riêng, không tự hoàn tiền. Chính sách trả trước/bảo lưu được xử lý riêng trong lát cắt tương ứng.

## Blocked by

- #5: Xem học phí theo buổi thực học và đơn giá hiệu lực

