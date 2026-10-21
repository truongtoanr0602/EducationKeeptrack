# TÀI LIỆU ĐẶC TẢ YÊU CẦU NGHIỆP VỤ (BRD)

## Dự án: Education Keeptrack

**Hệ thống quản lý lớp học, theo dõi học tập và học phí dành cho giáo viên dạy thêm, gia sư độc lập**

| Thông tin | Nội dung |
| --- | --- |
| Phiên bản | 2.2 — Bổ sung dịch vụ trả phí, Admin và quyền AI |
| Ngày lập | 16/10/2026 |
| Đối tượng đọc | Khách hàng, giáo viên, gia sư và người phụ trách dự án |
| Mục đích | Thống nhất phạm vi, quy trình vận hành và kết quả mong đợi trước khi triển khai |

> Các nội dung được đánh dấu **Đề xuất cần duyệt** là phương án nghiệp vụ để khách hàng xem xét, chưa được coi là chính sách đã thống nhất.

## 1. Tổng quan dự án và mục tiêu kinh doanh

### 1.1. Bối cảnh

Giáo viên dạy thêm và gia sư độc lập thường quản lý danh sách học sinh, điểm danh, kết quả học tập và học phí ở nhiều nơi khác nhau. Việc tổng hợp cuối tháng mất thời gian, dễ bỏ sót và gây khó khăn khi giải thích số tiền cần thu với phụ huynh.

Education Keeptrack là một hệ thống quản lý quan hệ với học sinh và phụ huynh ở quy mô nhỏ (Mini-CRM), tập trung vào công việc hằng ngày của giáo viên: quản lý lớp, ghi nhận tình hình học tập, tổng hợp học phí và lập báo cáo tháng.

### 1.2. Mục tiêu

- Giảm thời gian điểm danh, ghi nhận bài tập và tổng hợp học phí.
- Giúp giáo viên theo dõi sự tiến bộ của từng học sinh qua các buổi học.
- Cung cấp báo cáo dễ hiểu để phụ huynh nắm được tình hình học tập và khoản tiền cần thanh toán.
- Minh bạch số buổi tính phí, các khoản đã trả và số tiền bảo lưu.
- Hạn chế bỏ sót hoặc tính trùng khi học sinh nghỉ học dài ngày có phép.

### 1.3. Kết quả mong đợi

Giáo viên có thể thực hiện một quy trình xuyên suốt: **tạo lớp → thêm học sinh → theo dõi từng buổi → tổng hợp cuối tháng → gửi báo cáo và mã thanh toán → xác nhận đã thu tiền**.

## 2. Đối tượng sử dụng và các bên liên quan

| Đối tượng | Vai trò và nhu cầu |
| --- | --- |
| Giáo viên/gia sư | Người sử dụng chính; quản lý lớp, theo dõi học tập, tải ảnh để AI chấm điểm, duyệt nhận xét AI và kiểm tra học phí. |
| Admin | Quản lý đăng ký, đối chiếu phí dịch vụ, duyệt/từ chối, gia hạn, khóa và quyền AI; không mặc định đọc lớp, bài làm hoặc học phí học sinh. |
| Học sinh | Đối tượng được theo dõi chuyên cần, bài tập và kết quả học tập. |
| Phụ huynh/người thanh toán | Nhận báo cáo, kiểm tra học phí và quét mã để thanh toán. |

**Đã thống nhất:** Mỗi giáo viên có tài khoản và quản lý dữ liệu riêng; chưa có trợ giảng hoặc tài khoản dùng chung. Phụ huynh nhận PDF riêng của con, không cần tài khoản.

## 3. Phạm vi nghiệp vụ

### 3.1. Phạm vi bao gồm

| Mã | Nhóm nghiệp vụ | Kết quả cần đạt |
| --- | --- | --- |
| BR-01 | Quản lý lớp và danh sách | Có danh sách lớp, học sinh, lịch học cố định và đơn giá theo buổi. |
| BR-02 | Theo dõi hằng ngày và trợ lý AI | Điểm danh; tự chấm hoặc AI chấm theo hai chỉ số bốn mức; nhận xét và cảnh báo xu hướng. |
| BR-03 | Tổng hợp học phí | Tính phí theo các buổi thực tế học sinh có mặt. |
| BR-04 | Thanh toán | Tạo VietQR theo khoản cần thu và ghi nhận thanh toán. |
| BR-05 | Báo cáo tháng | Tổng hợp chuyên cần, bài tập, điểm, nhận xét và lộ trình tháng tới. |
| BR-06 | Bảo lưu học phí | Ghi nhận khoản đủ điều kiện bảo lưu và chuyển sang tháng sau. |
| BR-07 | Đăng ký và quyền dịch vụ trả phí | Giáo viên xác minh email, chờ Admin duyệt; quyền có hạn, AI tùy tài khoản và hạn mức theo kỳ. |

### 3.2. Giới hạn phạm vi ban đầu

- **Tự chấm:** Giáo viên đánh giá độc lập mức hoàn thành bài tập và kết quả làm đúng; mỗi chỉ số có bốn mức 25%, 50%, 75%, 100%. Không cần AI.
- **AI chấm:** AI đề xuất cùng hai chỉ số từ ảnh bài làm; giáo viên được sửa và xác nhận kết quả. Kết quả AI gốc được giữ để đối chiếu.
- Giáo viên chọn cách chấm trước khi bắt đầu: mặc định cho cả lượt, có thể đổi riêng từng học sinh trước khi xử lý bài đó. Mỗi bài chỉ có một cách chấm đang áp dụng.
- VietQR phục vụ việc chuyển khoản; việc quét mã không đồng nghĩa với đã thanh toán.
- Giáo viên xác nhận tiền đã nhận sau khi kiểm tra giao dịch. Tự động đối chiếu giao dịch ngân hàng chưa nằm trong phạm vi ban đầu.
- Chưa bao gồm lớp học trực tuyến, trả lương giáo viên, quản lý trung tâm nhiều chi nhánh hoặc hoàn tiền tự động.
- Phụ huynh nhận báo cáo và bảng học phí dạng PDF; không cần xuất Word hoặc DOCX cho phụ huynh.

## 4. Yêu cầu nghiệp vụ chi tiết

### 4.1. BR-01 — Quản lý lớp và danh sách học sinh

**Mục đích:** Giáo viên thiết lập thông tin lớp một lần để sử dụng trong các buổi học và kỳ thu phí.

Giáo viên cần có khả năng:

1. Tạo lớp với tên lớp, môn học và thông tin mô tả cần thiết.
2. Thêm học sinh vào lớp; ghi nhận họ tên, người liên hệ và thông tin liên lạc của phụ huynh.
3. Thiết lập lịch cố định theo thứ trong tuần và giờ bắt đầu, kết thúc; một lớp có thể học nhiều buổi trong tuần.
4. Thiết lập học phí cơ bản cho một học sinh trong một buổi của lớp.
5. Xem và cập nhật danh sách học sinh đang theo học, đã dừng hoặc đang nghỉ có phép.
6. Xem các buổi dự kiến theo lịch và ghi nhận buổi thực tế diễn ra, kể cả buổi học bù hoặc buổi bị hủy.

**Quy tắc nghiệp vụ:**

- Lịch cố định giúp lập kế hoạch; chỉ có lịch dự kiến chưa đủ để tính học phí.
- Một học sinh chỉ xuất hiện một lần trong danh sách của cùng một lớp tại cùng thời điểm.
- Buổi bị hủy không được tính phí.
- Thay đổi đơn giá phải có thời điểm bắt đầu áp dụng. Các buổi đã ghi nhận giữ nguyên đơn giá trước đó, trừ khi giáo viên chủ động điều chỉnh và ghi lý do.
- **Đề xuất cần duyệt:** Học sinh được tham gia nhiều lớp; số buổi và học phí được theo dõi riêng theo từng lớp.

### 4.2. BR-02 — Theo dõi tiến độ hằng ngày

#### a. Điểm danh trực quan

Khi mở một buổi học, giáo viên nhìn thấy toàn bộ học sinh của lớp và chọn trạng thái cho từng em.

| Trạng thái | Cách thể hiện | Ý nghĩa tính phí |
| --- | --- | --- |
| Chưa điểm danh | Có nhãn rõ ràng, dễ nhận biết | Chưa đủ căn cứ tính phí |
| Có mặt | Dòng học sinh có màu nổi bật và nhãn “Có mặt” | Được tính phí theo đơn giá của buổi |
| Vắng mặt | Toàn bộ dòng được bôi xám, vẫn đọc được thông tin và có nhãn “Vắng” | Không tính phí theo nguyên tắc thu theo buổi thực học |

Giáo viên có thể ghi chú lý do vắng và sửa điểm danh khi phát hiện sai. Các bản tổng hợp bị ảnh hưởng phải được cập nhật trước khi chốt báo cáo.

**Đã thống nhất:** Buổi mới mặc định là “Chưa điểm danh”; hệ thống nhắc giáo viên xử lý các trường hợp còn thiếu trước khi chốt tháng. Màu sắc luôn đi kèm nhãn để người dùng không phải nhận biết trạng thái chỉ bằng màu.

#### b. Tự chấm và hai chỉ số đánh giá chung

Giáo viên chọn cách chấm trước khi bắt đầu. Khi chọn **tự chấm**, giáo viên trực tiếp chọn mức cho hai chỉ số độc lập; không gọi AI:

| Chỉ số | Nội dung | Các mức chọn nhanh |
| --- | --- | --- |
| Mức hoàn thành | Lượng bài học sinh đã thực hiện so với lượng được giao | 25%, 50%, 75%, 100% |
| Kết quả làm đúng | Mức độ làm đúng theo căn cứ chấm của bài | 25%, 50%, 75%, 100% |

Ví dụ, học sinh có thể hoàn thành 100% nhưng kết quả làm đúng chỉ đạt mức 50%. Hai chỉ số không thay thế nhau. Cả tự chấm và AI chấm đều dùng chung các mức trên; không dùng điểm 8/10 làm chỉ số chính song song.

**Các trạng thái riêng đã thống nhất:**

| Trạng thái | Xử lý thống kê |
| --- | --- |
| Không làm bài | Cả mức hoàn thành và kết quả làm đúng được tính bằng 0 |
| Không có câu đúng | Mức hoàn thành theo lượng bài đã làm; kết quả làm đúng được tính bằng 0 |
| Không giao bài / Chưa đánh giá | Không tính vào trung bình và cảnh báo học lực |

Các trạng thái này không tạo thêm nút phần trăm. Ô trống không tự được hiểu là 0 hoặc 25%; không cho nhập tỷ lệ tùy ý. Giáo viên được sửa mức đã chọn.

**Quy tắc AI quy đổi tỷ lệ thực tế sang mức ghi nhận, áp dụng cho cả hai chỉ số:**

| Tỷ lệ thực tế | Mức ghi nhận |
| --- | --- |
| 0% | Trạng thái bằng 0 phù hợp đã nêu trên |
| Trên 0% đến 25% | 25% |
| Trên 25% đến 50% | 50% |
| Trên 50% đến 75% | 75% |
| Trên 75% đến dưới 100% | 75% |
| Đúng 100% | 100% |

100% chỉ dùng khi hoàn thành đầy đủ hoặc làm đúng toàn bộ. Các mức là nhóm đánh giá; ví dụ tỷ lệ thực tế 60% được ghi nhận mức 75%, không được trình bày thành khẳng định học sinh thực tế làm đúng 75%. Giáo viên vẫn được sửa mức AI đề xuất.

#### c. Chấm điểm tự động bằng AI

**Mục đích:** Khi đã chọn AI chấm, giáo viên chụp hoặc tải ảnh bài làm/bài kiểm tra để AI nhận diện nội dung, đề xuất hai mức đánh giá và ghi vào hồ sơ.

1. Giáo viên chọn học sinh, buổi học và tên bài đánh giá, sau đó tải đủ ảnh các trang bài làm.
2. Giáo viên cung cấp đề, đáp án và hướng dẫn chấm một lần cho mỗi bài, dùng chung cho các học sinh. Thiếu căn cứ thì bổ sung hoặc chuyển sang tự chấm.
3. AI đọc ảnh, phân tích bài và đề xuất mức hoàn thành, mức làm đúng theo quy tắc quy đổi, cùng các lỗi có căn cứ. Điểm gốc đọc được trên ảnh, nếu có, chỉ là thông tin đối chiếu, không thay hai chỉ số chính.
4. Kết quả thành công được lưu đúng hồ sơ, kèm ảnh nguồn và kết quả AI gốc. Sau khi AI hoàn tất, giáo viên xem, sửa mức nếu cần và xác nhận kết quả cuối cùng để dùng chính thức.

**Quy tắc và ngoại lệ:**

- Giáo viên được sửa kết quả AI sau khi xử lý hoàn tất; lưu kết quả AI gốc và lịch sử thay đổi để đối chiếu.
- Nếu AI lỗi hoặc không trả kết quả, được chuyển bài sang tự chấm. Lượt AI đang chờ bị hủy; kết quả về muộn không được áp dụng vào bài đã chuyển.
- Sửa đề, đáp án hoặc hướng dẫn không tự chấm lại. Hệ thống đánh dấu các bài dùng hướng dẫn cũ; giáo viên chọn bài cần chấm lại. Nếu bài có kết quả đã được giáo viên sửa, phải cảnh báo và xác nhận trước khi thay thế.
- Ảnh mờ, thiếu trang, không đọc được hoặc thiếu căn cứ chấm phải báo rõ nguyên nhân; không tự suy đoán điểm hoặc ghi điểm 0.
- Kết quả chưa đủ tin cậy mang trạng thái “Cần kiểm tra”, chưa dùng làm điểm chính thức để tổng hợp nhận xét hay cảnh báo.
- Hai chỉ số tuân theo bốn mức và các trạng thái riêng đã thống nhất; dữ liệu thiếu không đồng nghĩa với kết quả bằng 0.
- Chấm lại không tạo đầu điểm trùng; giữ dấu vết kết quả cũ và mới để đối chiếu. Ảnh gắn sai học sinh phải được sửa liên kết trước khi sử dụng.
- **Đề xuất cần duyệt:** Thống nhất môn học, dạng bài, thang điểm, yêu cầu ảnh và căn cứ chấm hỗ trợ trong giai đoạn đầu. Nghiệm thu chất lượng trên bộ bài mẫu hai bên thống nhất, gồm cả chữ viết tay và ảnh khó đọc; không mặc định AI chính xác tuyệt đối.

#### d. AI tự động phác thảo nhận xét tháng

AI tổng hợp điểm danh, hai mức đánh giá cuối cùng do giáo viên xác nhận và tần suất làm bài để soạn nhận xét cá nhân hóa. Áp dụng cho cả tự chấm và AI chấm; kết quả AI gốc chỉ phục vụ đối chiếu. Nhận xét gồm chuyên cần, điểm mạnh, sự tiến bộ, lỗi có bằng chứng và nội dung cần củng cố.

Giáo viên được sửa lời nhận xét và lộ trình tháng tới rồi duyệt trước khi xuất. Sửa nhận xét không làm thay đổi điểm AI. Hệ thống không tự gửi bản nháp cho phụ huynh, không tạo thành tích hoặc lỗi sai không có trong dữ liệu.

- Khi sửa đánh giá sau khi đã tạo nhận xét, hệ thống đánh dấu nhận xét cần cập nhật; chỉ tạo lại khi giáo viên yêu cầu để giữ phần lời đã chỉnh sửa.
- Nhận xét và cảnh báo dùng dữ liệu đã lưu, không yêu cầu chấm lại ảnh. Chỉ chấm lại khi giáo viên chủ động yêu cầu.
- Nếu AI viết nhận xét lỗi, giáo viên vẫn được tự viết và xuất PDF.
- Tần suất làm bài chỉ tổng hợp từ các buổi có giao bài và có thông tin đánh giá; ô trống không tự quy thành không làm bài.

#### e. AI phân tích xu hướng và cảnh báo sớm

Hệ thống sử dụng phân tích học tập và học máy để phát hiện điểm giảm qua nhiều bài, lượng bài hoàn thành giảm, vắng học tăng hoặc lỗi kiến thức nền tảng lặp lại. Giáo viên xem biểu đồ và danh sách học sinh cần quan tâm để chủ động hỗ trợ.

Mỗi cảnh báo thể hiện học sinh, dấu hiệu, giai đoạn so sánh, dữ liệu làm căn cứ và gợi ý hành động. Ví dụ: “Điểm giảm qua ba bài gần nhất, đồng thời mức hoàn thành bài tập giảm; đề nghị kiểm tra lại phần phân số.”

- Cảnh báo là gợi ý xem xét, không phải kết luận chắc chắn học sinh “mất gốc”.
- Khi chưa đủ lịch sử, hiển thị “Chưa đủ dữ liệu để đánh giá xu hướng”. Không tự coi thiếu dữ liệu hoặc nghỉ ốm có phép là sa sút.
- Cảnh báo dùng hai chỉ số cuối cùng giáo viên xác nhận, xét nội dung và mức độ tương đồng của bài. Các mức phần trăm là nhóm đánh giá, không phải điểm số chính xác.
- Giáo viên có thể đánh dấu đã xem, ghi biện pháp hỗ trợ hoặc lý do cảnh báo chưa phù hợp.
- **Đề xuất cần duyệt:** Thống nhất số bài tối thiểu, khoảng thời gian, ngưỡng cảnh báo và cách nghiệm thu bằng dữ liệu mẫu trước triển khai.

### 4.3. BR-03 — Tổng hợp học phí cuối tháng

Giáo viên chọn tháng và học sinh để xem các buổi đã học, đơn giá từng buổi, học phí phát sinh, khoản bảo lưu được sử dụng và tiền đã nhận cho kỳ đó.

**Nguyên tắc tính:**

> Học phí phát sinh trong tháng = Tổng đơn giá của các buổi học sinh được ghi nhận có mặt trong tháng.

Nếu toàn bộ buổi có cùng đơn giá:

> Học phí phát sinh = Số buổi có mặt × Đơn giá mỗi buổi.

> Dư nợ kỳ học = Học phí phát sinh + Điều chỉnh tăng − Điều chỉnh giảm − Bảo lưu thực dùng − Tiền đã phân bổ cho kỳ; tối thiểu bằng 0.

> Tổng cần thanh toán = Dư nợ cũ còn lại + Dư nợ kỳ hiện tại.

Nợ cũ là số còn lại sau phân bổ, không phải số nợ trước khi nhận tiền. Khoản bảo lưu và mỗi giao dịch nhận tiền chỉ được khấu trừ một lần. Ví dụ bên dưới không có nợ cũ hoặc khoản điều chỉnh.

**Quy tắc nghiệp vụ:**

- Không tính phí cho buổi vắng, bị hủy hoặc chỉ mới có trong lịch dự kiến.
- Mỗi lần tham dự một buổi chỉ được tính phí một lần.
- Nếu đơn giá thay đổi trong tháng, tính theo đơn giá áp dụng cho từng buổi, không áp giá mới cho toàn bộ tháng.
- Khoản bảo lưu được sử dụng không vượt quá học phí còn phải trả sau khi trừ tiền đã nhận cho kỳ này. Số dư bảo lưu chưa sử dụng tiếp tục được thể hiện riêng.
- Tiền trả trước đã đưa vào số dư bảo lưu không được đồng thời trừ như tiền đã nhận cho kỳ mới.
- **Đề xuất cần duyệt:** Nếu có khoản trả dư, giáo viên xác nhận cách xử lý và ghi nhận riêng; không thể hiện khoản cần thanh toán âm và không tự hoàn tiền.
- Nợ cũ hiển thị riêng, tự cộng vào tổng cần thanh toán và số tiền VietQR. Mỗi lần nhận tiền là một giao dịch riêng, mặc định phân bổ trả nợ cũ trước rồi đến kỳ mới.
- Được thêm khoản điều chỉnh tăng/giảm học phí có lý do. Hệ thống cập nhật dư nợ, trạng thái thanh toán, QR và báo cáo liên quan.

**Thống kê theo ngày nhận tiền và kỳ học phí:** Lưu cả ngày thực nhận và kỳ học phí được phân bổ. Ví dụ nhận tiền ngày 05/11 để trả nợ tháng 10: thống kê tiền thực thu ghi ở tháng 11, đồng thời giảm dư nợ học phí tháng 10. Không cộng lại thành hai khoản thu; bảo lưu được sử dụng không được tính là tiền mới nhận.

**Ví dụ minh họa:**

| Nội dung | Giá trị |
| --- | ---: |
| Số buổi có mặt | 8 buổi |
| Đơn giá áp dụng | 150.000 đồng/buổi |
| Học phí phát sinh | 1.200.000 đồng |
| Bảo lưu được sử dụng | 300.000 đồng |
| Tiền đã nhận cho kỳ này | 200.000 đồng |
| **Còn phải thanh toán** | **700.000 đồng** |

### 4.4. BR-04 — Thanh toán bằng VietQR

**Thứ tự xử lý đã thống nhất:** Ưu tiên tạo VietQR chứa số tiền và nội dung. Chỉ khi không tạo được mới dùng QR ngân hàng tĩnh, kèm số tiền và nội dung để phụ huynh tự nhập. Nếu QR tĩnh cũng chưa có, vẫn cho xuất PDF với ngân hàng, số tài khoản, người nhận, số tiền và nội dung; cảnh báo thiếu QR trước khi xuất. Không dùng mã cũ có số tiền sai.

Sau khi giáo viên kiểm tra khoản cần thu, hệ thống tự tạo mã VietQR để phụ huynh quét và thực hiện chuyển khoản.

Thông tin đi kèm mã gồm:

- Ngân hàng, số tài khoản và tên người thụ hưởng do giáo viên thiết lập.
- Số tiền còn phải thanh toán.
- Nội dung chuyển khoản giúp nhận diện học sinh và tháng thu phí.

**Quy tắc nghiệp vụ:**

- Giáo viên phải kiểm tra tài khoản nhận tiền trước khi chia sẻ báo cáo.
- Chưa có thông tin nhận tiền hợp lệ thì chưa tạo mã; giáo viên được thông báo nội dung cần bổ sung.
- Số tiền bằng 0 thì thể hiện “Không còn khoản phải thanh toán”, không tạo yêu cầu chuyển tiền.
- Khi khoản cần thu thay đổi, phải tạo lại mã và bản báo cáo tương ứng; nhắc giáo viên gửi bản cập nhật cho phụ huynh.
- Trạng thái học phí tối thiểu gồm: chưa thanh toán, thanh toán một phần và đã thanh toán đủ.
- Giáo viên nhập số tiền thực nhận, ngày nhận và ghi chú sau khi đối chiếu giao dịch. Thanh toán chưa đủ được ghi nhận là một phần, hiển thị khoản còn nợ cho phụ huynh và phục vụ thống kê. Chỉ chuyển sang “Đã thanh toán đủ” khi đã đủ tiền.
- Được điều chỉnh hoặc hủy giao dịch ghi nhầm/ghi trùng, bắt buộc có lý do và giữ lịch sử gốc. Tính lại dư nợ, trạng thái và QR; nếu đã gửi PDF thì nhắc xuất và gửi bản cập nhật.

### 4.5. BR-05 — Báo cáo học tập và học phí cuối tháng

Giáo viên xem trước, bổ sung nhận xét và xuất báo cáo riêng cho từng học sinh để gửi phụ huynh.

| Phần báo cáo | Nội dung |
| --- | --- |
| Thông tin chung | Họ tên học sinh, lớp, giáo viên và tháng báo cáo |
| Chuyên cần | Số buổi có mặt, số buổi vắng và các ghi chú liên quan |
| Bài tập và kết quả | Hai chỉ số cuối cùng đã xác nhận theo buổi; phân biệt trạng thái bằng 0, không giao bài và chưa đánh giá |
| Thông tin đánh giá | Tên bài, ngày, môn/phần học và cách chấm; các mức phần trăm thể hiện nhóm đánh giá |
| Thái độ học tập | Nhận xét do giáo viên viết về sự tập trung, chủ động, hợp tác và nỗ lực |
| Phân tích AI từng môn/phần học | Điểm mạnh, lỗi thường gặp, xu hướng học tập, nội dung chưa vững và gợi ý luyện thêm đã được giáo viên duyệt |
| Lộ trình tháng tới | Mục tiêu, nội dung ôn tập và gợi ý phối hợp với phụ huynh |
| Học phí | Số buổi, đơn giá, phí phát sinh, điều chỉnh, bảo lưu, tiền đã phân bổ, nợ cũ và tổng còn phải trả |
| Thanh toán | Thông tin nhận tiền và mã VietQR khi còn khoản phải trả |

Khu vực nhận xét hỗ trợ xuống dòng và emoji tối giản, ít màu, dùng tiết chế và không trang trí dày đặc. AI phác thảo nhận xét dựa trên dữ liệu; giáo viên chỉnh sửa và duyệt nội dung trước khi xuất.

**Đã thống nhất:** Phụ huynh chỉ cần nhận PDF, không cần Word/DOCX. Emoji phải dễ đọc trong bản xuất.

**Chỉnh sửa trước khi xuất:** Giáo viên sửa tên, thông tin liên hệ, nhận xét và lộ trình ngay trên bản xem trước. Số buổi, đơn giá, tiền đã nhận và bảo lưu phải sửa ở dữ liệu nguồn; tăng/giảm phí riêng qua khoản điều chỉnh có lý do. Học phí, dư nợ, QR và PDF phải cùng cập nhật, không sửa riêng số tổng trên PDF. “Hóa đơn” trong phạm vi tài liệu này là bảng học phí gửi phụ huynh; không bổ sung chức năng hóa đơn thuế.

### 4.6. BR-06 — Nghỉ ốm dài ngày và bảo lưu học phí

**Mục đích:** Giữ lại khoản học phí đã thu nhưng chưa sử dụng của học sinh được giáo viên đồng ý cho nghỉ ốm dài ngày, để sử dụng vào tháng sau.

**Điều kiện và cách hiểu:**

- Giáo viên xác nhận thời gian nghỉ, lý do và việc chấp thuận bảo lưu.
- Chỉ bảo lưu khoản đã thu và chưa sử dụng, có căn cứ đối chiếu rõ ràng.
- Nếu thu tiền sau theo số buổi thực học, buổi vắng không phát sinh phí; không tạo thêm số dư bảo lưu cho khoản chưa thu.
- Bảo lưu là khoản tiền được chuyển tiếp, không phải một lần giảm phí bổ sung cho cùng buổi vắng.
- **Đề xuất cần duyệt:** Theo dõi bảo lưu bằng tiền và sử dụng cho cùng học sinh, cùng lớp. Đơn giá tháng mới áp dụng cho buổi học tháng mới; số tiền bảo lưu không tự tăng theo đơn giá.

**Luồng xử lý:**

1. Giáo viên ghi nhận đề nghị nghỉ và khoảng thời gian nghỉ có phép.
2. Đối chiếu tiền đã thu, các buổi đã sử dụng và khoản chưa sử dụng đủ điều kiện chuyển tiếp.
3. Giáo viên duyệt số tiền bảo lưu, ghi lý do và tháng dự kiến sử dụng.
4. Báo cáo tháng hiện tại thể hiện rõ số tiền được chuyển sang tháng sau.
5. Khi lập học phí tháng sau, hệ thống đưa khoản bảo lưu đã duyệt vào bảng đối chiếu và trừ trong giới hạn khoản phải trả.
6. Nếu chưa dùng hết, hiển thị số dư còn lại để giáo viên và phụ huynh theo dõi.

**Ví dụ:** Phụ huynh đã trả trước 1.200.000 đồng cho 8 buổi, mỗi buổi 150.000 đồng. Học sinh học 5 buổi rồi nghỉ ốm có phép. Nếu giáo viên duyệt bảo lưu toàn bộ phần chưa dùng, học phí đã sử dụng là 750.000 đồng và 450.000 đồng được chuyển tiếp. Tháng sau học sinh học 6 buổi cùng đơn giá, học phí phát sinh là 900.000 đồng; sau khi sử dụng bảo lưu, còn phải trả 450.000 đồng nếu chưa thanh toán thêm.

**Các trường hợp cần thống nhất:**

| Tình huống | Phương án đề xuất cần duyệt |
| --- | --- |
| Nghỉ bao lâu được coi là dài ngày? | Giáo viên duyệt từng đề nghị; khách hàng xác nhận có cần số ngày/buổi tối thiểu hay không. |
| Chưa quay lại trong tháng sau | Giữ số dư đến khi sử dụng; thời hạn tối đa cần được khách hàng xác nhận. |
| Dừng học hẳn hoặc chuyển lớp | Giáo viên xử lý theo chính sách được thống nhất; chưa tự hoàn tiền hoặc tự chuyển số dư giữa lớp. |
| Sửa/hủy khoản bảo lưu đã duyệt | Ghi rõ lý do, đối chiếu phần đã dùng và cập nhật lại báo cáo liên quan để tránh trừ tiền hai lần. |

### 4.7. BR-07 — Dịch vụ trả phí và quyền sử dụng

**Đã thống nhất:** Giáo viên trả phí để dùng Education Keeptrack. Khoản này tách biệt hoàn toàn học phí phụ huynh trả giáo viên, không cộng vào thu tiền lớp hoặc số dư bảo lưu học sinh.

#### a. Đăng ký, thanh toán dịch vụ và Admin phê duyệt

- Giáo viên tự đăng ký bằng email/mật khẩu, xác minh email rồi chờ Admin duyệt. Trước duyệt chỉ đăng nhập, xem trạng thái/hướng dẫn thanh toán và gửi yêu cầu đối chiếu, chưa dùng nghiệp vụ lớp.
- Hướng dẫn chuyển khoản và thông tin liên hệ do Admin cấu hình. Giáo viên gửi mã giao dịch/ghi chú; gửi yêu cầu không tự cấp quyền.
- Admin kiểm tra tiền bên ngoài, ghi thông tin đối chiếu, duyệt hoặc từ chối có lý do. Từ chối không xóa tài khoản. Chưa có cổng thanh toán hoặc tự đối chiếu ngân hàng; không tự tạo gói dùng thử.
- Admin đặt ngày bắt đầu/kết thúc, cấu hình kỳ và hạn mức trước khi cấp. Admin không mặc định được xem dữ liệu lớp, ảnh bài hay học phí học sinh. Đăng ký công khai không được cấp vai trò Admin; Admin đầu tiên tạo qua bước thiết lập riêng và bắt buộc xác thực hai bước từ ứng dụng.

#### b. Còn hạn, hết hạn, gia hạn và khóa

| Trạng thái | Khả năng sử dụng |
| --- | --- |
| Chờ duyệt | Chỉ xem trạng thái, hướng dẫn và gửi yêu cầu đối chiếu |
| Còn hạn, không bị khóa | Dùng nghiệp vụ thủ công; AI còn cần quyền riêng và đủ hạn mức |
| Hết hạn | Đăng nhập, xem và xuất dữ liệu đã có; không thêm/sửa hoặc gọi AI mới |
| Bị khóa | Không truy cập dữ liệu cũ, vô hiệu phiên; giữ dữ liệu và lý do khóa |

- Còn hạn thì gia hạn nối tiếp ngày hết hạn; đã hết hạn thì kỳ mới bắt đầu khi Admin duyệt. Không tự suy thời hạn từ tiền chuyển khoản nếu chưa có bảng gói.
- Mỗi kỳ lưu cấu hình đã cấp; thay đổi gói chỉ áp dụng kỳ mới, điều chỉnh kỳ hiện tại phải có lý do/lịch sử. Lượt chưa dùng không chuyển tiếp; gia hạn sớm không cấp sớm lượt kỳ mới.
- Khi hết hạn giữa thao tác, server chặn ghi mới; giữ nội dung form trên màn hình và thông báo rõ. AI đã được chấp nhận trước hạn vẫn hoàn tất/lưu kết quả, chỉ được sửa/xác nhận sau gia hạn.
- Khóa tài khoản khác hết hạn: hủy công việc AI hoặc không áp dụng kết quả về muộn. Mở khóa không tự gia hạn quyền đã hết.

#### c. Quyền AI do Admin quản lý

- Admin bật/tắt quyền AI cho từng tài khoản, độc lập hạn mức. Không có AI vẫn được tự chấm, viết nhận xét, xem biểu đồ từ kết quả đã lưu và xuất PDF trong kỳ còn hạn.
- Tắt AI bao gồm chấm AI, sinh nhận xét AI và cảnh báo AI; giữ kết quả AI cũ. Lượt đã được chấp nhận vẫn hoàn tất, không nhận yêu cầu mới. Tắt rồi bật lại không reset lượt đã dùng.
- Hạn mức theo kỳ, riêng chấm một bài/một học sinh và sinh nhận xét tháng/một học sinh. Giữ lượt khi nhận, trừ khi kết quả hợp lệ, hoàn khi lỗi/hủy; gửi lặp không tính hai lần. Chấm lại/tạo lại thành công tính lượt mới.
- Giáo viên thấy lượt còn dùng được, đang giữ và đã dùng. Hết hạn mức AI vẫn làm thủ công trong kỳ còn hạn. Chưa có hạn mức thì Admin phải cấp rõ trước khi dùng AI; không tự đặt giá hay số lượt.
- Đơn vị tính lượt cho cảnh báo AI chưa được quyết định; cần xác nhận trước vận hành thật, không tự tính chung với chấm hoặc nhận xét.

#### d. Đăng nhập và khôi phục

- Đăng nhập email/mật khẩu; hỗ trợ đặt lại mật khẩu qua liên kết email một lần có hạn, thông báo không tiết lộ email có tồn tại hay không.
- Đổi email phải xác minh email mới, giữ quyền đã mua. Đổi email/đặt lại mật khẩu thành công vô hiệu các phiên cũ và yêu cầu đăng nhập lại.
- Giáo viên hết phiên sau 2 giờ không hoạt động hoặc 24 giờ từ lúc đăng nhập; Admin tương ứng 30 phút hoặc 8 giờ. Chưa có ghi nhớ đăng nhập.
- Duyệt/gia hạn/khóa/đổi hạn mức lưu người thao tác, thời điểm và lý do để đối chiếu.

## 5. Giao diện và hành trình sử dụng dự kiến

**Đăng ký dịch vụ:** Đăng ký → Xác minh email → Xem hướng dẫn/chuyển khoản → Gửi đối chiếu → Admin phê duyệt và cấp kỳ → Sử dụng. Màn hình chờ, hết hạn, gia hạn, quyền AI và quản trị bổ sung theo BR-07.

### 5.1. Thiết lập ban đầu

**Danh sách lớp → Tạo lớp → Thiết lập lịch và học phí → Thêm học sinh, phụ huynh → Kiểm tra thông tin nhận tiền.**

Kết quả: Giáo viên có lớp sẵn sàng cho điểm danh và thu phí.

### 5.2. Thao tác trong buổi học

**Chọn lớp → Chọn buổi → Điểm danh → Chọn cách chấm mặc định, đổi riêng từng học sinh nếu cần → Tự chấm hoặc tải ảnh để AI chấm → Xem/sửa hai mức đánh giá → Xác nhận kết quả.**

- Mỗi học sinh nằm trên một dòng, dễ nhận diện và thao tác.
- Mỗi chỉ số có bốn lựa chọn nhìn thấy ngay; trạng thái riêng phân biệt với ô chưa đánh giá. AI lỗi thì được chuyển sang tự chấm.
- Dòng học sinh vắng được bôi xám toàn bộ nhưng vẫn cho phép xem thông tin và sửa trạng thái.
- Có thông báo rõ khi lưu thành công hoặc khi còn dữ liệu chưa được lưu.

### 5.3. Tổng kết cuối tháng

**Chọn tháng → Kiểm tra điểm danh còn thiếu → Đối chiếu học phí và bảo lưu → Duyệt nhận xét AI và lộ trình → Xem trước báo cáo → Chốt và xuất → Chia sẻ với phụ huynh.**

**Đề xuất cần duyệt:** Báo cáo đã chốt chỉ được sửa sau thao tác mở lại có ghi lý do. Bản xuất mới thể hiện thời điểm cập nhật để phụ huynh nhận biết bản mới nhất.

### 5.4. Xác nhận thanh toán

**Phụ huynh nhận báo cáo → Quét VietQR → Chuyển tiền → Giáo viên kiểm tra giao dịch → Ghi nhận số tiền đã nhận → Cập nhật trạng thái.**

### 5.5. Ghi nhận bảo lưu

**Nhận đề nghị nghỉ → Xác nhận có phép → Đối chiếu tiền chưa sử dụng → Duyệt bảo lưu → Hiển thị trên báo cáo → Khấu trừ vào kỳ tiếp theo.**

## 6. Các yêu cầu chất lượng và bảo mật

| Nhóm | Yêu cầu |
| --- | --- |
| Bảo mật thông tin | Mỗi giáo viên chỉ truy cập dữ liệu của mình, gồm lớp, học sinh, ảnh bài, giao dịch và báo cáo; kiểm soát cả khi mở trực tiếp đường dẫn ảnh hoặc báo cáo. |
| Riêng tư khi chia sẻ | Báo cáo của một học sinh không kèm thông tin cá nhân hoặc học phí của học sinh khác. |
| Dễ sử dụng | Nội dung bằng tiếng Việt; thao tác chính dễ nhìn, ít bước và phù hợp khi giáo viên đang đứng lớp. |
| Khả năng đọc | Màu sắc đi kèm chữ; dòng bôi xám vẫn đọc được; số tiền và trạng thái thanh toán rõ ràng. |
| Tốc độ phản hồi | Điểm danh và chọn bài tập phản hồi nhanh, không làm gián đoạn buổi học. Mức thời gian cụ thể được thống nhất theo quy mô lớp thực tế. |
| Độ chính xác | Điểm danh, đơn giá, tiền đã thu và bảo lưu phải khớp giữa màn hình, bảng tổng hợp và báo cáo xuất ra. |
| An toàn dữ liệu | Có biện pháp lưu giữ và khôi phục dữ liệu; cảnh báo trước khi xóa thông tin ảnh hưởng đến lịch sử học tập hoặc học phí. |
| Khả năng đối chiếu | Những thay đổi ảnh hưởng đến học phí, thanh toán hoặc bảo lưu có thời điểm và lý do để kiểm tra lại. |
| Thiết bị sử dụng | Đề xuất hỗ trợ thao tác thuận tiện trên máy tính và điện thoại; danh sách thiết bị cần được khách hàng xác nhận. |

**Yêu cầu bổ sung đối với AI:** Bảo vệ ảnh bài làm như thông tin học sinh; thông báo mục đích xử lý. Đề xuất cần duyệt: thời hạn lưu ảnh, cách yêu cầu xóa và điều kiện sử dụng dữ liệu để huấn luyện AI. Khi AI chậm hoặc lỗi, phải có trạng thái rõ và cho phép xử lý lại mà không tạo điểm trùng; giáo viên vẫn tiếp tục điểm danh. Thống nhất quy mô sử dụng, thời gian xử lý ảnh và chất lượng chấm trên bộ mẫu trước nghiệm thu.

## 7. Tiêu chí nghiệm thu nghiệp vụ

| Mã | Tình huống kiểm tra | Kết quả đạt yêu cầu |
| --- | --- | --- |
| AC-01 | Tạo lớp với lịch nhiều ngày trong tuần, đơn giá và danh sách học sinh | Thông tin được lưu, xem lại và sử dụng cho buổi học. |
| AC-02 | Điểm danh một học sinh có mặt, một học sinh vắng | Dòng có mặt nổi bật; toàn bộ dòng vắng bôi xám; mỗi dòng có nhãn trạng thái rõ ràng. |
| AC-03 | Tự chấm | Hai chỉ số độc lập có đúng bốn mức 25%, 50%, 75%, 100%; không gọi AI; giáo viên sửa được mức. |
| AC-04 | Chọn AI chấm và tải đủ ảnh, căn cứ | AI đề xuất hai mức, chỉ ra lỗi có bằng chứng và lưu đúng hồ sơ; giáo viên sửa/xác nhận được; giữ kết quả AI gốc. |
| AC-05 | Một học sinh có mặt 8 buổi, đơn giá 150.000 đồng | Học phí phát sinh là 1.200.000 đồng; buổi vắng và buổi hủy không làm tăng phí. |
| AC-06 | Đơn giá thay đổi giữa tháng | Tổng học phí dùng đúng đơn giá từng buổi, giữ nguyên phí các buổi trước ngày áp dụng. |
| AC-07 | Còn phải thu 700.000 đồng | VietQR và thông tin kèm theo thể hiện đúng tài khoản, số tiền và nội dung chuyển khoản. |
| AC-08 | Phụ huynh quét mã nhưng giáo viên chưa xác nhận tiền | Trạng thái không tự chuyển thành đã thanh toán đủ. |
| AC-09 | AI soạn nhận xét; giáo viên duyệt và thêm emoji | Báo cáo phản ánh đúng dữ liệu học tập, phân tích AI và lộ trình tháng tới; giữ nội dung và emoji dễ đọc. |
| AC-10 | Đã thu 1.200.000 đồng, dùng 750.000 đồng, được duyệt bảo lưu phần còn lại | Chuyển tiếp đúng 450.000 đồng; tháng sau chỉ khấu trừ một lần. |
| AC-11 | Học sinh vắng có phép nhưng chưa trả trước | Không tính phí buổi vắng; không tạo số dư bảo lưu từ tiền chưa thu. |
| AC-12 | Số dư bảo lưu lớn hơn khoản phải trả | Khoản phải trả về 0; phần bảo lưu chưa dùng được thể hiện riêng. |
| AC-13 | Xuất báo cáo riêng cho một học sinh | Có đủ dữ liệu của học sinh được chọn và không lộ thông tin học sinh khác. |
| AC-14 | Ảnh mờ hoặc thiếu căn cứ chấm | Báo nguyên nhân; không sinh điểm 0; có thể tải lại và yêu cầu AI xử lý lại. |
| AC-15 | Chấm lại cùng bài hoặc kết quả chưa đủ tin cậy | Không tạo đầu điểm trùng; kết quả cần kiểm tra chưa dùng chính thức; giáo viên được sửa và xác nhận. |
| AC-16 | Có xu hướng giảm trong dữ liệu mẫu đủ lịch sử | Cảnh báo có giai đoạn và bằng chứng theo quy tắc đã duyệt; giáo viên ghi được hành động hỗ trợ. |
| AC-17 | Học sinh mới hoặc thiếu lịch sử | Báo chưa đủ dữ liệu; không kết luận mất gốc. |
| AC-18 | Không làm bài / Không có câu đúng / Không giao bài / Chưa đánh giá | Ghi nhận bằng 0 đúng chỉ số; không giao/chưa đánh giá loại khỏi trung bình và cảnh báo học lực; ô trống không tự thành 0. |
| AC-19 | AI tính tỷ lệ 0%, 25%, 50%, 60%, 75%, 90%, 100% | Quy đổi lần lượt thành trạng thái 0, 25%, 50%, 75%, 75%, 75%, 100% theo bảng; cả hai chỉ số áp dụng như nhau. |
| AC-20 | Chọn tự chấm mặc định, đổi một học sinh sang AI trước xử lý | Chỉ bài được chọn gọi AI; mỗi bài có một cách chấm đang áp dụng. |
| AC-21 | AI lỗi rồi chuyển sang tự chấm; AI trả về muộn | Giữ kết quả tự chấm, không áp dụng kết quả AI về muộn. |
| AC-22 | Sửa đề/đáp án/hướng dẫn | Đánh dấu bài dùng bản cũ, không tự chấm lại; cảnh báo và yêu cầu xác nhận trước thay kết quả đã sửa. |
| AC-23 | Sửa đánh giá sau khi tạo nhận xét | Nhận xét đánh dấu cần cập nhật, không tự ghi đè lời giáo viên; lần tạo lại dùng kết quả cuối đã xác nhận, không chấm lại ảnh. |
| AC-24 | AI viết nhận xét lỗi | Giáo viên vẫn tự viết và xuất PDF được. |
| AC-25 | Nợ cũ 500.000 đồng, phí mới 900.000 đồng, nhận 700.000 đồng | Tổng trước thu 1.400.000 đồng; phân bổ 500.000 đồng trả nợ cũ, 200.000 đồng trả kỳ mới; còn 700.000 đồng, QR cập nhật đúng. |
| AC-26 | Ngày 05/11 nhận 500.000 đồng trả nợ tháng 10 | Tiền thực thu tháng 11 tăng 500.000 đồng; dư nợ tháng 10 giảm 500.000 đồng; không tính hai lần. |
| AC-27 | Điều chỉnh/hủy giao dịch nhận tiền | Bắt buộc lý do, giữ lịch sử gốc; dư nợ, trạng thái, QR cập nhật; nhắc xuất lại PDF nếu đã gửi. |
| AC-28 | Không tạo được VietQR | Dùng QR tĩnh nếu có; nếu không thì cảnh báo và vẫn xuất PDF có đủ thông tin chuyển khoản; không dùng QR cũ sai tiền. |
| AC-29 | Sửa bản xem trước hoặc điều chỉnh phí | Thông tin/nhận xét sửa được; số liệu tài chính sửa từ nguồn hoặc khoản điều chỉnh có lý do; QR và PDF khớp. |
| AC-30 | Giáo viên mở đường dẫn ảnh/báo cáo của giáo viên khác | Bị từ chối truy cập; không lộ dữ liệu. |
| AC-31 | Đăng ký, xác minh email và chờ duyệt | Không truy cập lớp trước duyệt; gửi đối chiếu không tự cấp quyền; từ chối có lý do. |
| AC-32 | Admin cấp/gia hạn quyền | Ngày hiệu lực đúng quy tắc, giữ cấu hình từng kỳ; không gộp phí dịch vụ với học phí học sinh. |
| AC-33 | Quyền hết hạn hoặc tài khoản bị khóa | Hết hạn chỉ đọc/xuất; khóa chặn toàn bộ và vô hiệu phiên; mở khóa không tự gia hạn. |
| AC-34 | Admin tắt/bật quyền AI | Không nhận AI mới khi tắt, thủ công vẫn dùng được nếu còn hạn; bật lại không reset lượt và giữ kết quả cũ. |
| AC-35 | AI đồng thời, lỗi, hủy hoặc gửi lặp | Giữ/trừ/hoàn đúng kỳ và loại tác vụ, không vượt hạn mức hoặc tính hai lần. |
| AC-36 | Hết hạn/tắt AI/khóa khi AI đang chạy | Hai trường hợp đầu cho hoàn tất đã nhận; khóa hủy hoặc không áp dụng kết quả về muộn. |
| AC-37 | Phiên, đổi email hoặc đặt lại mật khẩu | Đúng idle/absolute timeout; liên kết một lần có hạn; phiên cũ vô hiệu, quyền đã mua giữ nguyên. |
| AC-38 | Tài khoản Admin | Bắt buộc xác thực hai bước; có lịch sử thao tác; không mặc định đọc dữ liệu học sinh, không tự đăng ký vai trò Admin. |

## 8. Các quyết định đã thống nhất và nội dung còn cần xác nhận

### 8.1. Các quyết định đã thống nhất qua phản hồi

- Tự chấm hoặc AI chấm được chọn trước khi xử lý; mặc định cho lượt, đổi riêng từng học sinh được trước khi bắt đầu.
- Dùng hai chỉ số chung với bốn mức và trạng thái riêng; giáo viên được sửa kết quả AI; giữ lịch sử và kết quả gốc.
- AI lỗi được chuyển sang tự chấm; kết quả về muộn không áp dụng. Sửa hướng dẫn không tự chấm lại.
- Nhận xét/cảnh báo dùng kết quả cuối giáo viên xác nhận; tạo nhận xét không chấm lại ảnh; AI lỗi được viết nhận xét thủ công.
- Ưu tiên VietQR, lỗi mới dùng QR tĩnh; không có cả hai vẫn xuất PDF kèm thông tin chuyển khoản và cảnh báo.
- Nhận tiền từng phần; nợ cũ chuyển tiếp, mặc định trả nợ cũ trước. Thống kê riêng ngày thực thu và kỳ học phí.
- Điều chỉnh/hủy giao dịch có lý do và lịch sử; tăng/giảm phí riêng qua khoản điều chỉnh. Số liệu nguồn, QR và PDF phải khớp.
- Giáo viên chỉnh thông tin và nhận xét trước khi xuất; chỉ xuất PDF cho phụ huynh, emoji tối giản.
- Mỗi giáo viên quản lý dữ liệu riêng, chưa có trợ giảng hoặc tài khoản dùng chung; buổi mới mặc định chưa điểm danh.
- Xác nhận thanh toán thủ công; chưa tự đối chiếu ngân hàng, quản lý nhiều chi nhánh, trả lương, lớp trực tuyến hoặc hoàn tiền tự động.

### 8.2. Nội dung còn cần khách hàng xác nhận

Các mục dưới đây chưa được coi là đã duyệt; cần kết luận trước khi triển khai hoặc nghiệm thu phần liên quan.

| STT | Nội dung cần xác nhận | Phương án đang đề xuất |
| --- | --- | --- |
| 1 | Học sinh tham gia nhiều lớp | Đề xuất quản lý học tập, học phí và bảo lưu riêng từng lớp. |
| 2 | Thu phí sau tháng học hay cho phép trả trước? | Tính phí theo buổi thực học; có ghi nhận trả trước để phục vụ bảo lưu. |
| 3 | Điều kiện nghỉ ốm được bảo lưu | Giáo viên phê duyệt; khách hàng xác nhận ngưỡng nghỉ dài ngày và giấy tờ nếu cần. |
| 4 | Thời hạn và phạm vi sử dụng bảo lưu | Cùng học sinh, cùng lớp; thời hạn tối đa cần xác nhận. |
| 5 | Cách xử lý tiền dư, dừng học, chuyển lớp | Giáo viên xử lý theo chính sách đã duyệt, có ghi nhận lý do. |
| 6 | Phạm vi AI chấm | Môn học, dạng bài, yêu cầu ảnh và hướng dẫn chấm được hỗ trợ. |
| 7 | Chất lượng AI | Bộ bài mẫu, mức chất lượng chấp nhận, thời gian xử lý và tiêu chí cần kiểm tra. |
| 8 | Cảnh báo xu hướng | Số bài tối thiểu, khoảng thời gian, ngưỡng cảnh báo và cách nghiệm thu. |
| 9 | Dữ liệu ảnh và AI | Thời hạn lưu, cách xóa và điều kiện sử dụng dữ liệu để huấn luyện AI. |
| 10 | Quy mô sử dụng dự kiến | Xác nhận số lớp, số học sinh/lớp và thiết bị thường dùng để thống nhất yêu cầu tốc độ. |

Việc duyệt các thay đổi ở mục 8.1 không đồng nghĩa các chính sách còn mở ở mục 8.2 đã được thống nhất.

## 9. Phê duyệt tài liệu

Việc phê duyệt tài liệu xác nhận mục tiêu, phạm vi và các quy trình nghiệp vụ được hai bên thống nhất. Những phương án còn đánh dấu “Đề xuất cần duyệt” phải được ghi nhận kết luận trước khi áp dụng vào vận hành.

| Đại diện | Họ tên | Ý kiến/phạm vi phê duyệt | Ngày xác nhận |
| --- | --- | --- | --- |
| Khách hàng |  |  |  |
| Bên thực hiện dự án |  |  |  |

**Trạng thái hiện tại: Chờ khách hàng xem xét và phê duyệt.**






