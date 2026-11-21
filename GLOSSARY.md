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
Tiêu chí đạt của một kỹ năng, có nguồn và phiên bản, do giáo viên duyệt cho từng lớp; AI không tự tạo chuẩn chính thức. Mỗi buổi chương trình cũng có chuẩn đầu ra mô tả điều học sinh cần đạt sau buổi đó; phần đánh giá vẫn tính theo kỹ năng.

**Buổi chương trình**:
Buổi thứ N trong chương trình của lớp, không có ngày: nội dung SGK, kiến thức, kỹ năng cần đạt, chuẩn đầu ra, năng lực và các kỹ năng (mã) của buổi. Buổi học thứ N chưa bị hủy của lớp tự gắn với buổi chương trình thứ N, giáo viên sửa được; khi buổi học đã diễn ra, các kỹ năng của buổi chương trình được tính là đã dạy.
_Avoid_: Buổi học (buổi có ngày), tiết, bài

**Ngân hàng câu hỏi**:
Kho câu hỏi của một lớp, giáo viên tải lên bằng file Excel mẫu hoặc soạn trên web; tải lại cùng mã câu thì cập nhật, không tạo mới. Có thể sao chép từ lớp khác của cùng giáo viên.

**Câu hỏi**:
Một câu trong ngân hàng, có mã, loại (trắc nghiệm một đáp án hoặc điền ô bằng nút bấm), mức độ, đáp án, một kỹ năng chính và tối đa hai kỹ năng phụ. Câu điền ô chỉ đúng khi mọi ô đều đúng. Câu không bị xóa, chỉ ngừng dùng.

**Mức độ câu hỏi**:
Bốn mức cố định: Nhận biết, Thông hiểu, Vận dụng, Vận dụng cao. Khác với độ khó của học liệu.
_Avoid_: Độ khó (của câu hỏi)

**Kỹ năng chính / kỹ năng phụ**:
Kỹ năng chính là kỹ năng duy nhất được tính đúng/sai khi học sinh trả lời câu hỏi; kỹ năng phụ chỉ dùng để tìm và lọc câu.

**Cấu trúc đề**:
Số câu theo từng mức độ câu hỏi (ví dụ 4-3-2-1) và phạm vi (từ buổi chương trình đến buổi, hoặc danh sách kỹ năng). Hệ thống bốc câu phủ đều các kỹ năng trong phạm vi; thiếu câu ở mức nào thì báo thiếu, không lấy câu mức khác bù vào.

**Đề kiểm tra**:
Bộ câu hỏi bốc từ ngân hàng theo cấu trúc đề, giữ bản chụp từng câu nên sửa ngân hàng không làm đổi đề đã phát. Đề chung là một đề cho cả lớp; khi mở đề, hệ thống tạo một bài đánh giá tương ứng. Kết quả chỉ thành kết quả cuối và kết quả theo kỹ năng khi giáo viên bấm xác nhận.

**Mã vào bài**:
Mã bốn số riêng của từng học sinh trong một đề kiểm tra, dùng cùng đường link của đề để vào làm bài mà không cần tài khoản. Nhập sai năm lần thì mã bị khóa đến khi giáo viên mở lại.

**Lượt làm bài**:
Lần làm bài online duy nhất của một học sinh trong một đề kiểm tra: tự lưu sau mỗi câu, tự nộp khi hết giờ hoặc khi giáo viên đóng đề; giáo viên có thể cho em làm lại.

**Học liệu**:
Nội dung luyện tập trong kho của giáo viên, gắn kỹ năng, độ khó và thời lượng; chỉ học liệu đã duyệt được đưa vào kế hoạch.

**Kết quả theo kỹ năng**:
Số nhiệm vụ và số đúng của một học sinh ở một kỹ năng trong một bài đánh giá, do giáo viên xác nhận; khác với kết quả cuối hai mức và không suy ra được từ hai mức đó.

**Tiến độ học sinh**:
Hai chuỗi độc lập theo thời gian, mức hoàn thành và mức làm đúng, lấy từ kết quả cuối của các bài đánh giá. Không làm bài hoặc không có câu đúng tính 0 ở chỉ số tương ứng; không giao bài và chưa đánh giá là khoảng trống, không tính 0 và không suy ra sa sút. Mỗi mức là nhóm giáo viên chọn, không phải tỷ lệ chính xác.
_Avoid_: Điểm học lực

**Hồ sơ năng lực**:
Mức đạt, chưa đạt hoặc chưa đủ dữ liệu của một học sinh theo từng kỹ năng đã dạy, tính bằng quy tắc có phiên bản từ các kết quả theo kỹ năng đã xác nhận; AI không tự tính hay đổi. Kết quả từ đề kiểm tra online được cộng dồn giữa các đề vì mỗi đề chỉ có vài câu cho một kỹ năng (ADR 0007).
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

**Đề nghị bảo lưu**:
Đề nghị giữ lại một phần tiền đã thu của một học sinh vì nghỉ ốm, do giáo viên ghi thay phụ huynh: khoảng ngày nghỉ, lý do, ghi chú minh chứng tự do và số tiền. Chỉ có hiệu lực khi có tiền đã thu, chưa dùng cho học phí và chưa nằm trong bảo lưu hoặc đề nghị khác; vắng mặt không tự tạo bảo lưu. Giáo viên duyệt hoặc từ chối từng đề nghị kèm lý do.

**Bảo lưu**:
Phần tiền chưa phân bổ của các khoản nhận tiền thật được giáo viên duyệt giữ lại, ghi rõ lấy từ khoản nhận nào và bao nhiêu. Không phải tiền mới nhận, không tự trừ vào học phí kỳ sau và không tự hoàn trả. Số ngày nghỉ tối thiểu, minh chứng, hạn dùng, chuyển hoặc dừng lớp và hoàn tiền chưa có chính sách, nên mỗi trường hợp do giáo viên quyết định. Khoản nhận tiền còn bảo lưu chưa được sửa hoặc hủy.

**Khấu trừ bảo lưu**:
Giáo viên áp một phần số dư bảo lưu vào nghĩa vụ học phí còn phải trả của một kỳ: tối đa bằng phần còn nợ của kỳ đó và tối đa bằng số dư bảo lưu còn lại, lấy từ bảo lưu duyệt sớm nhất trước. Làm giảm dư nợ kỳ nhưng không phải tiền mới nhận nên không vào thống kê thu tiền, và một khoản nhận tiền mới chỉ phân bổ phần nợ còn lại sau khấu trừ. Có thể hủy từng lần khấu trừ kèm lý do. Nếu học phí giảm sau đó làm phần đã khấu trừ vượt nghĩa vụ, phần vượt được báo để giáo viên hủy, không tự dùng lại.

**Sổ bảo lưu**:
Với mỗi học sinh và mỗi tháng: bảo lưu đầu kỳ, mới duyệt, đã dùng, đã hủy và chuyển sang kỳ sau, tính theo ngày duyệt, ngày khấu trừ và ngày hủy theo giờ Việt Nam. Hiển thị trong ứng dụng và trên PDF báo cáo tháng, cùng tổng cần thu đã trừ bảo lưu. Hủy bảo lưu chỉ được khi không còn lần khấu trừ nào đang áp dụng.

**Hôm nay**:
Màn hình đầu của giáo viên: các buổi học của mọi lớp trong ngày hiện tại theo giờ Việt Nam, kèm điểm danh nhanh. Không phải bảng thống kê.

**Hóa đơn** (tên mục trong giao diện):
Mục xuyên lớp liệt kê dư nợ kỳ của từng học sinh, nơi giáo viên ghi nhận Giao dịch nhận tiền và chia sẻ PDF hoặc mã QR cho phụ huynh. Không phải một thực thể riêng trong dữ liệu; chưa có phiếu thu PDF riêng ngoài báo cáo tháng.
_Avoid_: Phí dịch vụ

**Tiền giữ để đối chiếu**:
Phần tiền đã phân bổ vượt nghĩa vụ sau khi phí giảm, được giữ riêng để xử lý rõ ràng; chưa được dùng lại, bảo lưu hoặc hoàn trả tự động.

