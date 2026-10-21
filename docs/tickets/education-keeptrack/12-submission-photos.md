# T12: Chuẩn bị hướng dẫn chấm và tải ảnh bài nộp riêng tư

> Cập nhật 21/10/2026: dùng Cloudflare R2 private thay SeaweedFS/MinIO. [Thiết kế R2](../../../doc/cloudflare-r2.md) quy định cấu hình, quyền tải qua backend, kiểm thử và chuyển dữ liệu; thay thế hướng dẫn kho cũ. Mã nguồn/Compose hiện tại chưa được chuyển đổi.

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên cung cấp đề/đáp án/hướng dẫn chung cho bài, tải ảnh theo học sinh và xem lại đúng thứ tự trên UI.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 19, 20, 24, 50; AC-14, AC-30.

## Acceptance criteria

- [ ] Lưu phiên bản hướng dẫn bất biến, liên kết bài nộp và ảnh có thứ tự tại Learning; ảnh gốc private trong Cloudflare R2 qua S3 API.
- [ ] API/UI tải nhiều trang, sắp xếp/xem và sửa liên kết sai học sinh trước dùng; không gắn ảnh tài khoản khác qua ID.
- [ ] Kiểm tra nội dung loại tệp/kích thước thay vì chỉ đuôi tên; giới hạn là cấu hình rõ, không tự công bố số như đã duyệt.
- [ ] Chỉ cấp link đọc giới hạn thời gian sau kiểm tra chủ sở hữu; không log nội dung ảnh/URL nhạy cảm. Cấu hình bucket R2 private và secrets server và thử khôi phục ảnh cùng metadata của lát cắt.
- [ ] Test API upload thực vào object storage, tải lại UI, đường dẫn trực tiếp trái quyền/URL hết hạn, lỗi upload không để bài có ảnh giả. Phạm vi dùng AI và giữ/xóa ảnh còn mở, không cho huấn luyện ngoài mục đích.

## Blocked by

- #6: Tự chấm bài bằng hai chỉ số bốn mức

