# Stitch — Education Keeptrack

**Phạm vi bổ sung đã chốt:** BRD 2.2/spec 1.1 có đăng ký email, chờ Admin duyệt, khôi phục, quyền có hạn, Admin 2FA và quyền/hạn mức AI. Các mẫu Stitch hiện tại là nguồn cho luồng lớp cơ bản; chưa coi đã đủ UI cho #23–#27. Quyết định mới ưu tiên các ghi chú cũ nói cách đăng nhập chưa chốt.

**Kết quả hiện hành:** [Rà soát sau chỉnh sửa](Ticket_02_Latest_Review.md). Đã đủ 5 trạng thái tạo lớp desktop/mobile; các khác biệt còn lại được chuyển thành checklist triển khai. Kết luận thiếu mobile lỗi/thành công ở các báo cáo cũ đã được thay thế.

**Cập nhật mới nhất:** [Rà soát form tạo lớp](Ticket_02_Create_Class_Review.md): đã có tám bản tạo lớp. Kết luận chưa có form trong báo cáo trước đã được thay thế; còn cần sửa nội dung ngoài phạm vi, zoom/giới hạn mô tả và hoàn thiện đăng nhập.

**Rà soát mới nhất sau người dùng cập nhật:** [Ticket_02_User_Review.md](Ticket_02_User_Review.md). Có năm bản danh sách mới; đăng nhập cũ còn nội dung ngoài spec và chưa tìm thấy form tạo lớp mới trong inventory. Các bộ preview trước được giữ làm lịch sử, không tự coi đã được duyệt.

## Nguồn thiết kế được lựa chọn

| Thuộc tính | Giá trị |
| --- | --- |
| Project | Education Keeptrack Login Page |
| Project ID | `7871005430122021919` |
| Resource name | `projects/7871005430122021919` |
| Liên kết | [Mở project trong Stitch](https://stitch.withgoogle.com/projects/7871005430122021919) |
| Quyền riêng tư tại lần kiểm tra | PRIVATE |
| Ngày đối chiếu | 16/10/2026, theo múi giờ Việt Nam |
| Spec nguồn | [Đặc tả kỹ thuật](../Technical_Spec_EducationKeeptrack.md), [Issue #1](https://github.com/truongtoanr0602/EducationKeeptrack/issues/1) |
| Ticket nguồn | [Danh sách 21 lát cắt](../Ticket_Breakdown_EducationKeeptrack.md) |

Người dùng đã chọn project này làm nguồn thiết kế UI cho Education Keeptrack. Đây là liên kết tham chiếu trong workspace, không phải cơ chế tự đồng bộ mã nguồn hoặc tự triển khai. Chọn project không đồng nghĩa mọi màn hình và nội dung trong project đã được duyệt.

## Inventory ban đầu (trước lượt thiết kế ticket #2)

Đã gọi `get_project`, `list_screens`, tải và đọc HTML đăng nhập, xem ảnh chụp màn hình. Inventory là kết quả tại thời điểm kiểm tra; cần đọc lại Stitch trước khi triển khai vì project có thể thay đổi.

| Loại | Resource/ID ổn định | Kết quả |
| --- | --- | --- |
| Màn hình đăng nhập | `projects/7871005430122021919/screens/15a5fb831ea64c77917157b55c0a65e7` | Có screenshot và HTML; metadata MOBILE, ảnh 780 × 2210; mới là prototype UI |
| Ảnh minh họa giáo viên | `projects/7871005430122021919/screens/a4259a83d5564a30aff5fd5c9bf7f872` | Có ảnh 1200 × 896, không có HTML; không tính là màn hình nghiệp vụ |
| Design-system asset | `assets/c07b5f22908f45b29fe882985c23232c` | Có instance trong project; chưa kiểm tra nội dung token độc lập hoặc phê duyệt áp dụng toàn bộ |

Không dùng nhãn instance “Generating Screen...” để kết luận màn hình còn đang tạo: `list_screens` đã trả HTML và screenshot đăng nhập. Không ghi URL download tạm thời vào tài liệu; lấy lại qua resource name khi cần.

## Mức đáp ứng hiện tại

- Ticket #2 đã có bộ sáu bản nháp cho đăng nhập/danh sách/tạo lớp trên desktop và bố cục dành cho mobile. Xem [preview và các giới hạn kiểm tra](Ticket_02_Preview.md); chưa được người dùng duyệt, hai bản mobile có metadata DESKTOP và còn cần kiểm tra viewport thực.
- 20 ticket còn lại chưa có màn hình được tìm thấy trong inventory này. Nhiều ticket có thể dùng chung màn hình với tab, panel, modal hoặc trạng thái bổ sung; không cần tạo 21 trang riêng.
- Chưa có backend, cơ sở dữ liệu hoặc tích hợp API được chứng minh bởi các thiết kế này. HTML đăng nhập đang mô phỏng chờ bằng bộ đếm thời gian, chưa xác thực tài khoản.
- [Bảng đối chiếu đầy đủ](Stitch_Screen_Audit.md) nêu từng ticket, trạng thái còn thiếu và thứ tự đề xuất.

## Các điểm cần điều chỉnh hoặc xác nhận trên đăng nhập cũ

| Điểm quan sát từ HTML/screenshot | Xử lý khi triển khai |
| --- | --- |
| Email hoặc số điện thoại, Google login, ghi nhớ đăng nhập, quên mật khẩu | Spec chưa chốt phương thức xác thực. Xác nhận phương thức hỗ trợ trước khi triển khai; không coi các nút này là hợp đồng đã duyệt |
| Dùng thử 30 ngày | Chưa có chính sách gói/dùng thử trong spec; bỏ hoặc xác nhận trước khi đưa vào sản phẩm |
| Hotline 1900 6828 và giờ hỗ trợ | Chưa có thông tin liên hệ được xác nhận; thay bằng nội dung thật được cung cấp hoặc bỏ |
| Tuyên bố bảo mật AES-256 | Thiết kế không chứng minh năng lực bảo mật này; bỏ tuyên bố cho đến khi có căn cứ triển khai được xác nhận |
| Emoji vẫy tay và icon trang trí | Rà theo yêu cầu emoji tối giản, tiết chế; giữ icon chức năng có ích |
| `lang="en"`, viewport tắt zoom | Khi triển khai tiếng Việt dùng ngôn ngữ tài liệu phù hợp và cho phép phóng to; kiểm tra bàn phím, focus, nhãn lỗi và độ tương phản |
| HTML dùng CDN và script mô phỏng | Chỉ là tài liệu thiết kế tham khảo; chuyển thành UI của ứng dụng và nối API, không coi HTML xuất từ Stitch là ứng dụng hoàn chỉnh |

Màu xanh đậm, nền sáng, accent xanh ngọc, font Literata/Manrope được quan sát trong màn hình hiện tại, chưa phải quyết định thương hiệu đã duyệt. Không tự đổi design system hoặc tạo màn hình mới trong lần kiểm kê này.

## Cách dùng liên kết trong các lần triển khai sau

1. Đọc spec và ticket đang làm, rồi tìm hàng tương ứng trong bảng đối chiếu.
2. Đọc lại project bằng resource name trên; lấy màn hình cụ thể qua `get_screen`. Chỉ dùng HTML/ảnh từ project đã chọn.
3. Nếu màn hình chưa có, thực hiện bước thiết kế theo phạm vi ticket trước khi triển khai UI. Dữ liệu nghiệp vụ theo spec được ưu tiên khi mẫu thiết kế mâu thuẫn.
4. Sau khi có màn hình phù hợp, cập nhật resource name và trạng thái trong bảng đối chiếu; phân biệt rõ đã có thiết kế, đã duyệt và đã triển khai.
5. Khi xây UI, nối dữ liệu/API của ticket, kiểm tra responsive và các trạng thái lỗi/thiếu dữ liệu. Không cập nhật GitHub hoặc sửa Stitch chỉ từ việc đọc tài liệu này; theo phạm vi công việc được giao.

Thông tin máy đọc được nằm tại [stitch-project.json](stitch-project.json). Không chứa API key hoặc URL tải tạm thời.

## Bản nháp Ticket #2 mới

[Brief thiết kế](Ticket_02_Design.md) · [Sáu bản preview hiện hành và ID](Ticket_02_Preview.md). Đã tải ảnh/HTML về workspace; chưa sửa mã ứng dụng hoặc trạng thái GitHub issue. Mẫu cũ được giữ để đối chiếu, không dùng nội dung ngoài spec từ mẫu đó làm yêu cầu đã duyệt.





