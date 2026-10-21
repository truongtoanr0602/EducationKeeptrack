# T11: Soạn nhận xét và xuất báo cáo tháng PDF

> Cập nhật 21/10/2026: dùng Cloudflare R2 private thay SeaweedFS/MinIO. [Thiết kế R2](../../../doc/cloudflare-r2.md) quy định cấu hình, quyền tải qua backend, kiểm thử và chuyển dữ liệu; thay thế hướng dẫn kho cũ. Mã nguồn/Compose hiện tại chưa được chuyển đổi.

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên xem trước, biên tập nhận xét/lộ trình và tải PDF riêng từng học sinh với bảng học phí cùng thông tin thanh toán.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 33, 35, 46, 47, 48, 49; AC-09, AC-13, AC-24, AC-29.

## Acceptance criteria

- [ ] Lưu bản nháp, tùy chỉnh hiển thị, phiên bản nguồn và metadata bản xuất tại Reporting; lưu PDF riêng tư trong Cloudflare R2 qua S3 API và kiểm tra quyền khi tải.
- [ ] UI sửa tên/thông tin liên hệ hiển thị, nhận xét, lộ trình và emoji tối giản; tài chính sửa qua dữ liệu nguồn, không cho ghi đè tổng tiền trên preview.
- [ ] PDF có chuyên cần, hai mức đã xác nhận, trạng thái thiếu/0, lộ trình, phí, nợ cũ, tiền phân bổ, thông tin QR/fallback; chỉ dữ liệu học sinh được chọn.
- [ ] Xuất từ nguồn nhất quán; nếu dữ liệu đổi hoặc tổng hợp còn cũ thì yêu cầu làm mới, không phát hành PDF nửa cũ nửa mới. Thiếu QR vẫn xuất có cảnh báo.
- [ ] Test API với dữ liệu thật và dựng PDF thật; trích xuất nội dung/số liệu và xem mẫu tiếng Việt/emoji/nhiều trang. Test URL hết hạn/quyền tải/khóa chống lặp; không xuất Word/DOCX, không tự gửi phụ huynh.

- [ ] Giáo viên hết hạn vẫn xuất từ dữ liệu đã có nhưng không sửa nội dung/nguồn; metadata kỹ thuật xuất không mở quyền ghi nghiệp vụ. Tài khoản khóa không được tải PDF. Không gọi AI khi thiếu quyền hoặc hết hạn.

## Blocked by

- #6: Tự chấm bài bằng hai chỉ số bốn mức
- #8: Tạo VietQR với QR tĩnh và thông tin chuyển khoản dự phòng


