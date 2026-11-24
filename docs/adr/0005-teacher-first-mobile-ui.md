# Giao diện giáo viên ưu tiên mobile

**Trạng thái: accepted — 17/11/2026.** Người dùng chốt thiết kế lại UI giáo viên quanh ba việc chính: điểm danh, thu tiền/gửi hóa đơn và xem báo cáo tháng. Hướng này bổ sung ADR 0001 (React + TypeScript + Vite), không thay thế.

## Quyết định

- **Người dùng và thiết bị**: một giáo viên hoặc gia sư dùng một tài khoản. Thiết kế mobile trước và thao tác được bằng một tay; desktop vẫn dùng đầy đủ.
- **Điều hướng xuyên lớp**: bốn mục cấp cao là **Hôm nay**, **Lớp & học sinh**, **Hóa đơn**, **Báo cáo**. Mobile dùng thanh tab dưới, desktop dùng sidebar. Mục "Thêm" chứa tài khoản và AI. Trang chi tiết lớp giữ cấu hình lớp và Lịch tuần.
- **Hôm nay** là trang đầu: các buổi học của mọi lớp trong ngày. Điểm danh mặc định cả lớp có mặt, giáo viên chỉ chạm vào ngoại lệ, có nút Lưu và hoàn tác ngay sau khi lưu.
  Bổ sung 22/11/2026 (#103): **widget lịch ở Hôm nay** cho tuần và tháng. Dải tuần mặc định, mở thành lưới tháng; ngày đang chọn giữ nguyên khi đổi chế độ; chỉ đọc, điểm danh vẫn dùng luồng Hôm nay. Hiện cả chỗ **Chưa tạo buổi** suy ra từ lịch tuần và dấu **Quá hạn** (buổi đã qua trong 30 ngày vẫn Dự kiến). Lịch tuần vẫn cấu hình ở trang lớp. Thiết kế: `docs/design/today-calendar-widget.md`.
  Mục của lớp gom từ bảy thành năm: Học sinh, Lịch, Học phí, Học tập, Báo cáo. "Học tập" gồm Đánh giá, Chương trình & kiểm tra và Cá nhân hoá (vẫn ẩn khi không có Quyền AI); đường dẫn cũ tự chuyển sang `#/classes/{id}/learning/...`.
- **Hóa đơn** gom thu tiền: danh sách còn nợ xuyên lớp, lọc theo lớp. Từ đó giáo viên ghi nhận Giao dịch nhận tiền, sửa biên lai, xem Sổ bảo lưu và chia sẻ PDF hoặc mã QR bằng Web Share API. Không còn mục "Học phí & thu tiền" riêng.
- **Báo cáo** mở ra thẻ số liệu tháng (số buổi, chuyên cần, tổng thu, còn nợ). Xuất PDF và soạn bản nháp là hành động phụ.
- **Đánh giá** nằm trong trang chi tiết học sinh. Phần liên quan đến AI ẩn cùng AI.
- **AI ẩn hẳn khi không có Quyền AI**, không để ô khoá hay quảng cáo. Code AI được lazy-load.
  Bổ sung 20/11/2026 (#77): chương trình theo buổi, ngân hàng câu hỏi, đề kiểm tra và hồ sơ năng lực không phải AI, nên nằm ở mục lớp riêng **"Chương trình & kiểm tra"** và luôn hiện. Mục "Cá nhân hoá" (học liệu, kế hoạch ôn tập) vẫn ẩn khi không có Quyền AI. Link cũ `…/personalization/<tab của chương trình>` tự chuyển sang mục mới.
- **Admin ngoài phạm vi**: giữ workspace hiện có, chỉ đảm bảo không hỏng khi đổi shell.

## Hiệu năng

- Không thêm thư viện fetch hay state. Viết cache nhỏ kiểu stale-while-revalidate, cache CSRF token để bỏ lượt `GET /api/auth/csrf` thừa trước mỗi lần ghi, và optimistic update cho điểm danh.
- Tách chunk bằng `React.lazy` theo route (AI, admin, báo cáo).
- Backend chỉ thêm endpoint đọc tổng hợp xuyên lớp và phân trang; không đổi domain hay dữ liệu.
  Ngoại lệ (22/11/2026, #104): `GET /api/today/range?from&to` (tối đa 42 ngày) trả buổi theo ngày và các chỗ chưa tạo buổi theo đúng phiên bản lịch tuần từng ngày; chỉ đọc, không đổi schema.
  Ngoại lệ (18/11/2026, theo yêu cầu người dùng): `PUT /api/classes/{classId}/sessions/{id}/attendance` lưu điểm danh cả buổi trong một lần, có hoặc không. Endpoint chỉ gom các lệnh ghi điểm danh đã có: mỗi học sinh thay đổi vẫn tăng một phiên bản buổi và sinh một sự kiện; không đổi schema.
- Mốc đo trên mobile giả lập 4G chậm: "Hôm nay" tương tác được dưới 2 giây ở lần tải đầu; điểm danh lớp 15 học sinh dưới 5 chạm; bundle ban đầu dưới 150 KB gzip. Đo bằng Lighthouse và Playwright, kèm viewport mobile cho các luồng chính.
- Không làm offline hoặc PWA trong đợt này.

## Triển khai

Thay shell dần trong `ui/`, mỗi bước một issue và một nhánh; không giữ giao diện cũ song song.

## Điểm còn mở

Mã nguồn chưa có PDF hóa đơn riêng cho từng học sinh; PDF hiện có là báo cáo tháng gửi phụ huynh. Issue Hóa đơn phải xác định dùng lại PDF báo cáo tháng theo từng học sinh hay thêm một phiếu thu học phí riêng.
