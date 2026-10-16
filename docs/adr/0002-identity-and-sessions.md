# Identity với cookie cùng origin và phiên có thể thu hồi

**Trạng thái: accepted — 16/10/2026.** Dùng email/mật khẩu với ASP.NET Core Identity và cookie HttpOnly, UI/API cùng origin khi triển khai; không dùng JWT hoặc OAuth cho phạm vi đầu tiên. Cách này phù hợp ứng dụng web hiện tại và giữ token khỏi JavaScript, đổi lại phải kiểm soát CSRF và chủ động vô hiệu phiên từ server.

Cookie truyền qua HTTPS, yêu cầu ghi có chống CSRF. Phiên giáo viên có idle timeout 2 giờ và absolute timeout 24 giờ; Admin tương ứng 30 phút/8 giờ. Theo dõi phiên có thể thu hồi để khóa tài khoản hoặc đổi email/đặt lại mật khẩu vô hiệu các phiên cũ ngay tại lần yêu cầu server tiếp theo, không chỉ chờ cookie hết hạn. Polling nền không được âm thầm kéo dài phiên người dùng vô thời hạn.

Đăng ký phải xác minh email, sau đó chờ Admin duyệt. Admin đầu tiên tạo qua bước thiết lập riêng, không có đăng ký công khai vai trò Admin; Admin dùng TOTP từ ứng dụng. Khôi phục mật khẩu bằng email dùng liên kết một lần có hạn và phản hồi không tiết lộ tài khoản; đổi email phải xác minh email mới, giữ định danh và quyền đã mua.

