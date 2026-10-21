# T09: Sửa hoặc hủy khoản nhận tiền và đối chiếu lịch sử

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên sửa/hủy giao dịch nhầm từ UI và thấy phân bổ, dư nợ được phục hồi đúng.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 41, 49; AC-27.

## Acceptance criteria

- [ ] API/UI yêu cầu lý do sửa/hủy; giữ giao dịch gốc và dấu vết đảo/thay thế, không xóa lịch sử.
- [ ] Đảo phân bổ bị ảnh hưởng, tính lại theo giao dịch còn hiệu lực nguyên tử; không trừ hai lần hoặc lưu nửa thao tác.
- [ ] Giữ phân biệt ngày thực nhận và thời điểm sửa; trạng thái thanh toán, dữ liệu yêu cầu thu cập nhật theo phiên bản mới.
- [ ] UI xem lịch sử trước/sau; phát thay đổi để báo cáo đã xuất có thể đánh dấu cần cập nhật khi tính năng PDF tồn tại.
- [ ] Test API hủy nhận tiền trả nhiều kỳ, sửa tiền/ngày, lệnh lặp và giao dịch đồng thời; tải lại UI đối chiếu đúng số dư. Không tự hoàn tiền hoặc đoán cách dùng tiền dư.

## Blocked by

- #7: Ghi nhận thanh toán một phần và chuyển nợ cũ

