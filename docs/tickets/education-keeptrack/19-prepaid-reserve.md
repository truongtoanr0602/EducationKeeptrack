# T19: Ghi tiền trả trước và duyệt bảo lưu nghỉ ốm

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên ghi khoản trả trước, xem phần chưa dùng và duyệt bảo lưu từ đề nghị nghỉ ốm trên UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 42; AC-10, AC-11.

## Acceptance criteria

- [ ] Lưu tiền trả trước chưa phân bổ và đề nghị nghỉ/lý do/phê duyệt, nối tới nguồn tiền thực; UI đối chiếu nguồn đã thu, đã dùng, chưa dùng.
- [ ] Đã trả 1.200.000, thực dùng 750.000: chỉ tối đa 450.000 có thể xét bảo lưu; chưa trả tiền thì không tạo bảo lưu từ buổi vắng.
- [ ] Duyệt có lý do, chỉ chuyển tiền chưa dùng chưa thuộc nguồn bảo lưu khác; thao tác nguyên tử/chống lặp/tranh chấp và lịch sử.
- [ ] Không tự quyết số ngày đủ dài, minh chứng, hạn dùng, chuyển/dừng lớp hoặc hoàn tiền; cần chính sách được chốt trước áp dụng thật, UI nêu rõ trạng thái chưa có chính sách.
- [ ] Test API nguồn đủ/thiếu, duyệt hai lần, đã phân bổ và rollback; UI tải lại sổ nguồn đúng. Hỗ trợ ví dụ đã duyệt nhưng không coi đó là duyệt mọi trường hợp trả trước.

## Blocked by

- #7: Ghi nhận thanh toán một phần và chuyển nợ cũ

