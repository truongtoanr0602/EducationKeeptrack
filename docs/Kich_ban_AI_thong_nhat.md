# KỊCH BẢN AI THỐNG NHẤT – EDUCATIONKEEPTRACK

**Ngày lập:** 11/11/2026.\
**Trạng thái:** Bản thống nhất phạm vi trước khi triển khai. Thay thế việc đọc rời hai nguồn: báo cáo kịch bản AI ngày 02/11 và các ticket AI T13–T18, T26.\
**Nguồn:** [Báo cáo kịch bản AI](Bao_cao_kich_ban_AI_EducationKeeptrack.md), [BA Requirements](BA_Requirements_Keeptrack.md), [ADR-0003](adr/0003-service-access-and-ai.md), [GLOSSARY](../GLOSSARY.md).

## 1. Quyết định phạm vi

| Nội dung | Quyết định | Ngày |
|---|---|---|
| Phạm vi v1 của AI | **Gợi ý ôn tập tuần theo kỹ năng** (UC-AI-01…06 trong báo cáo 02/11). Lớp thí điểm: Toán lớp 2, học kỳ 1, ưu tiên 1–2 học sinh. | 11/11 |
| UC-AI-01 (thiết lập chương trình) | AI **soạn dự thảo** kỹ năng và chuẩn; giáo viên duyệt từng mục. | 11/11 |
| UC-AI-03 (hồ sơ năng lực) | AI **đánh giá học sinh theo từng buổi** và **trong báo cáo tháng gửi phụ huynh**. Mức đạt vẫn do quy tắc hệ thống tính. | 11/11 |
| AI chấm bài từ ảnh (T13–T15) | Làm **sau** v1. Môn và dạng bài chấm xử lý khi làm giai đoạn này. | 11/11 |
| Cảnh báo sớm (T18) | Làm **sau** v1, giữ nguyên ticket hiện có. | 11/11 |
| AI nhận xét tháng (T16) | **Gộp vào T37**: một tính năng nhận xét tháng duy nhất, dựa trên kết quả theo kỹ năng. Khi có AI chấm bài, thêm hai chỉ số bốn mức làm nguồn dữ liệu của T37, không tạo luồng thứ hai. Issue #17 (T16) cần đóng hoặc viết lại. | 11/11 |
| Nhận xét theo buổi | Giáo viên bấm tạo cho từng học sinh; **mỗi lần tạo tính một lượt**. | 11/11 |
| Loại lượt | **Hai loại:** (1) *Nhận xét* dùng loại "sinh nhận xét" đã có trong ADR-0003, áp dụng cho nhận xét buổi và tháng; (2) *Kế hoạch* là loại mới, áp dụng cho kế hoạch ôn tập tuần và dự thảo kỹ năng. | 11/11 |
| Giọng văn gửi phụ huynh | Trung tính, không nhãn tiêu cực, 3–5 câu, có phần gợi ý cho gia đình; giáo viên chỉnh được. | 11/11 |
| Đơn vị tính lượt | Tạo **loại lượt riêng** cho tác vụ lập kế hoạch ôn tập, có hạn mức riêng do Admin cấp. Không tính chung với chấm bài hoặc nhận xét; không mặc định miễn phí. | 11/11 |
| Nhà cung cấp AI | Chưa chọn. Dự kiến DeepSeek hoặc model rẻ/miễn phí. Đặt sau một interface để đổi được. | 11/11 |

## 2. Mục tiêu v1

Giáo viên dùng kết quả đánh giá theo kỹ năng đã xác nhận để xác định nội dung cần ôn, rồi nhận dự thảo kế hoạch ôn tập tuần. Giáo viên sửa và duyệt trước khi áp dụng.

Chu trình một tuần:

**Ghi kết quả theo kỹ năng → hệ thống tính mức đạt theo quy tắc → AI đề xuất kế hoạch → giáo viên sửa và duyệt → tổ chức buổi ôn → đánh giá lại.**

## 3. Phân vai giữa hệ thống và AI

Đây là điểm quan trọng nhất khi dùng model rẻ: **mọi quyết định có thể tính bằng quy tắc thì hệ thống tự tính, AI không đụng vào.**

| Việc | Ai làm |
|---|---|
| Soạn dự thảo danh sách kỹ năng và chuẩn từ bộ sách, chương (UC-AI-01) | **AI** soạn, **giáo viên** duyệt từng mục |
| Tính mức đạt / chưa đạt / chưa đủ dữ liệu theo từng kỹ năng | **Hệ thống** (quy tắc BR-AI-04, BR-AI-05) |
| Viết nhận xét theo buổi và nhận xét trong báo cáo tháng từ dữ liệu đã có (UC-AI-03) | **AI** viết dự thảo, **giáo viên** sửa và duyệt |
| Chọn tối đa hai kỹ năng ưu tiên cho mỗi em | **Hệ thống** chọn danh sách ứng viên theo quy tắc; AI chỉ xếp thứ tự và giải thích khi cần |
| Chọn học liệu từ kho đã duyệt, sắp xếp vào từng buổi, đề xuất hoạt động chung/riêng | **AI** |
| Giải thích lý do chọn nội dung, cách kiểm tra lại | **AI** |
| Kiểm tra đầu ra AI (cấu trúc, học liệu có thật, kỹ năng thuộc ứng viên, tổng phút ≤ quỹ phút) | **Hệ thống**, trước khi lưu dự thảo |
| Duyệt và áp dụng kế hoạch | **Giáo viên** |

AI không tự đặt chuẩn, không tự đổi ngưỡng, không tự xác nhận kết quả.

## 4. Quy tắc nghiệp vụ

Giữ nguyên BR-AI-01…12 trong [báo cáo 02/11](Bao_cao_kich_ban_AI_EducationKeeptrack.md#5-quy-tắc-nghiệp-vụ). Tóm tắt:

- Chuẩn và bộ sách do giáo viên cấu hình, có nguồn và phiên bản; chỉ đánh giá nội dung đã dạy.
- Chỉ dùng kết quả đã được giáo viên xác nhận.
- Ngưỡng thí điểm: đạt ≥ 80% trong **từng** lần của hai lần đánh giá khác nhau, mỗi lần ≥ 5 nhiệm vụ cùng kỹ năng. Thiếu thì ghi "chưa đủ dữ liệu", không kết luận yếu.
- Kế hoạch theo tuần, tối đa hai kỹ năng mỗi em, không vượt quỹ phút giáo viên nhập.
- Ưu tiên học liệu đã duyệt. Bài tập AI tạo mới phải được giáo viên duyệt trước khi giao.
- Lớp hai em: mục tiêu và kết quả quản lý riêng; chỉ ghép hoạt động khi phù hợp.
- AI chỉ tạo dự thảo. Mỗi bản kế hoạch lưu dữ liệu đầu vào, nội dung AI gốc, chỉnh sửa của giáo viên và lịch sử duyệt.

### 4.1. Bổ sung cho nhà cung cấp AI giá rẻ

| Mã | Quy tắc |
|---|---|
| BR-AI-13 | Chỉ gửi dữ liệu cần thiết: mã học sinh (không gửi tên, không gửi thông tin liên hệ), mã kỹ năng, số liệu kết quả, mã học liệu, quỹ phút. Ghi nhận nhà cung cấp và model đã dùng cho từng lượt. |
| BR-AI-14 | Đầu ra AI phải theo cấu trúc cố định và được kiểm tra. Đầu ra sai cấu trúc, tham chiếu học liệu không tồn tại, chọn kỹ năng ngoài danh sách ứng viên hoặc vượt quỹ phút đều bị loại, không lưu thành dự thảo. |
| BR-AI-15 | Đổi nhà cung cấp hoặc model không được làm đổi nghiệp vụ. Mọi lời gọi đi qua một interface; môi trường kiểm thử dùng adapter mô phỏng. |
| BR-AI-16 | Chưa chọn nhà cung cấp thì chức năng chỉ chạy được với adapter mô phỏng. Luồng mô phỏng không phải bằng chứng về chất lượng AI. |
| BR-AI-17 | (UC-AI-01) Kỹ năng AI soạn ở trạng thái "dự thảo", ghi rõ nguồn gợi ý. Chưa được giáo viên duyệt thì không dùng để đánh giá, không đưa vào hồ sơ năng lực hay kế hoạch. |
| BR-AI-18 | (UC-AI-03) Nhận xét AI chỉ dựa trên dữ liệu có trong hệ thống: điểm danh, kết quả theo kỹ năng đã xác nhận, ghi chú giáo viên. Không bịa thành tích, lỗi sai hay nguyên nhân không có trong dữ liệu. Thiếu dữ liệu thì nêu rõ "chưa đủ dữ liệu". |
| BR-AI-19 | (UC-AI-03) Nhận xét theo buổi và nhận xét tháng đều là dự thảo. Chỉ nội dung giáo viên đã duyệt mới được đưa vào báo cáo tháng gửi phụ huynh. Hệ thống không tự gửi cho phụ huynh. |
| BR-AI-20 | (UC-AI-03) Nhận xét gửi phụ huynh dài 3–5 câu, ngôn ngữ trung tính, mô tả kỹ năng và bằng chứng, kèm phần gợi ý cho gia đình. Cấm nhãn tiêu cực như "yếu", "kém", "chậm". Hệ thống kiểm tra danh sách từ cấm trước khi lưu dự thảo; giáo viên vẫn chỉnh sửa được. |
| BR-AI-21 | (UC-AI-03) Sửa kết quả hoặc điểm danh sau khi nhận xét được tạo thì nhận xét đó bị đánh dấu lỗi thời, không tự ghi đè lời giáo viên đã sửa. |

## 5. Quyền và hạn mức (theo ADR-0003)

- Quyền AI do Admin bật/tắt theo tài khoản. Không có quyền AI vẫn lập kế hoạch ôn tập thủ công được, nếu tài khoản còn hạn.
- Có hai loại lượt cho v1:
  - **Kế hoạch** (loại mới): tạo kế hoạch ôn tập tuần (UC-AI-04) và soạn dự thảo kỹ năng (UC-AI-01).
  - **Nhận xét** (dùng lại loại "sinh nhận xét" của ADR-0003): nhận xét theo buổi và theo tháng (UC-AI-03).
- Giữ lượt trước khi gửi, trừ khi có dự thảo hợp lệ, hoàn khi lỗi/hủy, không trừ hai lần khi retry.
- Mỗi lần giáo viên bấm tạo nhận xét cho một học sinh tính một lượt. Tạo lại tính lượt mới. Nhận xét buổi phát sinh nhiều lượt hơn nhận xét tháng nên số lượt mặc định phải đủ cho lớp 1–2 học sinh (xem mục 9, điểm 2).
- Cảnh báo AI (T18) vẫn chưa có đơn vị lượt, giữ nguyên là giai đoạn sau.
- Tắt AI hoặc hết hạn không hủy lượt đã nhận; kết quả chỉ là dự thảo. Khóa tài khoản thì hủy hoặc vô hiệu kết quả về muộn.
- Hết hạn vẫn xem/xuất kế hoạch cũ, không tạo hoặc sửa kế hoạch mới.
- Thay đổi cần làm trong ServiceAccess: thêm loại tác vụ mới vào sổ hạn mức và màn hình Admin/giáo viên. Số lượt mặc định chưa chốt, Admin phải cấp rõ.

## 6. Kịch bản

| Mã | Kịch bản | Ghi chú v1 |
|---|---|---|
| UC-AI-01 | Thiết lập chương trình cho lớp | **Có AI:** soạn dự thảo kỹ năng và chuẩn, giáo viên duyệt (xem 6.1) |
| UC-AI-02 | Ghi nhận kết quả theo kỹ năng | Cần làm trước, không có AI |
| UC-AI-03 | Hồ sơ năng lực và đánh giá học sinh | **Có AI:** nhận xét theo buổi và trong báo cáo tháng (xem 6.2) |
| UC-AI-04 | Tạo đề xuất ôn tập tuần | **Bước gọi AI duy nhất của v1** |
| UC-AI-05 | Chỉnh sửa, duyệt kế hoạch | Thủ công, có phiên bản |
| UC-AI-06 | Ghi nhận buổi ôn và đánh giá lại | Dùng lại UC-AI-02 |

### 6.1. UC-AI-01 – AI soạn dự thảo kỹ năng và chuẩn

1. Giáo viên chọn lớp, môn, học kỳ và bộ sách; có thể nhập tên chương hoặc dán mục lục.
2. Giáo viên chọn "AI soạn dự thảo". Hệ thống kiểm tra quyền, hạn mức rồi giữ lượt.
3. AI trả danh sách kỹ năng đề xuất, mỗi kỹ năng có mô tả, tiêu chí đạt và chương liên quan. Hệ thống kiểm tra cấu trúc, mã trùng và kỹ năng quá rộng (ví dụ nhãn "Số và phép tính").
4. Dự thảo lưu ở trạng thái chờ duyệt (BR-AI-17).
5. Giáo viên duyệt, sửa, gộp, tách hoặc xóa từng kỹ năng, gắn học liệu và đánh dấu nội dung đã dạy. Kỹ năng duyệt xong mới có hiệu lực.

**Ngoại lệ:** AI lỗi hoặc trả kết quả không hợp lệ thì hoàn lượt, giáo viên nhập thủ công. Không có quyền AI vẫn thiết lập thủ công được. AI không nhận diện được bộ sách thì nêu rõ, không tự suy đoán nội dung.

### 6.2. UC-AI-03 – Đánh giá học sinh theo buổi và trong báo cáo tháng

**Tác nhân chính:** Giáo viên.\
**Điều kiện:** Buổi học đã có điểm danh. Có kết quả theo kỹ năng đã xác nhận nếu buổi có đánh giá.

*Nhận xét theo buổi:*

1. Sau buổi học, giáo viên mở học sinh và chọn "AI soạn nhận xét buổi".
2. Hệ thống chụp đầu vào: điểm danh, kỹ năng đã dạy trong buổi, kết quả đã xác nhận, mức đạt hiện tại theo quy tắc và ghi chú giáo viên.
3. AI viết dự thảo ngắn: học sinh đã làm gì, điểm tốt, điểm cần củng cố, căn cứ.
4. Giáo viên sửa và duyệt. Nhận xét được duyệt gắn vào buổi học.

*Nhận xét trong báo cáo tháng gửi phụ huynh:*

1. Giáo viên mở báo cáo tháng (PDF, cùng nơi với học phí và hóa đơn của Reporting).
2. Giáo viên chọn "AI soạn nhận xét tháng". Đầu vào gồm điểm danh cả tháng, các nhận xét buổi đã duyệt, mức đạt theo kỹ năng và tiến bộ so với tháng trước.
3. AI viết dự thảo: chuyên cần, kỹ năng đã tiến bộ, kỹ năng cần củng cố, gợi ý cho gia đình.
4. Giáo viên sửa và duyệt. PDF chỉ chứa nội dung đã duyệt (BR-AI-19). Hệ thống không tự gửi cho phụ huynh.

**Ngoại lệ:**

- Thiếu dữ liệu: nêu "chưa đủ dữ liệu", không kết luận (BR-AI-18, BR-AI-20).
- Dữ liệu nguồn sửa sau khi tạo: nhận xét đánh dấu lỗi thời, giáo viên quyết định tạo lại (BR-AI-21).
- AI lỗi: giáo viên tự viết, vẫn xuất PDF được.
- Học sinh vắng cả buổi hoặc cả tháng: ghi đúng tình trạng, không diễn giải thành thất bại học tập.
- Báo cáo có hóa đơn: phần học phí do Billing tính, AI không đọc và không viết về số tiền.

Mức đạt của từng kỹ năng vẫn do hệ thống tính. AI chỉ diễn đạt lại kết quả đó bằng lời, không đổi ngưỡng hay tạo điểm.

Chi tiết các bước và ngoại lệ của các kịch bản còn lại xem [báo cáo 02/11, mục 6](Bao_cao_kich_ban_AI_EducationKeeptrack.md#6-danh-mục-kịch-bản). Ví dụ lớp hai học sinh (An, Bình) ở mục 7 của báo cáo đó dùng làm dữ liệu kiểm thử mẫu.

## 7. Đề xuất ticket cho v1

Chưa tạo trên GitHub. Số T27 trở đi để tránh trùng T01–T26. Mỗi ticket là một lát cắt dùng được (dữ liệu, API, UI, kiểm thử), theo quy ước hiện có.

| Ticket đề xuất | Nội dung | Phụ thuộc |
|---|---|---|
| T27 | Cấu hình chương trình lớp **thủ công**: bộ sách, kỹ năng và chuẩn đã duyệt, đánh dấu nội dung đã dạy (UC-AI-01) | T01–T03 (đã xong) |
| T28 | Kho học liệu đã duyệt, gắn kỹ năng, độ khó, thời lượng | T27 |
| T29 | Kết quả theo kỹ năng: số nhiệm vụ, số đúng, xác nhận, lịch sử (UC-AI-02) | T27 |
| T30 | Hồ sơ năng lực: mức đạt / chưa đạt / chưa đủ dữ liệu theo quy tắc có phiên bản, xem bằng chứng (UC-AI-03) | T29 |
| T31 | Lập kế hoạch ôn tập **thủ công**: quỹ phút, hoạt động từng buổi, phiên bản, duyệt (UC-AI-05) | T28, T30 |
| T32 | Loại lượt **Kế hoạch** mới trong ServiceAccess (cạnh loại "sinh nhận xét" đã có): hạn mức theo kỳ, giữ/trừ/hoàn; hiển thị cho Admin và giáo viên | T26 (hạn mức AI, issue #27) |
| T33 | AI đề xuất kế hoạch tuần: chụp đầu vào, tác vụ nền, interface nhà cung cấp, adapter mô phỏng, kiểm tra đầu ra, lưu dự thảo (UC-AI-04) | T31, T32 |
| T34 | Ghi nhận buổi ôn thực tế, đánh giá lại và xem tiến bộ (UC-AI-06) | T31 |
| T35 | AI soạn dự thảo kỹ năng và chuẩn, duyệt từng mục (UC-AI-01) | T27, T32 |
| T36 | Nhận xét học sinh theo buổi: thủ công, AI soạn, duyệt, lưu theo buổi (UC-AI-03) | T30, T32 |
| T37 | Nhận xét AI trong báo cáo tháng gửi phụ huynh: tổng hợp từ nhận xét buổi, duyệt, đưa vào PDF, đánh dấu lỗi thời (UC-AI-03) | T36, T11 (#12); tích hợp T21 (#22) khi xong, không chặn |

Thứ tự gợi ý: T27 → T28/T29 → T30 → T31 → T32 → T33 → T34, T35 và T36 → T37.
T37 thay thế T16 (đã chốt 11/11). Issue #17 (T16) cần đóng hoặc viết lại trỏ sang T37. Làm kế hoạch thủ công (T31) trước AI (T33) để chức năng dùng được khi không có AI và để kiểm tra luồng duyệt không phụ thuộc nhà cung cấp.

Ngoài danh sách trên, trước khi bắt đầu cần có T25 (gia hạn, khóa) và T26 (hạn mức AI) vì T32 phụ thuộc vào chúng.

## 8. Nghiệm thu v1

Dùng AC-AI-01…12 trong báo cáo 02/11. Bổ sung:

| Mã | Tiêu chí |
|---|---|
| AC-AI-13 | Request gửi nhà cung cấp không chứa tên học sinh hoặc thông tin liên hệ. |
| AC-AI-14 | Đầu ra sai cấu trúc, học liệu không tồn tại, kỹ năng ngoài ứng viên hoặc vượt quỹ phút bị loại; lượt được hoàn và không có dự thảo. |
| AC-AI-15 | Đổi adapter (mô phỏng ↔ nhà cung cấp thật) không cần sửa nghiệp vụ Learning. |
| AC-AI-16 | Không có quyền AI hoặc hết hạn mức vẫn tạo được kế hoạch thủ công trong kỳ còn hạn. |
| AC-AI-17 | Kỹ năng AI soạn chưa duyệt không xuất hiện trong hồ sơ năng lực, kết quả hay kế hoạch. |
| AC-AI-18 | Nhận xét theo buổi và theo tháng không chứa thông tin ngoài dữ liệu nguồn; thiếu dữ liệu thì ghi "chưa đủ dữ liệu". |
| AC-AI-19 | PDF báo cáo tháng chỉ chứa nhận xét đã được giáo viên duyệt; bản nháp không lọt vào PDF. Hệ thống không tự gửi cho phụ huynh. |
| AC-AI-20 | Sửa kết quả hoặc điểm danh làm nhận xét tương ứng bị đánh dấu lỗi thời, không mất lời giáo viên đã sửa. |
| AC-AI-21 | AI lỗi thì giáo viên vẫn tự viết nhận xét và xuất PDF; phần học phí không bị ảnh hưởng. |

## 9. Điểm còn mở

| # | Vấn đề | Cần quyết trước khi |
|---|---|---|
| 1 | Cách chọn cặp kết quả khi có nhiều hơn hai lần đánh giá (không cộng dồn câu để vượt điều kiện từng lần) | Làm T30 |
| 2 | Số lượt mặc định của hai loại (Kế hoạch, Nhận xét); kế hoạch tuần tính một lượt cho cả lớp hay mỗi học sinh một lượt (loại lượt và đơn vị nhận xét đã chốt, xem mục 1) | Làm T32 |
| 3 | Chọn nhà cung cấp AI và model; điều khoản dữ liệu của nhà cung cấp đó | Nghiệm thu AI thật |
| 4 | Nguồn bộ chuẩn và học liệu Toán lớp 2 cho lớp thí điểm (ai soạn, ai duyệt) | Làm T27, T28 |
| 5 | Tiêu chí "độ khó tương đương" khi đánh giá lại | Làm T34 |
| 6 | AI chấm bài (môn, dạng bài, nhà cung cấp có thị giác) | Bắt đầu T13 |
| 7 | Danh sách từ cấm cụ thể cho nhận xét gửi phụ huynh (BR-AI-20) và có cho giáo viên tự thêm từ không | Làm T37 |
| 8 | Khi có AI chấm bài, thêm hai chỉ số bốn mức vào nguồn dữ liệu của T37 như thế nào | Bắt đầu T13 |

Đã chốt ngày 11/11 (không còn mở): T16 gộp vào T37; nhận xét buổi giáo viên bấm từng học sinh, mỗi lần một lượt; hai loại lượt (Kế hoạch, Nhận xét); giọng văn phụ huynh trung tính, 3–5 câu.

## 10. Trạng thái cập nhật tài liệu và ticket (11/11/2026)

Đã thực hiện:

- **GitHub:** tạo T27–T37 thành issue #31–#41 (T_n = issue #n+4). #34 (T30) và #36 (T32) gắn `needs-info` vì còn điểm phải chốt; các ticket còn lại `ready-for-agent`. Issue #17 (T16) đã đóng, gộp vào #41.
- **GLOSSARY.md:** thêm Kỹ năng, Chuẩn đầu ra, Học liệu, Kết quả theo kỹ năng, Hồ sơ năng lực, Kế hoạch ôn tập, Nhận xét buổi/tháng; cập nhật Lượt AI thành ba loại.
- **ADR-0003:** ghi chú cập nhật ba loại lượt và nhà cung cấp AI sau interface.

Chưa thực hiện:

- **Technical Spec và Ticket Breakdown (file trong `docs/`):** thêm phạm vi bộ B và T27–T37; ghi chú T13–T15, T18 là giai đoạn sau.
- **Issue #1 (spec):** thêm ghi chú phạm vi AI v1.
- **File ticket cục bộ** `docs/tickets/education-keeptrack/`: chưa có file cho T27–T37 (nội dung nằm trong issue GitHub).
- **Ticket hiện có:** các file ticket ghi `Blocked by #13…` lệch với số issue GitHub (ảnh bài nộp là #13, AI chấm là #14); cần rà lại.
