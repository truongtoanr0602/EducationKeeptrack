# Học sinh vào đề kiểm tra online bằng link và mã vào bài

**Trạng thái: accepted — 20/11/2026.** Thuộc issue #81 (cha #77). Bổ sung ADR 0002, không thay thế.

## Bối cảnh

Giáo viên muốn học sinh lớp 2 tự làm đề kiểm tra online. Hệ thống chỉ có tài khoản Giáo viên và Admin. Trẻ 7–8 tuổi thường dùng máy của phụ huynh và hay quên mật khẩu.

## Quyết định

- **Không tạo tài khoản học sinh.** Mỗi đề kiểm tra có một link công khai chứa token ngẫu nhiên 128 bit. Khi giáo viên mở đề, mỗi học sinh nhận một **mã vào bài** bốn số, không trùng trong đề. Giáo viên in bảng mã hoặc đọc cho từng em.
- Học sinh mở link, chọn tên mình, nhập mã. Danh sách chỉ hiện **tên gọi kèm chữ cái đầu của họ** (ví dụ "An N."), chỉ khi đề đang mở. Không hiện số điện thoại, tên phụ huynh hay ghi chú.
- Nhập đúng mã thì server đặt cookie `ekt.exam`: HttpOnly, SameSite=Strict, `Path=/api/exam-take`, Secure khi chạy HTTPS, sống 8 giờ. Cookie chứa token ký bằng Data Protection gồm đề, chỗ ngồi và một nonce. Mỗi lần vào lại thì nonce đổi, nên phiên trên máy cũ hết hiệu lực. Cookie này tách hẳn khỏi phiên của giáo viên (ADR 0002).
- Ghi vẫn đi qua kiểm tra CSRF chung của `/api`. `GET /api/auth/csrf` gọi được khi chưa đăng nhập.
- **Chống dò mã**: nhập sai năm lần thì mã bị khóa, giáo viên mở lại. Mọi route `/api/exam-take/*` có giới hạn 300 yêu cầu mỗi phút cho mỗi địa chỉ IP (ASP.NET Core rate limiting).
- API học sinh **không bao giờ trả đáp án** khi đề đang mở. Đáp án chỉ hiện sau khi giáo viên đóng đề và bật "cho xem kết quả".
- Tài khoản giáo viên không còn quyền ghi (hết hạn hoặc bị khóa) thì học sinh không vào làm bài được.

## Hệ quả

- Ai có link và đúng mã thì làm bài thay được cho em đó. Rủi ro này chấp nhận được với bài kiểm tra trên lớp: giáo viên giữ bảng mã và cho làm lại khi cần.
- Không có lịch sử bài làm theo học sinh ngoài các đề. Nếu sau này cần tài khoản học sinh thì phải có ADR mới.
