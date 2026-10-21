# Đối chiếu màn hình Stitch với các ticket

**Kết quả hiện hành:** [Rà soát sau chỉnh sửa](Ticket_02_Latest_Review.md). Đã đủ 5 trạng thái tạo lớp desktop/mobile; các khác biệt còn lại được chuyển thành checklist triển khai. Kết luận thiếu mobile lỗi/thành công ở các báo cáo cũ đã được thay thế.

**Cập nhật mới nhất:** [Rà soát form tạo lớp](Ticket_02_Create_Class_Review.md): đã có tám bản tạo lớp. Kết luận chưa có form trong báo cáo trước đã được thay thế; còn cần sửa nội dung ngoài phạm vi, zoom/giới hạn mô tả và hoàn thiện đăng nhập.

**Rà soát mới nhất sau người dùng cập nhật:** [Ticket_02_User_Review.md](Ticket_02_User_Review.md). Có năm bản danh sách mới; đăng nhập cũ còn nội dung ngoài spec và chưa tìm thấy form tạo lớp mới trong inventory. Các bộ preview trước được giữ làm lịch sử, không tự coi đã được duyệt.

Nguồn thiết kế: [Project Stitch đã chọn](Stitch_Project.md). Nguồn công việc: [21 ticket của Issue #1](../Ticket_Breakdown_EducationKeeptrack.md). Ngày kiểm tra: 16/10/2026 theo múi giờ Việt Nam.

**Kết quả:** 1 màn hình UI mobile có HTML, 1 ảnh minh họa và 1 design-system instance. Trong 21 ticket, 1 ticket có thiết kế một phần, 20 ticket chưa tìm thấy thiết kế. Không có ticket nào được xác nhận đủ toàn bộ UI và các trạng thái nghiệm thu.

“Chưa có” nghĩa là chưa tìm thấy trong inventory project tại lần kiểm tra, không có nghĩa màn hình đã bị xóa. Bảng này chỉ đánh giá thiết kế; không xác nhận API hoặc tính năng đã triển khai.

## Cập nhật sau lượt thiết kế Ticket #2

Ticket #2 hiện có sáu bản nháp mới (ba desktop, ba bố cục dành cho mobile), đã tải ảnh/HTML và rà soát nội dung. [Xem các bản hiện hành](Ticket_02_Preview.md). Chưa coi là thiết kế đã duyệt hoặc đủ responsive: hai bản mobile vẫn mang metadata DESKTOP; các trạng thái chưa được kiểm thử trình duyệt. Kết quả inventory ở đoạn mở đầu là snapshot trước lượt tạo này; 20 ticket khác vẫn chưa có thiết kế.

## Đối chiếu theo ticket

| Ticket | Trạng thái | Màn hình/thành phần còn thiếu |
| --- | --- | --- |
| [#2 — Đăng nhập và tạo lớp](https://github.com/truongtoanr0602/EducationKeeptrack/issues/2) | Có sáu bản nháp: xem Ticket_02_Preview.md | Cần duyệt thiết kế, kiểm tra responsive thực tại 390px, focus/contrast và tương tác; chưa triển khai API/xác thực |
| [#3 — Danh sách học sinh](https://github.com/truongtoanr0602/EducationKeeptrack/issues/3) | Chưa có | Chi tiết lớp, danh sách học sinh, thêm/sửa học sinh và phụ huynh; tình trạng theo học; đăng ký trùng, danh sách rỗng, xung đột |
| [#4 — Lịch và điểm danh](https://github.com/truongtoanr0602/EducationKeeptrack/issues/4) | Chưa có | Thiết lập lịch tuần, danh sách buổi, học bù/hủy; bảng điểm danh có mặt/vắng/chưa điểm danh; lý do nghỉ, nhắc thiếu dữ liệu |
| [#5 — Học phí thực học](https://github.com/truongtoanr0602/EducationKeeptrack/issues/5) | Chưa có | Đơn giá và ngày hiệu lực; bảng phí kỳ theo học sinh với chi tiết buổi; chờ tổng hợp, giá thay đổi, điểm danh còn thiếu |
| [#6 — Tự chấm hai chỉ số](https://github.com/truongtoanr0602/EducationKeeptrack/issues/6) | Chưa có | Tạo bài đánh giá, chọn cách chấm mặc định/riêng; hai nhóm bốn nút độc lập; trạng thái 0/không giao/chưa đánh giá, xác nhận và lịch sử |
| [#7 — Nhận tiền và nợ cũ](https://github.com/truongtoanr0602/EducationKeeptrack/issues/7) | Chưa có | Form nhận tiền, bảng nợ theo kỳ và phân bổ cũ trước/mới sau; lịch sử thu; trạng thái chưa trả/một phần/đủ; tranh chấp và gửi lặp |
| [#8 — VietQR và dự phòng](https://github.com/truongtoanr0602/EducationKeeptrack/issues/8) | Chưa có | Cấu hình tài khoản/QR tĩnh; preview VietQR; fallback QR tĩnh/không QR; không cần trả, tài khoản thiếu, mã lỗi thời |
| [#9 — Điều chỉnh phí](https://github.com/truongtoanr0602/EducationKeeptrack/issues/9) | Chưa có | Modal tăng/giảm có lý do, lịch sử điều chỉnh và tổng cập nhật trên bảng phí; phần dư, dữ liệu nguồn đã thay đổi |
| [#10 — Sửa/hủy tiền nhận](https://github.com/truongtoanr0602/EducationKeeptrack/issues/10) | Chưa có | Chi tiết giao dịch, sửa/hủy có lý do và xác nhận; lịch sử trước/sau, phân bổ/dư nợ mới; lỗi thao tác |
| [#11 — Thống kê thu và nợ](https://github.com/truongtoanr0602/EducationKeeptrack/issues/11) | Chưa có | Hai góc nhìn thực thu theo ngày và nghĩa vụ theo kỳ; bộ lọc, tổng/chi tiết đối chiếu, trạng thái rỗng/đang tải |
| [#12 — Báo cáo PDF](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12) | Chưa có | Chọn tháng/học sinh, preview báo cáo, sửa thông tin/nhận xét/lộ trình, emoji tối giản; layout PDF nhiều trang, xuất/tải và dữ liệu thay đổi |
| [#13 — Hướng dẫn và ảnh bài](https://github.com/truongtoanr0602/EducationKeeptrack/issues/13) | Chưa có | Form đề/đáp án/hướng dẫn dùng chung; upload nhiều trang, sắp xếp, xem ảnh và sửa liên kết học sinh; tệp lỗi, thiếu căn cứ, hết quyền xem |
| [#14 — AI chấm và xác nhận](https://github.com/truongtoanr0602/EducationKeeptrack/issues/14) | Chưa có | Tiến độ AI, ảnh và bằng chứng lỗi, hai mức đề xuất/gốc/cuối; sửa/xác nhận, cần kiểm tra, phản hồi AI không hợp lệ |
| [#15 — AI lỗi/chuyển tự chấm](https://github.com/truongtoanr0602/EducationKeeptrack/issues/15) | Chưa có | Panel lỗi AI, bổ sung ảnh/thử lại/chuyển tự chấm, thông báo lượt cũ mất hiệu lực; trở về hai nhóm nút thủ công |
| [#16 — Chấm lại theo hướng dẫn](https://github.com/truongtoanr0602/EducationKeeptrack/issues/16) | Chưa có | Phiên bản hướng dẫn, danh sách bài dùng bản cũ, chọn chấm lại; cảnh báo thay kết quả đã sửa, đang chấm lại và giữ kết quả cũ |
| [#17 — Nhận xét AI](https://github.com/truongtoanr0602/EducationKeeptrack/issues/17) | Chưa có | Nút tạo nhận xét/lộ trình trong preview, tiến độ/lỗi, chỉnh/duyệt; nhãn lỗi thời, tạo lại chủ động, AI lỗi vẫn viết tay/xuất |
| [#18 — Biểu đồ học tập](https://github.com/truongtoanr0602/EducationKeeptrack/issues/18) | Chưa có | Hồ sơ tiến độ, hai chuỗi độc lập, bộ lọc và căn cứ; thiếu dữ liệu, phân biệt 0/thiếu và nhãn nhóm đánh giá |
| [#19 — Cảnh báo và hỗ trợ](https://github.com/truongtoanr0602/EducationKeeptrack/issues/19) | Chưa có | Danh sách/chi tiết cảnh báo, giai đoạn/bằng chứng; đã xem, lý do chưa phù hợp, hành động và duyệt đưa vào PDF; chưa thiết lập tiêu chí |
| [#20 — Trả trước và duyệt bảo lưu](https://github.com/truongtoanr0602/EducationKeeptrack/issues/20) | Chưa có | Form trả trước, sổ nguồn chưa dùng, đề nghị nghỉ ốm và duyệt có lý do; nguồn không đủ/đã dùng, chính sách chưa xác nhận |
| [#21 — Dùng bảo lưu và PDF](https://github.com/truongtoanr0602/EducationKeeptrack/issues/21) | Chưa có | Sổ đầu kỳ/mới duyệt/đã dùng/chuyển tiếp; áp dụng/đảo bảo lưu, đối chiếu phần đã dùng; mục bảo lưu trong preview/PDF |
| [#22 — PDF cập nhật](https://github.com/truongtoanr0602/EducationKeeptrack/issues/22) | Chưa có | Lịch sử bản xuất, bản lỗi thời, nguồn thay đổi và nút xuất bản mới; nhắc gửi lại, QR mới, không tự thu hồi bản đã gửi |

## Nhóm màn hình dùng lại

Đây là cách tổ chức tham chiếu cho danh sách thiếu, chưa phải bộ màn hình đã được sinh hoặc duyệt trong Stitch.

| Nhóm | Ticket sử dụng | Thứ tự đề xuất |
| --- | --- | --- |
| Đăng nhập, danh sách/tạo lớp | #2 | 1 — hoàn thiện lát cắt đầu tiên |
| Chi tiết lớp và danh sách học sinh | #3 | 2 |
| Lịch/buổi học và điểm danh | #4 | 3 |
| Chấm bài thủ công và hồ sơ tiến độ | #6, #18 | 4 — nhánh học tập |
| Bảng phí, đơn giá và nhận tiền | #5, #7 | 4 — nhánh tài chính, song song thiết kế học tập |
| Tài khoản nhận tiền và QR | #8 | 5 |
| Điều chỉnh phí, sửa/hủy giao dịch, thống kê | #9, #10, #11 | 5 |
| Preview/PDF và lịch sử bản xuất | #12, #22 | 6 — dùng dữ liệu học tập và tài chính đã chốt |
| Hướng dẫn, ảnh bài, AI chấm/lỗi/chấm lại | #13, #14, #15, #16 | 6 — mở rộng màn hình chấm bài |
| Nhận xét AI và cảnh báo | #17, #19 | 7 — mở rộng báo cáo/hồ sơ |
| Trả trước, bảo lưu và đối chiếu PDF | #20, #21 | 7 — mở rộng bảng phí/báo cáo |

Thứ tự này là ưu tiên thiết kế. Các phụ thuộc triển khai chính thức vẫn theo `Blocked by` trên GitHub, không phát sinh cạnh phụ thuộc mới từ việc nhóm màn hình.

## Điều kiện đánh dấu một ticket đủ thiết kế

- Có resource màn hình hoặc biến thể Stitch cụ thể và đối chiếu đủ tiêu chí UI của ticket.
- Có bố cục điện thoại và máy tính hoặc bằng chứng responsive đã được kiểm tra; hiện mới có đăng nhập MOBILE.
- Có trạng thái thành công, rỗng, lỗi, đang tải, thiếu dữ liệu và xung đột phù hợp với tính năng; không chỉ một ảnh happy path.
- Màu đi kèm nhãn; hai chỉ số và bốn mức đúng yêu cầu; dữ liệu tài chính/QR theo spec; không thêm dịch vụ hoặc chính sách chưa duyệt.
- Người dùng duyệt thiết kế trước khi đánh dấu đã duyệt; có HTML không đồng nghĩa đã kết nối API hoặc đạt nghiệm thu tính năng.

Lần kiểm kê ban đầu chỉ đọc Stitch. Sau khi người dùng đồng ý thiết kế ticket #2, đã tạo/chỉnh các prototype và lưu bản local; chưa đăng bình luận hoặc thay trạng thái GitHub.




