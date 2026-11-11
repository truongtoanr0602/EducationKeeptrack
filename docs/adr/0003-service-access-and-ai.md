# Tách quyền phần mềm, quyền AI và học phí học sinh

**Trạng thái: accepted — 16/10/2026.** Phần mềm thu phí giáo viên và Admin đối chiếu chuyển khoản thủ công trước cấp quyền. Phí dịch vụ không đi vào Billing học phí học sinh. Tạo module ServiceAccess quản lý hồ sơ đăng ký, kỳ sử dụng, yêu cầu thanh toán dịch vụ, quyền AI và sổ hạn mức; Identity quản lý danh tính/phiên, module nghiệp vụ thực thi chính sách qua hợp đồng công khai.

Tài khoản không có quyền AI vẫn dùng nghiệp vụ thủ công nếu còn hạn. Hết hạn được xem/xuất dữ liệu, không ghi mới; khóa tài khoản chặn toàn bộ truy cập và vô hiệu phiên. Tắt AI hoặc hết hạn không hủy công việc AI đã được chấp nhận; khóa tài khoản thì hủy/vô hiệu kết quả về muộn. Hoàn tất nền chỉ được lưu kết quả dự thảo, không tự xác nhận hoặc ghi đè nội dung giáo viên.

**Cập nhật 11/11/2026:** có ba loại lượt riêng: chấm bài, nhận xét và kế hoạch. Loại nhận xét (trước đây chỉ nhận xét tháng) áp dụng cho cả nhận xét theo buổi và theo tháng, mỗi lần tạo cho một học sinh là một lượt. Loại kế hoạch là loại mới cho kế hoạch ôn tập tuần và dự thảo kỹ năng. Đơn vị lượt của kế hoạch tuần chưa chốt và Admin phải cấp hạn mức rõ, không có số lượt mặc định. Nhà cung cấp AI nằm sau một interface, chưa chọn; nghiệp vụ không phụ thuộc vào nhà cung cấp cụ thể. Chi tiết: `docs/Kich_ban_AI_thong_nhat.md`.

Hạn mức riêng cho chấm bài và sinh nhận xét được giữ trước, trừ khi thành công, hoàn khi lỗi/hủy và không trừ hai lần. Đổi gói không sửa ngược kỳ đã cấp, tắt/bật AI không reset lượt. Cảnh báo AI bị tắt cùng quyền AI; cuộc phỏng vấn chưa chốt đơn vị tính lượt cảnh báo, nên không tự coi là một loại lượt có giá hoặc miễn phí. Phần này phải được thống nhất trước vận hành cảnh báo AI thật.

