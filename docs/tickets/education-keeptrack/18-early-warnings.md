# T18: Xem cảnh báo học tập có bằng chứng và ghi hành động hỗ trợ

## Parent

Spec: https://github.com/truongtoanr0602/EducationKeeptrack/issues/1

## What to build

Giáo viên xem danh sách học sinh cần quan tâm, kiểm tra bằng chứng, ghi đã xem/hành động và duyệt nội dung đưa vào PDF.

Lát cắt hoàn chỉnh gồm lưu dữ liệu, API, UI dùng được và kiểm thử hành vi. Tuân theo ranh giới module, cô lập giáo viên và nguyên tắc dữ liệu của spec; không tách thành ticket database/API/UI riêng.

Đối chiếu: User Stories 30, 31; AC-16, AC-17.

## Acceptance criteria

- [ ] Lưu cảnh báo, phiên bản tiêu chí/mô hình, giai đoạn/bằng chứng và hành động hỗ trợ; API/UI chi tiết đối chiếu được tới dữ liệu chính thức.
- [ ] Chỉ kết quả cuối dùng phân tích; thiếu lịch sử trả chưa đủ dữ liệu, xét nghỉ có phép; không tự kết luận mất gốc.
- [ ] UI ghi đã xem/lý do chưa phù hợp/hỗ trợ; chỉ nội dung giáo viên duyệt được đưa vào báo cáo PDF.
- [ ] Số bài tối thiểu, ngưỡng, mô hình và bộ dữ liệu nghiệm thu phải được xác nhận trước thực hiện thuật toán thật; chưa cấu hình hiển thị chưa thiết lập, không dùng ngưỡng tự đặt. Không tạo phiếu nghiên cứu riêng thay lát cắt này.
- [ ] Test API với tiêu chí fixture minh bạch và mô hình mô phỏng chỉ kiểm tra workflow; nghiệm thu thực trên bộ đã duyệt. Test UI căn cứ/hành động và PDF không lộ cảnh báo chưa duyệt.

- [ ] Nhận tác vụ AI phải kiểm tra quyền/hạn mức ServiceAccess và giữ lượt đúng loại/kỳ; thành công trừ, lỗi/hủy hoàn, phản hồi lặp không tính hai lần. Tắt AI/hết hạn cho lượt đã nhận hoàn tất bản nháp; khóa vô hiệu kết quả về muộn. Không tự đặt cách tính lượt cảnh báo chưa được chốt.

## Blocked by

- #18: Xem biểu đồ hai chỉ số và độ đầy đủ dữ liệu
- #12: Soạn nhận xét và xuất báo cáo tháng PDF


- #27: Admin bật tắt AI và giáo viên xem hạn mức theo kỳ

