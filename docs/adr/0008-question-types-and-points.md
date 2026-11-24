# Dạng câu hỏi và điểm

**Trạng thái: accepted — 24/11/2026.** Thuộc issue #111 và các lát #112–#116 (cha #84). Thay cho #91 (câu nhiều đáp án) và mở rộng quyết định "Loại câu" đã chốt ở #77. Sửa ADR 0007 ở phần kết quả theo kỹ năng.

## Bối cảnh

Ngân hàng câu hỏi chỉ có trắc nghiệm một đáp án và điền ô bằng nút; đề bài là chữ thuần; kết quả chỉ đếm số câu đúng. Giáo viên cần thêm câu nhiều đáp án, điền từ gõ chữ có định dạng, âm thanh, câu tự luận chấm tay, và điểm cho từng câu.

## Quyết định

- **Bốn dạng câu** (mã loại trong dữ liệu):
  - `single_choice` Trắc nghiệm: một đáp án (như hiện nay).
  - `multi_choice` Trắc nghiệm nhiều đáp án: phương án A–F, đáp án là tập khoá; **đúng khi chọn đúng đủ**, không có điểm thành phần. Màn làm bài ghi rõ "Chọn n đáp án".
  - `fill_blanks` Điền từ: có hai **cách điền**: *chọn nút* (như hiện nay, chỗ trống viết `[ ]`) và *gõ chữ* (chỗ trống là thẻ `{{1}}`, `{{2}}`…, tối đa 10 chỗ, đáp án lưu riêng). Cách điền chọn nút giữ nguyên để đề cũ chạy.
  - `essay` Tự luận (điền ô lời giải): học sinh gõ văn bản tối đa 2.000 ký tự; **giáo viên chấm tay**.
- **Điểm câu**: từ 0,25 đến 10, bước 0,25, mặc định 1. Câu một đáp án: đúng thì đủ điểm. Câu điền từ nhiều chỗ: điểm chia đều cho các chỗ, mỗi chỗ đúng được phần của nó. Tự luận: giáo viên cho điểm từ 0 đến điểm tối đa kèm nhận xét ngắn, được sửa sau khi chấm.
- **So khớp câu gõ chữ**: bỏ qua chữ hoa/thường và khoảng trắng thừa, **giữ dấu** tiếng Việt (công tắc "bỏ qua dấu" theo từng câu), mỗi chỗ trống nhận nhiều đáp án (ngăn bằng `|`), số `0,5` bằng `0.5`.
- **Đề bài định dạng chỉ cho dạng điền từ**: HTML sạch gồm 4 font (không chân, có chân, chữ viết tay, chữ đều), 4 cỡ chữ, 6 màu cố định, đậm/nghiêng/gạch chân, căn lề, danh sách, ảnh, âm thanh. Máy chủ **làm sạch HTML theo danh sách cho phép** khi lưu (tối đa 20.000 ký tự) và trình duyệt làm sạch lại khi hiển thị. Ba dạng còn lại giữ đề chữ thuần và một ảnh đề. Excel chỉ nhập chữ thuần.
- **Âm thanh**: tải lên mp3 hoặc m4a tối đa 5 MB, lưu cùng kho ảnh đề của lớp, mỗi câu tối đa một âm thanh, phát lại không giới hạn.
- **Bài làm** lưu theo từng câu: một khoá; tập khoá; các giá trị theo chỗ trống; hoặc văn bản tự luận. Đáp án không bao giờ gửi xuống trình duyệt học sinh khi đề đang mở (ADR 0006).
- **Chấm tay**: bài nộp có câu tự luận ở trạng thái **Chờ chấm**. Học sinh thấy ngay điểm phần tự động kèm dòng "chưa gồm phần tự luận". Giáo viên chấm theo câu (xem lần lượt bài cả lớp, lưu và sang bài kế) hoặc theo học sinh. Điểm tổng và kết quả theo kỹ năng chỉ chốt khi mọi câu tự luận đã chấm.
- **Xác nhận**: không xác nhận được kết quả khi còn bài tự luận chưa chấm; chấm và sửa điểm được khi đề đang mở hoặc đã đóng, sau khi xác nhận điểm là cuối cùng. Kết quả theo kỹ năng được lưu bằng số câu tương đương (làm tròn điểm đạt / điểm tối đa theo số câu, xem ADR 0007).
- **Tổng điểm**: tổng điểm các câu, hiện "x / y điểm" và quy đổi thang 10 (làm tròn đến 0,01).
- **Dữ liệu**: migration chỉ thêm cột (điểm mặc định 1, đề bài định dạng, tệp âm thanh, cách điền, công tắc bỏ qua dấu, đáp án mẫu của tự luận) và loại câu mới. Bản chụp câu trong đề mang các trường này. Bài đã nộp trước đây tính điểm 1 mỗi câu như cũ.
- **Mẫu Excel**: thêm cột Dạng, Điểm, Phương án E–F; cột Đáp án: `B` (một đáp án), `A,C` (nhiều đáp án), điền từ gõ chữ viết đáp án ngay trong đề `[Hà Nội|Ha Noi]` (nhập thành `{{1}}` kèm đáp án), tự luận để trống hoặc ghi đáp án mẫu. Điểm để trống thì lấy 1. Tệp mẫu cũ vẫn nhập được.

## Hệ quả

- Migration không rút lại được (cột thêm); mã cũ không đọc được loại câu mới.
- Một câu điền từ có điểm lẻ nên "đúng/sai" của câu không còn là hai giá trị; mọi nơi đếm "số câu đúng" phải chuyển sang điểm (xem ADR 0007).
- Trình soạn thảo Tiptap nạp lười chỉ ở màn soạn câu hỏi; không đổi gói ban đầu.
- Tự luận không có ảnh bài giải viết tay ở bản đầu.
