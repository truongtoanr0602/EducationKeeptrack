# T20: Khấu trừ bảo lưu kỳ sau và thể hiện số dư trên PDF

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên áp dụng bảo lưu được duyệt vào kỳ sau, xem phần còn lại và xuất PDF đối chiếu đầy đủ.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 43, 48; AC-10, AC-12.

## Acceptance criteria

- [ ] Lưu từng lần sử dụng/đảo sử dụng và số dư bảo lưu; API/UI chỉ khấu trừ trong giới hạn nghĩa vụ còn phải trả và nguồn còn hiệu lực.
- [ ] Kỳ sau phí 900.000, bảo lưu 450.000: còn 450.000; nguồn lớn hơn phí thì dư nợ 0, phần bảo lưu còn lại tách riêng.
- [ ] Tiền chuyển sang bảo lưu không đồng thời được tính tiền trả kỳ mới; bảo lưu dùng không là khoản tiền thực thu mới.
- [ ] UI sổ đầu kỳ/mới duyệt/đã dùng/chuyển tiếp; PDF thể hiện các khoản và cập nhật tổng yêu cầu thanh toán. Sửa/hủy bảo lưu có lý do, đối chiếu phần đã dùng trước khi cập nhật.
- [ ] Test API dùng đồng thời/lặp, hủy có sử dụng, nguồn hết hạn theo chính sách đã chốt; test PDF tổng/bảo lưu khớp. Hạn dùng/phạm vi/chuyển lớp chưa duyệt thì không tự áp dụng.

## Blocked by

- #20: Ghi tiền trả trước và duyệt bảo lưu nghỉ ốm
- #12: Soạn nhận xét và xuất báo cáo tháng PDF

