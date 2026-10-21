# Education Keeptrack — Đặc tả sản phẩm và kỹ thuật

> Cập nhật 21/10/2026: dùng Cloudflare R2 private thay SeaweedFS/MinIO. [Thiết kế R2](../doc/cloudflare-r2.md) quy định cấu hình, quyền tải qua backend, kiểm thử và chuyển dữ liệu; thay thế hướng dẫn kho cũ. Ticket #28 chuyển cấu hình và adapter sang R2; nghiệm thu thực tế cần bucket test và credentials. CI và chuyển dữ liệu cũ lần lượt thuộc #29/#30.

| Thuộc tính | Giá trị |
| --- | --- |
| Phiên bản | 1.2 — Cloudflare R2 (21/10/2026) |
| Ngày lập | 16/10/2026 |
| Cơ sở nghiệp vụ | BRD Education Keeptrack 2.2 và các quyết định trong phiên làm rõ yêu cầu |
| Điểm kiểm thử đã duyệt | API hệ thống, dữ liệu lưu trữ thật trong môi trường kiểm thử, mô phỏng AI và lỗi QR; kiểm tra riêng PDF |
| Nơi quản lý công việc | GitHub Issues: truongtoanr0602/EducationKeeptrack |

## Problem Statement

Giáo viên dạy thêm và gia sư độc lập mất thời gian quản lý danh sách lớp, điểm danh, chấm bài, theo dõi học phí và viết báo cáo tháng ở nhiều nơi. Các ghi nhận dễ thiếu nhất quán; tiền thu từng phần, nợ cũ và bảo lưu khó đối chiếu. Giáo viên cần biết học sinh nào đang sa sút nhưng không có dữ liệu tập trung để đánh giá.

Sản phẩm phải hỗ trợ cả giáo viên tự chấm và giáo viên dùng AI, giữ quyền quyết định kết quả cuối cùng cho giáo viên. Phụ huynh cần PDF rõ ràng về tình hình học tập và khoản phải trả, không cần tài khoản riêng.

## Solution

Cung cấp một Mini-CRM riêng cho từng giáo viên: thiết lập lớp và lịch học, điểm danh từng buổi, đánh giá bài bằng hai chỉ số thống nhất, tổng hợp xu hướng và nhận xét, tính phí theo buổi thực học, quản lý từng khoản nhận tiền, nợ chuyển tiếp và bảo lưu, xuất báo cáo PDF kèm thông tin thanh toán.

Giáo viên chọn tự chấm hoặc AI chấm trước khi xử lý bài; AI chỉ đề xuất, giáo viên có thể sửa rồi xác nhận. Nhận xét và cảnh báo sử dụng kết quả cuối đã xác nhận. Giao dịch nhận tiền, dữ liệu học tập và các phiên bản báo cáo được lưu đủ để đối chiếu.

## User Stories

1. Là giáo viên, tôi muốn truy cập tài khoản riêng để quản lý dữ liệu của mình an toàn.
2. Là giáo viên, tôi muốn tạo lớp với tên, môn và mô tả để tổ chức học sinh theo lớp.
3. Là giáo viên, tôi muốn thêm học sinh và thông tin phụ huynh để liên hệ và lập báo cáo.
4. Là giáo viên, tôi muốn ngăn thêm trùng học sinh trong một lớp để tránh điểm danh và thu phí trùng.
5. Là giáo viên, tôi muốn thiết lập nhiều lịch cố định trong tuần để chuẩn bị các buổi học.
6. Là giáo viên, tôi muốn đặt đơn giá mỗi buổi và thời điểm áp dụng để tính đúng học phí khi đổi giá.
7. Là giáo viên, tôi muốn ghi nhận học bù hoặc hủy buổi để phản ánh lịch thực tế.
8. Là giáo viên, tôi muốn theo dõi học sinh đang học, nghỉ có phép hoặc dừng học để giữ lịch sử chính xác.
9. Là giáo viên, tôi muốn buổi mới mặc định chưa điểm danh để không tự tính phí khi thiếu thông tin.
10. Là giáo viên, tôi muốn dòng có mặt nổi bật và toàn bộ dòng vắng bôi xám, có nhãn chữ để thao tác nhanh.
11. Là giáo viên, tôi muốn ghi lý do vắng và sửa điểm danh nhầm để dữ liệu học tập và học phí khớp.
12. Là giáo viên, tôi muốn được nhắc các trường hợp chưa điểm danh trước khi chốt tháng để tránh bỏ sót.
13. Là giáo viên, tôi muốn chọn cách chấm mặc định cho một lượt để giảm thao tác lặp.
14. Là giáo viên, tôi muốn đổi cách chấm riêng từng học sinh trước khi xử lý để phù hợp từng bài.
15. Là giáo viên tự chấm, tôi muốn chọn mức hoàn thành và kết quả làm đúng độc lập để phân biệt lượng bài đã làm với chất lượng bài.
16. Là giáo viên, tôi muốn mỗi chỉ số có bốn nút 25%, 50%, 75%, 100% để chấm nhanh và nhất quán.
17. Là giáo viên, tôi muốn ghi trạng thái không làm bài hoặc không có câu đúng để phản ánh kết quả bằng 0 mà không thêm nút phần trăm.
18. Là giáo viên, tôi muốn không giao bài và chưa đánh giá được phân biệt với kết quả kém để thống kê không gây hiểu sai.
19. Là giáo viên chọn AI, tôi muốn cung cấp đề, đáp án và hướng dẫn một lần cho mỗi bài để dùng chung khi chấm học sinh.
20. Là giáo viên, tôi muốn tải nhiều ảnh đúng học sinh và thứ tự trang để AI có đủ nội dung bài.
21. Là giáo viên, tôi muốn AI đề xuất hai mức đánh giá và chỉ ra lỗi có căn cứ để giảm công chấm.
22. Là giáo viên, tôi muốn sửa và xác nhận kết quả AI để chịu trách nhiệm về đánh giá cuối cùng.
23. Là giáo viên, tôi muốn xem kết quả AI gốc và lịch sử chỉnh sửa để đối chiếu khi cần.
24. Là giáo viên, tôi muốn biết ảnh mờ, thiếu trang hoặc thiếu hướng dẫn để bổ sung đúng thông tin.
25. Là giáo viên, tôi muốn chuyển bài AI lỗi sang tự chấm để không bị gián đoạn công việc.
26. Là giáo viên, tôi muốn kết quả AI về muộn không thay bài đã chuyển sang tự chấm để giữ quyết định của mình.
27. Là giáo viên, tôi muốn biết bài nào dùng hướng dẫn cũ sau khi sửa đáp án để chủ động chọn chấm lại.
28. Là giáo viên, tôi muốn được cảnh báo trước khi chấm lại thay kết quả đã sửa để tránh mất công điều chỉnh.
29. Là giáo viên, tôi muốn xem xu hướng hai chỉ số theo thời gian để phát hiện học sinh cần hỗ trợ.
30. Là giáo viên, tôi muốn cảnh báo có dữ liệu và giai đoạn làm căn cứ để quyết định biện pháp phù hợp.
31. Là giáo viên, tôi muốn ghi đã xem và hành động hỗ trợ để theo dõi việc xử lý cảnh báo.
32. Là giáo viên, tôi muốn AI phác thảo nhận xét tháng và lộ trình từ kết quả đã xác nhận để tiết kiệm thời gian.
33. Là giáo viên, tôi muốn chỉnh lời nhận xét và chỉ tạo lại khi yêu cầu để giữ nội dung đã biên tập.
34. Là giáo viên, tôi muốn nhận xét được đánh dấu cần cập nhật khi sửa đánh giá để không gửi nội dung lỗi thời.
35. Là giáo viên, tôi muốn tự viết nhận xét khi AI lỗi để vẫn xuất được báo cáo.
36. Là giáo viên, tôi muốn tính phí từ buổi thực học và đúng đơn giá từng buổi để giải thích được khoản thu.
37. Là giáo viên, tôi muốn thêm khoản điều chỉnh tăng hoặc giảm có lý do để xử lý thỏa thuận riêng minh bạch.
38. Là giáo viên, tôi muốn ghi từng lần nhận tiền, ngày nhận và ghi chú để quản lý thanh toán một phần.
39. Là giáo viên, tôi muốn chuyển nợ cũ sang tổng cần thu và mặc định trả nợ cũ trước để hạn chế nợ bị bỏ sót.
40. Là giáo viên, tôi muốn thống kê theo cả ngày nhận tiền và kỳ học phí để phân biệt tiền thực thu với công nợ.
41. Là giáo viên, tôi muốn sửa hoặc hủy giao dịch nhầm có lý do và lịch sử để khôi phục số dư đúng.
42. Là giáo viên, tôi muốn duyệt bảo lưu từ tiền đã thu nhưng chưa dùng khi nghỉ ốm có phép để bảo vệ quyền lợi học sinh.
43. Là giáo viên, tôi muốn bảo lưu chỉ được sử dụng một lần và theo dõi phần dư để tránh khấu trừ trùng.
44. Là giáo viên, tôi muốn ưu tiên VietQR có số tiền/nội dung và dùng QR tĩnh khi lỗi để phụ huynh thuận tiện chuyển khoản.
45. Là giáo viên, tôi muốn vẫn xuất PDF có thông tin chuyển khoản khi không có QR để không bị chặn công việc.
46. Là giáo viên, tôi muốn sửa thông tin, nhận xét trên bản xem trước và sửa số tiền từ dữ liệu nguồn để PDF và dư nợ khớp nhau.
47. Là giáo viên, tôi muốn xuất PDF riêng từng học sinh với emoji tối giản để chia sẻ báo cáo dễ đọc.
48. Là phụ huynh, tôi muốn thấy buổi học, hai mức đánh giá, lộ trình, nợ cũ, tiền đã trả và tổng phải trả để hiểu tình hình của con.
49. Là phụ huynh, tôi muốn nhận bản PDF cập nhật khi số liệu thay đổi để thanh toán đúng.
50. Là giáo viên, tôi muốn dữ liệu, ảnh và báo cáo của mình không bị giáo viên khác truy cập kể cả qua đường dẫn trực tiếp để bảo vệ học sinh.

51. Là giáo viên, tôi muốn tự đăng ký và xác minh email để gửi yêu cầu dùng dịch vụ.
52. Là giáo viên chờ duyệt, tôi muốn xem hướng dẫn và gửi mã giao dịch để Admin đối chiếu thanh toán.
53. Là Admin, tôi muốn duyệt/từ chối và cấp kỳ quyền sử dụng có lịch sử để quản lý dịch vụ trả phí.
54. Là giáo viên hết hạn, tôi muốn xem/xuất dữ liệu cũ và được hướng dẫn gia hạn để giữ công việc đã làm.
55. Là Admin, tôi muốn gia hạn nối tiếp đúng ngày và giữ cấu hình từng kỳ để không làm mất quyền đã mua.
56. Là Admin, tôi muốn bật/tắt quyền AI riêng mỗi tài khoản để cung cấp lựa chọn chỉ thao tác thủ công.
57. Là giáo viên, tôi muốn thấy lượt AI còn dùng, đang xử lý và đã dùng để kiểm soát gói được cấp.
58. Là giáo viên, tôi muốn lỗi AI không mất lượt và gửi lặp không tính hai lần để hạn mức chính xác.
59. Là Admin, tôi muốn khóa/mở tài khoản và vô hiệu phiên để xử lý quyền truy cập có kiểm soát.
60. Là giáo viên, tôi muốn đặt lại mật khẩu và đổi email đã xác minh mà giữ quyền đã mua.
61. Là Admin, tôi muốn đăng nhập bằng xác thực hai bước để bảo vệ quyền quản lý dịch vụ.
62. Là giáo viên, tôi muốn AI đã được nhận trước hết hạn/tắt quyền vẫn hoàn tất để không mất công việc hợp lệ.

## Implementation Decisions

### 1. Hiện trạng và mức độ chốt

- Workspace hiện chỉ có tài liệu và bộ skills; chưa có backend, API, schema hay test để mở rộng. Không có bộ kiểm thử hiện hữu làm tiền lệ.
- Nguồn ràng buộc kỹ thuật là đề cương học phần: .NET Modular Monolith, Clean Architecture trong từng module, DbContext/schema tách biệt, giao tiếp qua hợp đồng và in-memory event bus, Cloudflare R2 qua S3 API, Docker Compose và Jenkins CI/CD.
- Các quy tắc nghiệp vụ đã chốt dưới đây là bắt buộc. Ranh giới module, mô hình lưu trữ và hợp đồng lệnh là thiết kế đề xuất để thực hiện yêu cầu, chưa phải kiến trúc đã được triển khai.
- Đã chốt React + TypeScript + Vite, PostgreSQL schema/DbContext riêng từng module; ASP.NET Core Identity, email/mật khẩu, cookie HttpOnly cùng origin. Chưa chốt phiên bản .NET/thư viện, nhà cung cấp email/AI và môi trường triển khai cụ thể.

### 2. Thuật ngữ và quyền sở hữu

| Khái niệm | Ý nghĩa |
| --- | --- |
| Giáo viên sở hữu | Tài khoản là phạm vi cô lập dữ liệu; danh tính lấy từ phiên xác thực, không tin giá trị do client gửi |
| Buổi học | Lần học cụ thể có thời gian, trạng thái và đơn giá áp dụng; khác lịch tuần dự kiến |
| Bài đánh giá | Đề bài và các phiên bản đáp án/hướng dẫn dùng chung cho một lượt chấm |
| Bài nộp | Bài của một học sinh trong một bài đánh giá; có một cách chấm hiện hành |
| Lượt AI | Một lần xử lý cụ thể, gắn với bài nộp và phiên bản hướng dẫn |
| Kết quả cuối | Hai chỉ số do giáo viên xác nhận; nguồn chính thức cho báo cáo và cảnh báo |
| Giao dịch nhận tiền | Một khoản thực nhận, có ngày nhận và lịch sử sửa/hủy |
| Phân bổ thanh toán | Phần của một giao dịch dùng trả nghĩa vụ của một kỳ học phí |
| Bảo lưu | Tiền đã thu chưa sử dụng, được duyệt chuyển tiếp; không phải tiền mới thu |
| Bản xuất báo cáo | PDF tại một phiên bản dữ liệu, có thời điểm xuất và thông tin đối chiếu |

### 3. Ranh giới module và giao tiếp

| Module | Dữ liệu và trách nhiệm sở hữu | Hợp đồng cung cấp |
| --- | --- | --- |
| Identity | Tài khoản, email, vai trò, TOTP Admin, phiên có thể thu hồi | Đăng ký/xác minh/đăng nhập/khôi phục, danh tính và trạng thái khóa |
| ServiceAccess | Hồ sơ duyệt, phí dịch vụ, kỳ quyền sử dụng, quyền AI và sổ hạn mức | Đối chiếu/duyệt/gia hạn; kiểm tra đọc/ghi/AI, giữ/trừ/hoàn lượt |
| Classroom | Lớp, học sinh, liên hệ, đăng ký lớp, lịch, buổi thực tế, điểm danh, đơn giá hiệu lực | Tạo/cập nhật lớp, buổi, điểm danh; đọc dữ liệu buổi có phiên bản |
| Learning | Bài đánh giá, hướng dẫn có phiên bản, bài nộp, cách chấm, lượt AI, kết quả gốc/cuối, lỗi và lịch sử | Chọn cách chấm, xử lý AI, tự chấm, xác nhận, chấm lại, chuyển sang thủ công; đọc hồ sơ chính thức |
| Billing | Kỳ phí, phí từng buổi, điều chỉnh, tiền nhận, phân bổ, bảo lưu, số dư, tài khoản nhận tiền | Tính phí, ghi/hủy/điều chỉnh tiền nhận, duyệt/dùng bảo lưu, đối chiếu dư nợ, lấy yêu cầu thanh toán |
| Reporting | Bản nháp nhận xét, xu hướng/cảnh báo, hành động hỗ trợ, tùy chỉnh trình bày, bản xuất | Tạo/chỉnh nhận xét, đọc xu hướng, xem trước, xuất PDF |

- Mỗi module có schema và DbContext riêng; chỉ module sở hữu được ghi dữ liệu. Module khác dùng hợp đồng công khai hoặc sự kiện, không đọc trực tiếp bảng/DbContext của nhau.
- S3, AI, sinh QR và dựng PDF là adapter phía ngoài các module; không tạo module nghiệp vụ riêng chỉ vì khác nhà cung cấp.
- Xử lý chấm AI và sinh nhận xét chạy nền, không giữ yêu cầu HTTP chờ lâu. Lưu trạng thái công việc trong dữ liệu bền vững để khôi phục sau khởi động lại.
- Phát các sự kiện thay đổi điểm danh/đơn giá, xác nhận kết quả, thay hướng dẫn và thay số dư. Sự kiện có ID, phạm vi giáo viên, ID đối tượng và phiên bản; handler bỏ qua bản đã xử lý.
- Đề xuất lưu sự kiện chờ phát cùng giao dịch nguồn rồi chuyển qua in-memory bus; cơ chế này tránh mất cập nhật khi tiến trình dừng. Các module xử lý lặp an toàn.
- Đồng bộ liên module có thể diễn ra trễ ở màn hình tổng quan. Khi xuất PDF hoặc QR, kiểm tra phiên bản dữ liệu nguồn; nếu bản tổng hợp cũ thì cập nhật/chờ xử lý hoặc báo cần thử lại, không xuất số liệu sai.

### 4. Mô hình dữ liệu và bất biến

- Mọi đối tượng thuộc giáo viên đều có khóa phạm vi sở hữu; kiểm tra quyền cả đối tượng cha và tham chiếu liên quan. Không cho gắn ảnh/học sinh/giao dịch của giáo viên khác bằng ID.
- Đăng ký học sinh vào lớp có thời điểm bắt đầu/kết thúc; ngăn đăng ký trùng đang hiệu lực. Điểm danh duy nhất theo buổi và học sinh.
- Mỗi buổi có trạng thái dự kiến/đã diễn ra/hủy, đơn giá áp dụng và phiên bản. Đổi lịch không tự sửa lịch sử buổi; hủy không tạo phí.
- Bài đánh giá có nhiều phiên bản hướng dẫn bất biến. Bài nộp duy nhất theo bài đánh giá và học sinh; nhiều ảnh theo thứ tự, nhiều lượt AI nhưng chỉ một lượt hiện hành.
- Hai chỉ số lưu dưới dạng mức 25/50/75/100 hoặc trạng thái riêng. Lưu dữ liệu nhận diện/tỷ lệ AI gốc riêng với mức quy đổi và kết quả cuối để không đánh đồng tỷ lệ thực với nhóm đánh giá.
- Kết quả không làm bài có hai chỉ số bằng 0; không có câu đúng chỉ đặt kết quả làm đúng bằng 0. Không giao/chưa đánh giá có giá trị thống kê thiếu, không phải 0. Thất bại AI là trạng thái xử lý, không phải kết quả học lực.
- Tiền lưu chính xác theo VND bằng kiểu số nguyên hoặc decimal phù hợp, không dùng số thực dấu phẩy động. Giao dịch có số tiền dương; khoản điều chỉnh biểu diễn rõ chiều tăng/giảm và lý do.
- Phân bổ không vượt tiền giao dịch còn hiệu lực; tổng sử dụng bảo lưu không vượt khoản đã duyệt. Nguồn tiền đã chuyển sang bảo lưu không đồng thời phân bổ trả phí lần nữa.
- Lưu lịch sử kết quả và giao dịch với người thao tác, thời điểm và lý do khi cần. Hủy/điều chỉnh giao dịch tạo dấu vết đảo hoặc thay thế, không xóa lịch sử tài chính.
- Thời điểm hệ thống lưu UTC; ngày nhận tiền, buổi học và kỳ tháng được xác định theo Asia/Ho_Chi_Minh. Ngày thực nhận do giáo viên nhập khác thời điểm nhập liệu.

### 5. Quy tắc chấm và trạng thái công việc

- Cách chấm mặc định của lượt được áp vào các bài chưa bắt đầu; chọn riêng từng bài được trước xử lý. Tự chấm không gọi AI.
- Với AI: cung cấp đề, đáp án/hướng dẫn một lần mỗi bài; AI đọc ảnh và trả hai tỷ lệ có căn cứ cùng lỗi. Máy chủ kiểm tra dữ liệu trả về trước khi quy đổi; không tin trực tiếp kết quả từ nhà cung cấp.

| Tỷ lệ thực tế | Mức ghi nhận cho từng chỉ số |
| --- | --- |
| 0% | Trạng thái bằng 0 phù hợp |
| Trên 0% đến 25% | 25% |
| Trên 25% đến 50% | 50% |
| Trên 50% đến 75% | 75% |
| Trên 75% đến dưới 100% | 75% |
| Đúng 100% | 100% |

- Giữ nguyên bảng đã duyệt, không thay bằng làm tròn gần nhất. Ví dụ 60% và 90% đều ghi mức 75%; 100% chỉ khi đầy đủ/đúng toàn bộ. Tỷ lệ ngoài 0–100 hoặc thiếu căn cứ phải được báo lỗi/cần kiểm tra.
- Lượt AI đi qua chờ xử lý, đang xử lý, hoàn tất hoặc lỗi/hủy. Kết quả AI hoàn tất vẫn cần giáo viên sửa/xác nhận trước khi trở thành chính thức; giữ gốc bất biến.
- Ảnh mờ, thiếu trang, thiếu hướng dẫn hoặc độ chắc chắn thấp được thông báo cụ thể; không tự chuyển sang điểm 0. Tiêu chí độ chắc chắn còn cần chốt bằng bộ mẫu.
- Khi AI lỗi/không trả kết quả, thao tác chuyển sang tự chấm làm vô hiệu lượt AI hiện hành. Kết quả về muộn chỉ có thể được lưu đối chiếu trạng thái, không ghi vào kết quả cuối.
- Khi nhận kết quả nền, kiểm tra ID lượt hiện hành, cách chấm và phiên bản bài nộp trong cùng giao dịch. Sử dụng kiểm soát phiên bản để ngăn cập nhật tranh chấp.
- Sửa hướng dẫn tạo phiên bản mới và đánh dấu bài dùng bản cũ. Không tự chấm lại. Giáo viên chọn bài; bài đã sửa kết quả phải yêu cầu xác nhận thay thế trước khi gửi lượt mới.
- Không tạo đầu điểm mới khi chấm lại; giữ kết quả chính thức cũ cho đến khi xác nhận bản thay thế, kèm trạng thái đang chấm lại/cần cập nhật để tránh hiểu nhầm.

### 6. Nhận xét, xu hướng và cảnh báo

- Dùng điểm danh và kết quả cuối đã xác nhận của cả hai cách chấm. AI gốc không được lẫn vào dữ liệu chính thức.
- Tần suất làm bài dùng các buổi có giao và có đánh giá; trạng thái không làm có tính, không giao/chưa đánh giá loại khỏi mẫu. Hiển thị độ đầy đủ của dữ liệu.
- Nhận xét lưu riêng bản AI phác thảo và lời giáo viên biên tập cùng phiên bản dữ liệu nguồn. Sửa kết quả/điểm danh liên quan làm nhận xét lỗi thời; không tự ghi đè lời đã sửa.
- Tạo lại chỉ khi giáo viên yêu cầu và không chấm lại ảnh. Kết quả AI viết nhận xét trả về cho phiên bản cũ phải mang nhãn lỗi thời, không âm thầm thay bản hiện hành.
- Giáo viên được tự viết khi AI lỗi; chỉ nội dung đã duyệt đi vào PDF. Không tự gửi nhận xét/cảnh báo cho phụ huynh.
- Cảnh báo phải có dấu hiệu, giai đoạn và bằng chứng; chỉ là gợi ý, không khẳng định mất gốc. Dữ liệu thiếu hoặc nghỉ ốm có phép không tự thành sa sút.
- Chưa đủ dữ liệu thì trả trạng thái chưa đủ dữ liệu. Ngưỡng, lịch sử tối thiểu và mô hình học máy chưa được chốt; không thay bằng một mô hình hoặc bộ quy tắc tự đặt rồi coi là đã nghiệm thu.

### 7. Học phí, phân bổ và bảo lưu

- Học phí phát sinh là tổng đơn giá các buổi thực tế có mặt. Chưa điểm danh, vắng, hủy hoặc lịch dự kiến không sinh phí. Học bù tính một lần ở buổi thực tế.
- Đơn giá mới không áp ngược lịch sử trừ thao tác điều chỉnh rõ ràng có lý do. Phí theo từng buổi phải đối chiếu được.
- Dư nợ kỳ bằng phí phát sinh cộng tăng, trừ giảm, trừ bảo lưu đã dùng, trừ tiền phân bổ cho kỳ; không âm. Tiền dư theo dõi riêng và chờ chính sách, không tự hoàn tiền.
- Tổng cần thu bằng tổng dư nợ các kỳ còn mở, gồm nợ cũ và kỳ hiện tại. Nợ không được sao chép thành khoản phí mới hàng tháng.
- Khi nhận tiền, mặc định phân bổ kỳ cũ nhất còn nợ trước rồi kỳ mới. Ghi nhận khoản nhận và phân bổ trong giao dịch nguyên tử; kiểm soát đồng thời để hai lần thu không cùng trả một khoản nợ quá mức.
- Ví dụ nợ cũ 500.000 đồng, phí mới 900.000 đồng, nhận 700.000 đồng: phân bổ 500.000 đồng vào kỳ cũ, 200.000 đồng kỳ mới; còn 700.000 đồng.
- Thu ngày 05/11 trả nợ tháng 10: thực thu tháng 11 tăng, nợ kỳ tháng 10 giảm. Báo cáo tiền thực thu dùng ngày nhận; báo cáo nghĩa vụ dùng kỳ học. Bảo lưu không là thực thu lần nữa.
- Điều chỉnh/hủy tiền nhận có lý do và lịch sử; đảo phân bổ liên quan rồi tính lại theo sổ hiện hành một cách nguyên tử. Nếu phát sinh số dư/vấn đề chính sách chưa chốt, báo rõ thay vì tự hoàn tiền hoặc âm dư nợ.
- Bảo lưu chỉ từ tiền đã nhận và chưa sử dụng đủ điều kiện, do giáo viên duyệt cho nghỉ ốm có phép. Không tạo bảo lưu từ buổi vắng chưa trả tiền.
- Ví dụ trả trước 1.200.000 đồng, thực học dùng 750.000 đồng: tối đa xét phần chưa dùng 450.000 đồng; nếu duyệt, kỳ sau phí 900.000 đồng dùng bảo lưu 450.000 đồng còn 450.000 đồng cần thu.
- Điều kiện đủ dài ngày, thời hạn, phạm vi lớp và xử lý dừng/chuyển lớp còn mở. Chưa tự động hóa các chính sách này; không ngầm coi chuyển lớp/chuyển học sinh/hoàn tiền là được phép.

### 8. Hợp đồng API và xử lý lỗi

Các nhóm thao tác công khai dưới đây là hợp đồng hành vi, tên tuyến cụ thể sẽ theo quy ước backend khi khởi tạo:

| Nhóm lệnh/truy vấn | Đầu vào chính | Kết quả và ràng buộc |
| --- | --- | --- |
| Thiết lập lớp/lịch/đơn giá | Thông tin lớp, ngày hiệu lực, lịch tuần | ID và phiên bản; lỗi nếu trùng đăng ký hoặc dữ liệu không hợp lệ |
| Điểm danh | Buổi, học sinh, trạng thái, ghi chú, phiên bản | Trạng thái đã lưu; tạo cập nhật tổng hợp liên quan |
| Tự chấm/xác nhận | Bài nộp, hai mức/trạng thái, phiên bản | Kết quả cuối và lịch sử; kiểm tra cách chấm/trạng thái hợp lệ |
| Bắt đầu AI | Bài nộp, ảnh, phiên bản hướng dẫn, xác nhận thay thế nếu cần | Chấp nhận xử lý nền, ID lượt và trạng thái; chưa trả kết quả chính thức |
| Đọc/chuyển lượt AI | ID lượt hoặc bài, phiên bản | Tiến độ hoặc chuyển thủ công; lượt cũ mất quyền cập nhật |
| Tạo nhận xét | Học sinh, kỳ, phiên bản dữ liệu | ID công việc nền hoặc bản nháp; không tự gửi phụ huynh |
| Ghi/điều chỉnh/hủy tiền | Học sinh, tiền, ngày nhận, lý do, phiên bản, khóa chống lặp | Giao dịch, phân bổ, số dư mới; áp dụng nguyên tử |
| Duyệt/dùng bảo lưu | Nguồn tiền, khoảng nghỉ, khoản duyệt/dùng và lý do | Sổ bảo lưu và số dư; ngăn dùng trùng hoặc vượt nguồn |
| Đọc bảng phí/xem trước | Học sinh, kỳ, tùy chỉnh nội dung | Các dòng phí, nợ, tiền phân bổ, bảo lưu, phiên bản nguồn và cảnh báo |
| Xuất PDF/QR | Bản nháp, phiên bản nguồn đã xem | Bản xuất nhất quán hoặc thông báo nguồn đã đổi; QR có loại và số tiền tương ứng |

- Mọi thao tác cần xác thực và kiểm tra sở hữu; yêu cầu bị từ chối không tiết lộ dữ liệu của tài khoản khác.
- Lỗi trả mã ổn định, thông điệp tiếng Việt, trường cần sửa và ID đối chiếu. Phân biệt thiếu quyền, dữ liệu sai, xung đột phiên bản, phụ thuộc ngoài lỗi và công việc đang xử lý.
- Lệnh dễ gửi lại như thu tiền, duyệt bảo lưu, tạo lượt AI và xuất báo cáo nhận khóa chống lặp. Cùng khóa và cùng nội dung trả kết quả đã có; cùng khóa khác nội dung báo xung đột.
- Dùng phiên bản kỳ vọng cho cập nhật; khi hai tab cùng sửa, yêu cầu tải lại thay vì ghi đè âm thầm.
- Giới hạn kích thước/số trang, loại tệp, tần suất AI và thời gian xử lý là cấu hình triển khai cần thống nhất. Ảnh tải lên được kiểm tra thực tế, gắn chủ sở hữu; không tin tên hoặc đường dẫn tệp do người dùng cung cấp.

### 9. QR và PDF

- Thứ tự QR: VietQR có số tiền/nội dung; nếu không tạo được thì QR ngân hàng tĩnh kèm hướng dẫn nhập số tiền/nội dung; nếu không có cả hai vẫn xuất PDF với thông tin chuyển khoản và cảnh báo thiếu QR.
- Chưa có tài khoản nhận tiền hợp lệ thì báo thiếu, không tạo mã giả. Tổng bằng 0 không tạo yêu cầu thanh toán. Quét QR không đổi trạng thái thu tiền.
- Mã gắn phiên bản số dư và thông tin ngân hàng. Khi số liệu đổi phải tạo mã/bản xuất mới; mã cũ sai tiền không được dùng trong PDF mới.
- Bản xem trước cho sửa tên/thông tin liên hệ hiển thị, nhận xét và lộ trình. Số buổi, đơn giá, tiền đã nhận và bảo lưu sửa qua nghiệp vụ nguồn; tăng/giảm riêng qua khoản điều chỉnh có lý do.
- Tùy chỉnh trình bày không làm đổi chủ sở hữu, học sinh liên quan hay lịch sử tài chính. Cập nhật hồ sơ gốc phải là thao tác tường minh riêng.
- Bản PDF lấy ảnh chụp dữ liệu nhất quán, gồm phiên bản nguồn, thời điểm xuất, hai chỉ số, phân tích đã duyệt, lộ trình, học phí/nợ và thông tin thanh toán. Nếu nguồn đổi khi đang tạo, báo cần tạo lại thay vì phát hành bản nửa cũ nửa mới.
- Chỉ xuất PDF cho phụ huynh. Hỗ trợ tiếng Việt, xuống dòng, emoji tiết chế; font có khả năng hiển thị và in dễ đọc. Không bắt buộc giữ mọi màu emoji.
- PDF đã tải/gửi bên ngoài không thể tự thu hồi. Khi sửa giao dịch/số liệu liên quan, đánh dấu bản xuất cũ cần cập nhật và nhắc giáo viên xuất/gửi lại. Chính sách mở lại báo cáo đã chốt vẫn cần xác nhận.

### 10. Bảo mật, lưu trữ và vận hành

- Ảnh bài và PDF lưu riêng tư trong Cloudflare R2 qua S3 API. Máy chủ kiểm tra sở hữu trước đọc, xuất hoặc cấp quyền tải; PDF tải qua API backend, không cấp presigned URL R2 trực tiếp. Token ứng dụng mặc định hạn 5 phút vẫn yêu cầu phiên đăng nhập và kiểm tra quyền ở mỗi lần tải; phản hồi no-store, không ghi token vào log.
- Phụ huynh nhận PDF do giáo viên chia sẻ, chưa có cổng phụ huynh và luồng tự gửi qua email/tin nhắn.
- Nhà cung cấp AI chỉ nhận dữ liệu cần chấm/nhận xét; nội dung ảnh là dữ liệu học sinh, không phải chỉ thị hệ thống. Kết quả AI phải qua kiểm tra cấu trúc, miền giá trị và phạm vi bài.
- Cấu hình/secrets nằm ngoài mã nguồn; log dùng ID công việc và lỗi đã loại thông tin nhạy cảm. Thời hạn lưu ảnh, quyền xóa và điều kiện dùng dữ liệu huấn luyện còn chờ xác nhận.
- Docker Compose chạy backend và PostgreSQL bền vững; R2 là dịch vụ ngoài qua HTTPS; pipeline Jenkins thực hiện build và kiểm thử trước bước triển khai được cấu hình. Việc triển khai hạ tầng thật không thuộc hành động của spec này.
- Có khả năng sao lưu/khôi phục dữ liệu và ảnh tương ứng; thử khôi phục trong môi trường kiểm thử. Các mục tiêu tốc độ/quy mô chưa được ấn định khi chưa có số liệu.

### 11. Quyết định đã chốt: nền tảng, Admin và dịch vụ trả phí

**Trạng thái: accepted — 16/10/2026.** Các quyết định này thay thế mọi mô tả trước đây về công nghệ/xác thực còn chưa chốt; nghiệp vụ học sinh giữ nguyên. Thuật ngữ phí dịch vụ và học phí phải tách biệt.

#### Công nghệ và phiên

- React + TypeScript + Vite; PostgreSQL một database với schema/DbContext riêng từng module; .NET Modular Monolith theo đề cương. Không dùng schema làm thay thế kiểm tra tenant/giáo viên sở hữu.
- ASP.NET Core Identity quản lý email/mật khẩu, xác minh, khôi phục và TOTP Admin. UI/API cùng origin sau reverse proxy; cookie HttpOnly, Secure qua HTTPS, chống CSRF cho lệnh ghi. Không lưu token đăng nhập ở localStorage; chưa dùng JWT/Google OAuth/số điện thoại.
- Phiên giáo viên: idle 2 giờ, absolute 24 giờ. Admin: idle 30 phút, absolute 8 giờ. Lưu dấu vết phiên có thể thu hồi; khóa/đổi email/đặt lại mật khẩu làm phiên cũ vô hiệu tại request tiếp theo, không chỉ chờ cookie hết hạn. Hoạt động polling nền không được vượt giới hạn absolute hoặc giữ người dùng hoạt động giả.
- Khôi phục mật khẩu qua liên kết một lần có hạn, phản hồi không tiết lộ tài khoản; đổi email chỉ có hiệu lực sau xác minh mới, giữ ID/quyền đã mua. Thời hạn cụ thể token và dịch vụ gửi email là cấu hình triển khai chưa chốt.
- Đăng ký công khai luôn tạo giáo viên. Admin đầu tiên thiết lập riêng qua công cụ vận hành có kiểm soát, không endpoint công khai; bắt buộc TOTP từ ứng dụng. Lựa chọn quy trình dự phòng mất thiết bị Admin cần xác nhận trước vận hành production, không tự bỏ 2FA.

#### Trách nhiệm dữ liệu

ServiceAccess sở hữu yêu cầu đối chiếu phí dịch vụ, trạng thái duyệt, cấu hình gói, kỳ quyền sử dụng và quyền AI/hạn mức. Identity sở hữu danh tính/phiên/khóa. Billing tiếp tục chỉ sở hữu học phí học sinh; không tái sử dụng sổ thu của học sinh để ghi tiền mua phần mềm. Admin không có quyền đọc mặc định đối với Classroom/Learning/Billing hoặc ảnh/PDF học sinh.

Mỗi kỳ lưu snapshot ngày bắt đầu/kết thúc và hạn mức từng loại. Mẫu gói đổi chỉ ảnh hưởng kỳ mới; thay kỳ hiện tại có lý do, người/thời điểm và kiểm soát phiên bản. Đơn giá gói, số lượt và độ dài kỳ không tự đặt thành chính sách đã duyệt.

#### Đăng ký, cấp quyền và gia hạn

- Đăng ký → xác minh email → chờ duyệt → Admin đối chiếu tiền → duyệt/cấp kỳ hoặc từ chối có lý do. Email chưa xác minh không được kích hoạt nghiệp vụ. Tài khoản chờ chỉ truy cập hồ sơ đăng ký/trạng thái/hướng dẫn và gửi đối chiếu, không đọc dữ liệu lớp.
- Admin cấu hình hướng dẫn ngân hàng/liên hệ; giáo viên gửi mã giao dịch/ghi chú. Không có cổng thanh toán hoặc webhook tự cấp quyền. Các thao tác đối chiếu/duyệt phải chống lặp để một yêu cầu không cấp hai kỳ.
- Còn hạn gia hạn bắt đầu tại cuối kỳ hiện hành; đã hết hạn bắt đầu tại lúc Admin duyệt, Admin xem trước ngày. Thời gian dùng khoảng [bắt đầu, kết thúc), ranh giới tháng/ngày hiển thị theo Việt Nam, lưu mốc UTC.
- Hết hạn cho đọc/xuất dữ liệu đã có, chặn ghi mới và AI mới. Xuất PDF ở chế độ này phải là truy vấn/kết xuất, không mở quyền sửa nhận xét/học phí; metadata kỹ thuật tạo file có thể lưu nhưng không thay dữ liệu nghiệp vụ.
- Khóa chặn truy cập cả dữ liệu cũ, thu hồi phiên và vô hiệu công việc AI. Mở khóa không tự cấp kỳ mới. Chỉ API server quyết định quyền; UI thể hiện hạn và trạng thái, không là lớp kiểm soát duy nhất.

#### Quyền AI, hạn mức và xử lý đồng thời

- Điều kiện nhận AI mới: email xác minh + đã duyệt + không khóa + kỳ hiện hành còn hiệu lực + quyền AI bật + hạn mức tương ứng còn đủ. Không có AI vẫn tự chấm/viết nhận xét/xem biểu đồ/xuất trong kỳ còn hạn.
- Tắt quyền AI chặn chấm/sinh nhận xét/cảnh báo AI mới, giữ dữ liệu AI cũ. Bật lại không reset số đã dùng. Nếu chưa cấp hạn mức cho kỳ hiện hành thì không nhận yêu cầu AI.
- Hạn mức tách loại chấm một bài của một học sinh và sinh nhận xét tháng một học sinh. Cấp lại theo kỳ, không theo tháng dương lịch; gia hạn sớm chỉ cấp lượt mới khi kỳ mới bắt đầu; phần dư kỳ cũ không chuyển tiếp.
- Sổ lượt lưu khóa yêu cầu, kỳ, loại tác vụ, trạng thái giữ/trừ/hoàn, liên kết công việc. Giữ trong giao dịch nguyên tử trước khi gửi; chỉ một kết quả hợp lệ được trừ. Lỗi/hủy trả giữ về đúng kỳ; phản hồi trùng không trừ hai lần. Chấm lại/tạo lại thành công là lượt mới.
- Lượt đã nhận thuộc kỳ ban đầu kể cả hoàn tất sau ranh giới kỳ; không chuyển phí lượt sang kỳ mới hoặc hồi sinh hạn mức kỳ đã hết. Còn dùng được bằng hạn mức trừ đã dùng trừ đang giữ; không âm.
- Kiểm tra quyền và giữ lượt phải không tạo khoảng tranh chấp với Admin tắt/khóa. Kiểm tra trạng thái khóa và ID lượt hiện hành khi áp dụng kết quả; khóa làm kết quả về muộn không được áp dụng.
- Hết hạn hoặc tắt AI cho phép lượt đã nhận hoàn tất/lưu bản nháp; nếu hết hạn chưa được sửa/xác nhận đến khi gia hạn. Không tự ghi đè lời đã biên tập. Khóa thì hủy/vô hiệu, hoàn giữ đúng kỳ.
- Cảnh báo AI chịu cùng cờ quyền; đơn vị tính lượt cảnh báo chưa chốt, không tự tính miễn phí hay gộp loại. Biểu đồ mô tả từ kết quả đã lưu vẫn là chức năng thủ công được phép.

#### API/UI bổ sung và nghiệm thu

- UI giáo viên: đăng ký, xác minh email, trạng thái chờ/từ chối, hướng dẫn chuyển khoản/gửi đối chiếu; hồ sơ quyền/ngày hết hạn, số lượt còn/giữ/đã dùng, quên mật khẩu và đổi email.
- UI Admin: đăng nhập/TOTP, danh sách đăng ký, đối chiếu/duyệt/từ chối, cấu hình hướng dẫn/gói, xem trước kỳ cấp/gia hạn, khóa/mở, bật/tắt AI, hạn mức và lịch sử. Không điều hướng tới dữ liệu học sinh theo quyền Admin.
- API server cung cấp các hành vi trên với role policy, CSRF, khóa chống lặp và expected version; lỗi phân biệt chưa xác minh/chờ duyệt/hết hạn/khóa/AI tắt/hết lượt mà không tiết lộ dữ liệu khác.
- Thêm kiểm thử API với đồng hồ điều khiển, hai giáo viên và Admin: CSRF/cookie, idle/absolute, thu hồi phiên, token email một lần, Admin không 2FA không được vào, không tự nâng role, không vượt phạm vi lớp.
- Kiểm thử kỳ nối tiếp/đã hết, snapshot gói, bật/tắt không reset, giữ lượt đồng thời, sự kiện lặp, lỗi/hủy/khởi động lại, khóa và phản hồi muộn; kiểm tra đúng loại/kỳ và khả năng thủ công khi AI không có quyền.
- Mô phỏng email/AI ở ranh giới; database thật cho giao dịch/quyền. Kiểm tra UI read-only, nội dung form khi bị chặn, không tự bật quyền qua client. Đối chiếu bổ sung AC-31–AC-38 của BRD.

## Testing Decisions

### Điểm kiểm thử đã được người dùng xác nhận

Dùng API hệ thống làm điểm kiểm thử chính, đi qua xác thực, nghiệp vụ và lưu trữ thật trong môi trường kiểm thử. Chỉ mô phỏng ranh giới phụ thuộc ngoài: AI, tạo QR và lỗi dịch vụ. PDF được dựng thực để kiểm tra nội dung/khả năng đọc. Chưa có API hoặc test hiện hữu để tái sử dụng; tạo bộ kiểm thử tại ranh giới công khai này khi triển khai.

Test tốt kiểm tra hành vi và dữ liệu quan sát được, không phụ thuộc tên lớp, bảng nội bộ hoặc thứ tự gọi handler. Các kiểm thử dùng hai tài khoản giáo viên, nhiều kỳ học, đồng hồ cố định, phản hồi AI điều khiển được và dữ liệu tài chính đối chiếu chính xác.

| Phạm vi | Tình huống bắt buộc | Đối chiếu BRD |
| --- | --- | --- |
| Classroom | Lịch nhiều ngày, trùng đăng ký, điểm danh thiếu/có/vắng, hủy/học bù, đổi giá giữa kỳ | AC-01, AC-02, AC-05, AC-06 |
| Learning | Hai chỉ số độc lập, bốn mức, các trạng thái 0/thiếu, không gọi AI khi tự chấm | AC-03, AC-18, AC-20 |
| AI chấm | Ảnh đủ/thiếu/mờ, lỗi phụ thuộc, xác nhận/sửa, giữ gốc, chấm lại không tạo trùng | AC-04, AC-14, AC-15 |
| Quy đổi | 0, 25, 50, 60, 75, 90, 100 và ngay hai phía các mốc; ngoài miền phải lỗi | AC-19 |
| Bất đồng bộ | Chuyển thủ công trước kết quả AI về, sự kiện lặp, dừng/khôi phục công việc, tranh chấp phiên bản | AC-21 và tính an toàn dữ liệu |
| Hướng dẫn | Sửa đáp án đánh dấu bản cũ, không tự gọi AI; yêu cầu xác nhận thay kết quả đã sửa | AC-22 |
| Nhận xét/cảnh báo | Chỉ dùng kết quả cuối, giữ lời sửa, đánh dấu lỗi thời, không đọc/chấm lại ảnh, thiếu dữ liệu, AI lỗi vẫn tự viết | AC-09, AC-16, AC-17, AC-23, AC-24 |
| Billing | Số buổi/giá, bảo lưu đúng nguồn/dùng một lần, không tiền trước không sinh bảo lưu, dư lớn hơn phí | AC-05, AC-06, AC-10, AC-11, AC-12 |
| Thu tiền | Trả một phần, nợ cũ trước, ngày thực thu khác kỳ học, hủy/sửa giữ lịch sử, lệnh lặp và thu đồng thời | AC-08, AC-25, AC-26, AC-27 |
| QR/PDF | Đúng số tiền/người nhận, không quét là đã trả, fallback hai tầng, số tiền 0, không dùng mã cũ, nguồn thay khi xuất | AC-07, AC-08, AC-28, AC-29 |
| Bảo mật | Giáo viên khác truy cập API/ảnh/PDF, tham chiếu chéo và URL hết hạn đều bị kiểm soát phù hợp | AC-13, AC-30 |

- Kiểm tra UI riêng cho màu/nhãn điểm danh, toàn dòng vắng, hai nhóm bốn nút và trạng thái riêng; không suy ra chất lượng giao diện từ API test.
- Kiểm tra PDF bằng trích xuất văn bản/số liệu và xem bản mẫu: tiếng Việt, emoji, xuống dòng, nhiều trang, không lẫn học sinh khác. Không chỉ so sánh byte PDF hoặc tạo test bám bố cục nội bộ.
- Kiểm thử tài chính chứng minh tổng tiền còn lại và phân bổ sau các chuỗi nhận/hủy/điều chỉnh; cập nhật lỗi phải rollback, không lưu nửa giao dịch.
- Kiểm thử chất lượng AI thật tách khỏi bộ API mô phỏng: dùng bộ bài gắn nhãn và dữ liệu cảnh báo do hai bên duyệt. API test xanh không chứng minh AI thực chấm chính xác hoặc mô hình phát hiện sa sút đạt yêu cầu.
- Nghiệm thu chức năng dùng AC-01–AC-38; phần phụ thuộc chính sách mở chỉ được nghiệm thu sau khi có quyết định và bộ mẫu tương ứng.

## Out of Scope

- Cổng/tài khoản phụ huynh và học sinh, trợ giảng, tài khoản dùng chung, trung tâm nhiều chi nhánh.
- Dạy trực tuyến, trả lương, tự đối chiếu ngân hàng, tự xác nhận thanh toán từ quét QR, hoàn tiền tự động và hóa đơn thuế.
- Xuất Word/DOCX cho phụ huynh; tự gửi PDF hoặc tin nhắn khi chưa có yêu cầu.
- Chấm thủ công theo thang điểm tự do hoặc dùng điểm số truyền thống làm chỉ số chính song song với hai mức đã chốt.
- Tự áp dụng kết quả AI chưa xác nhận, tự chấm lại khi đổi đáp án, tự ghi đè lời nhận xét đã sửa.
- Tự chọn chính sách bảo lưu, ngưỡng cảnh báo, nhà cung cấp AI hay chỉ tiêu chất lượng rồi coi là đã được duyệt.
- Triển khai mã nguồn, tạo PR, thiết lập hạ tầng thật hoặc phân rã thành nhiều issue triển khai trong bước lập spec này.

- Chưa có thanh toán dịch vụ tự động, gói dùng thử, OAuth, đăng nhập điện thoại hoặc JWT cho API bên thứ ba. Chưa tự đặt giá gói, số lượt hoặc cách tính lượt cảnh báo AI.

## Further Notes

### Quyết định còn mở và phạm vi sẵn sàng

Spec mô tả đầy đủ phần đã chốt và được xuất với nhãn workflow `ready-for-agent` theo skill to-spec. Nhãn này không thay thế việc xác nhận các chính sách còn mở. Agent triển khai có thể làm phần nền và hành vi đã xác định; phần phụ thuộc dưới đây cần được chốt trước khi tự động hóa hoặc nghiệm thu tương ứng.

| Nội dung còn mở | Ảnh hưởng | Cách xử lý trong khi chưa chốt |
| --- | --- | --- |
| Học sinh nhiều lớp | Đăng ký và phạm vi tổng hợp | Thiết kế có đăng ký lớp riêng; chưa coi tự chuyển dư/bảo lưu giữa lớp là được phép |
| Chính sách trả trước | Phân bổ tiền và điều kiện bảo lưu | Mô hình hỗ trợ nguồn tiền chưa dùng; chưa tự áp dụng chính sách ngoài ví dụ đã duyệt |
| Nghỉ dài ngày, minh chứng, hạn dùng bảo lưu, chuyển/dừng lớp, tiền dư | Duyệt và dùng số dư | Không tự sinh tiền hoặc hoàn tiền; cần giáo viên xác nhận theo chính sách được duyệt |
| Môn/dạng bài hỗ trợ, tiêu chí độ chắc chắn, bộ mẫu, chất lượng và thời gian AI | Adapter chấm và nghiệm thu thật | Có thể kiểm thử luồng bằng mô phỏng; chưa cam kết chấm mọi môn/mọi ảnh |
| Lịch sử tối thiểu, ngưỡng, mô hình phân tích xu hướng | Cảnh báo học tập | Không tự đặt kết luận mất gốc; cần bộ dữ liệu và tiêu chí trước nghiệm thu |
| Giữ/xóa ảnh, điều kiện dùng dữ liệu huấn luyện | Quy trình dữ liệu và nhà cung cấp | Bảo vệ riêng tư; chưa bổ sung sử dụng dữ liệu cho mục đích khác |
| Quy mô, hạn mức tải ảnh, thời gian phản hồi, sao lưu/khôi phục | Chất lượng vận hành | Cấu hình được và đo trong môi trường kiểm thử; mục tiêu số cần xác nhận |
| Mở lại báo cáo đã chốt | Quy trình phiên bản báo cáo | Giữ PDF cũ, đánh dấu cần cập nhật; điều kiện mở lại theo chính sách còn cần duyệt |

Khi có quyết định mới, cập nhật BRD và spec cùng nhau, giữ nguyên các quyết định đã xác nhận nếu chưa có yêu cầu thay đổi. Issue này là đặc tả tổng thể; các ticket triển khai sau phải nêu phần phụ thuộc còn mở thay vì bỏ qua chúng.
