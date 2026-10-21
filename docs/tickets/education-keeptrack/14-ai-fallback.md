# T14: Thử lại AI lỗi hoặc chuyển bài sang tự chấm an toàn

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên xử lý bài AI lỗi trên UI bằng tải bổ sung/thử lại hoặc chuyển sang tự chấm và tiếp tục hoàn tất bài.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 25, 26; AC-14, AC-21.

## Acceptance criteria

- [ ] Lưu nguyên nhân lỗi và hiệu lực từng lượt; API/UI cho thử lại với ảnh/hướng dẫn hợp lệ, có khóa chống lặp.
- [ ] Chuyển thủ công vô hiệu lượt AI đang chờ trong cùng giao dịch; UI trở về hai nhóm nút tự chấm.
- [ ] Kết quả AI cũ về muộn kiểm tra ID lượt, chế độ và phiên bản; không thay kết quả thủ công hoặc lượt AI mới.
- [ ] Retry không tạo bài/đầu điểm trùng; lỗi nhà cung cấp có timeout và thông báo cụ thể, không tự sinh mức 0.
- [ ] Test API điều khiển thứ tự trả kết quả, chuyển chế độ trước phản hồi, retry hai lần và khởi động lại worker; UI sau tải lại vẫn giữ kết quả giáo viên.

## Blocked by

- #14: AI chấm bài và giáo viên sửa xác nhận hai mức

