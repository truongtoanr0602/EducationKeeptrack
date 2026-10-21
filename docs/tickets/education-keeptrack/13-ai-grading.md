# T13: AI chấm bài và giáo viên sửa xác nhận hai mức

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên chọn AI, theo dõi xử lý ảnh và sửa/xác nhận hai mức đề xuất trên UI bài nộp.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 14, 21, 22, 23, 24; AC-04, AC-15, AC-19, AC-20.

## Acceptance criteria

- [ ] Lưu công việc nền và lượt hiện hành gắn bài/ảnh/phiên bản hướng dẫn; API trả ID/trạng thái, UI không bị chặn khi AI xử lý.
- [ ] Kiểm tra phản hồi AI và quy đổi 0→trạng thái 0, (0,25]→25, (25,50]→50, (50,75]→75, (75,100)→75, 100→100 cho cả hai chỉ số; ngoài miền/thiếu căn cứ báo lỗi.
- [ ] UI hiện lỗi có bằng chứng, ảnh nguồn, mức gợi ý và kết quả gốc; giáo viên sửa/xác nhận, lưu lịch sử; chưa xác nhận chưa dùng chính thức.
- [ ] AI mờ/thiếu trang/thiếu căn cứ/không chắc chắn có trạng thái rõ, không ghi 0; không gọi AI đối với bài tự chấm.
- [ ] Test API phản hồi mô phỏng, mốc và hai phía mốc, lệnh lặp, khởi động lại công việc, truy cập chéo và xác nhận tranh chấp. Phải chốt môn/dạng bài, nhà cung cấp, bộ mẫu và độ chắc chắn trước nghiệm thu AI thật; luồng mô phỏng không là bằng chứng chất lượng AI.

- [ ] Nhận tác vụ AI phải kiểm tra quyền/hạn mức ServiceAccess và giữ lượt đúng loại/kỳ; thành công trừ, lỗi/hủy hoàn, phản hồi lặp không tính hai lần. Tắt AI/hết hạn cho lượt đã nhận hoàn tất bản nháp; khóa vô hiệu kết quả về muộn. Không tự đặt cách tính lượt cảnh báo chưa được chốt.

## Blocked by

- #13: Chuẩn bị hướng dẫn chấm và tải ảnh bài nộp riêng tư


- #27: Admin bật tắt AI và giáo viên xem hạn mức theo kỳ

