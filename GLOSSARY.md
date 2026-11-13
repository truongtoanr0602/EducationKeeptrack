# Education Keeptrack

Thuật ngữ dùng chung cho quản lý lớp và cung cấp dịch vụ phần mềm cho giáo viên.

## Language

**Bài đánh giá**:
Đề bài dùng chung cho một lượt chấm của lớp vào một ngày xác định.

**Bài nộp**:
Bài của một học sinh trong một bài đánh giá, có cách chấm và lịch sử đánh giá riêng.

**Bản nháp đánh giá**:
Hai mức hoặc trạng thái giáo viên đang ghi nhận nhưng chưa xác nhận làm kết quả cuối.

**Giáo viên**:
Người sử dụng phần mềm để quản lý lớp, học sinh và học phí trong phạm vi dữ liệu riêng.

**Admin**:
Người quản lý đăng ký, đối chiếu thanh toán dịch vụ và quyền sử dụng của giáo viên; không mặc định được đọc dữ liệu học sinh.

**Phí dịch vụ**:
Khoản giáo viên trả để sử dụng Education Keeptrack.
_Avoid_: Học phí, tiền phụ huynh

**Học phí**:
Khoản phụ huynh hoặc học sinh trả giáo viên cho các buổi học.
_Avoid_: Phí dịch vụ, phí tài khoản

**Yêu cầu đối chiếu thanh toán dịch vụ**:
Thông tin giáo viên gửi để Admin kiểm tra khoản chuyển tiền mua hoặc gia hạn dịch vụ; không phải chứng nhận đã thanh toán.

**Kỳ quyền sử dụng**:
Khoảng thời gian sử dụng phần mềm được Admin cấp cho giáo viên, kèm điều kiện và hạn mức tại thời điểm cấp.

**Quyền AI**:
Sự cho phép dùng các chức năng AI do Admin bật hoặc tắt cho từng tài khoản, độc lập với hạn mức và thời hạn phần mềm.

**Hạn mức AI**:
Số lượt được cấp theo từng loại tác vụ trong một kỳ quyền sử dụng.

**Lượt AI**:
Một lần xử lý AI được chấp nhận theo một trong ba loại, mỗi loại được theo dõi riêng: **chấm bài** (một bài của một học sinh), **nhận xét** (một nhận xét theo buổi hoặc theo tháng cho một học sinh) và **kế hoạch** (một kế hoạch ôn tập tuần hoặc một dự thảo kỹ năng). Tạo lại là lượt mới. Một lần tạo kế hoạch ôn tập tuần cho cả lớp là một lượt kế hoạch (chốt 12/11/2026). Đơn vị lượt của cảnh báo AI chưa được chốt.

**Lượt đang giữ**:
Phần hạn mức đã dành cho yêu cầu AI được chấp nhận và chưa có kết quả cuối.

**Hết hạn sử dụng**:
Kỳ sử dụng đã kết thúc; giáo viên vẫn được xem và xuất dữ liệu cũ nhưng không thêm, sửa hoặc yêu cầu AI mới.

**Khóa tài khoản**:
Quyết định của Admin chặn truy cập, kể cả dữ liệu cũ, và vô hiệu các phiên; khác với hết hạn sử dụng.

**Kết quả cuối**:
Hai mức đánh giá do giáo viên xác nhận, dùng chính thức trong hồ sơ, báo cáo và phân tích.

**Kỹ năng**:
Đơn vị nhỏ nhất có thể đánh giá của chương trình, ví dụ "cộng qua 10 trong phạm vi 20"; chỉ có hiệu lực khi giáo viên đã duyệt. Nhãn rộng như "Số và phép tính" không phải kỹ năng.
_Avoid_: Chủ đề, mạch nội dung

**Chuẩn đầu ra**:
Tiêu chí đạt của một kỹ năng, có nguồn và phiên bản, do giáo viên duyệt cho từng lớp; AI không tự tạo chuẩn chính thức.

**Học liệu**:
Nội dung luyện tập trong kho của giáo viên, gắn kỹ năng, độ khó và thời lượng; chỉ học liệu đã duyệt được đưa vào kế hoạch.

**Kết quả theo kỹ năng**:
Số nhiệm vụ và số đúng của một học sinh ở một kỹ năng trong một bài đánh giá, do giáo viên xác nhận; khác với kết quả cuối hai mức và không suy ra được từ hai mức đó.

**Tiến độ học sinh**:
Hai chuỗi độc lập theo thời gian, mức hoàn thành và mức làm đúng, lấy từ kết quả cuối của các bài đánh giá. Không làm bài hoặc không có câu đúng tính 0 ở chỉ số tương ứng; không giao bài và chưa đánh giá là khoảng trống, không tính 0 và không suy ra sa sút. Mỗi mức là nhóm giáo viên chọn, không phải tỷ lệ chính xác.
_Avoid_: Điểm học lực

**Hồ sơ năng lực**:
Mức đạt, chưa đạt hoặc chưa đủ dữ liệu của một học sinh theo từng kỹ năng đã dạy, tính bằng quy tắc có phiên bản từ các kết quả theo kỹ năng đã xác nhận; AI không tự tính hay đổi.
_Avoid_: Điểm học lực

**Kế hoạch ôn tập**:
Kế hoạch theo tuần cho học sinh với tối đa hai kỹ năng ưu tiên mỗi em, hoạt động từng buổi trong quỹ phút của giáo viên; có phiên bản và chỉ áp dụng sau khi giáo viên duyệt.

**Nhận xét buổi / nhận xét tháng**:
Lời nhận xét về một học sinh sau một buổi học hoặc trong báo cáo tháng gửi phụ huynh; có thể do AI soạn nhưng chỉ nội dung giáo viên đã duyệt được dùng và đưa vào PDF.

**Bản xuất lỗi thời**:
Bản PDF báo cáo tháng đã lưu mà dữ liệu nguồn (điểm danh, kết quả đánh giá, học phí, thông tin nhận tiền) hoặc bản nháp nhận xét đã thay đổi sau khi xuất. File đã xuất không bao giờ bị sửa; muốn cập nhật thì xuất bản mới từ dữ liệu hiện tại, bản cũ vẫn giữ và được đánh dấu lỗi thời. Khi học phí còn đang cập nhật thì chưa kết luận bản nào còn mới.
_Avoid_: Bản hết hạn (hết hạn là việc của kỳ sử dụng và liên kết tải)

**Lịch tuần**:
Các ngày và khung giờ học lặp lại trong tuần của một lớp, áp dụng từ ngày hiệu lực.

**Buổi học**:
Một lần học của lớp vào ngày và khung giờ cụ thể; có thể dự kiến, đã diễn ra hoặc đã hủy.

**Học bù**:
Buổi học bổ sung do giáo viên tạo để bù việc học; có thể gắn với buổi đã hủy.

**Điểm danh**:
Ghi nhận một học sinh chưa được điểm danh, có mặt hoặc vắng ở một buổi học; khi vắng có thể ghi lý do và nghỉ có phép.
_Avoid_: Tình trạng theo học, kết quả học tập

**Tình trạng theo học**:
Trạng thái đang học, nghỉ có phép hoặc dừng học của đăng ký lớp; khác với điểm danh của từng buổi.

**Đơn giá hiệu lực**:
Số tiền học phí mỗi buổi áp dụng từ một ngày xác định; đơn giá mới không tự thay đổi căn cứ của buổi cũ.

**Phí từng buổi**:
Khoản học phí của một học sinh cho một buổi đã diễn ra có mặt, theo đơn giá áp dụng của buổi đó.

**Bảng phí tháng**:
Bảng đối chiếu học phí phát sinh trong tháng với các buổi học làm căn cứ; khác với tiền đã nhận hoặc dư nợ.

**Giao dịch nhận tiền**:
Một khoản học phí giáo viên thực nhận của một học sinh trong một lớp, có số tiền, ngày nhận và ghi chú; khác với phí phát sinh.

**Phân bổ thanh toán**:
Phần của một giao dịch nhận tiền dùng trả nghĩa vụ của một kỳ học phí, mặc định kỳ cũ nhất còn nợ trước.

**Dư nợ kỳ**:
Phần nghĩa vụ học phí của một kỳ còn phải trả sau các khoản phân bổ, điều chỉnh và bảo lưu đã sử dụng; không âm.

**Tiền chưa phân bổ**:
Phần tiền đã nhận chưa dùng trả nghĩa vụ học phí, được theo dõi riêng và chưa tự áp dụng cho kỳ sau hoặc hoàn trả.
_Avoid_: Tiền bảo lưu, tiền mới thu

**Tiền giữ để đối chiếu**:
Phần tiền đã phân bổ vượt nghĩa vụ sau khi phí giảm, được giữ riêng để xử lý rõ ràng; chưa được dùng lại, bảo lưu hoặc hoàn trả tự động.

