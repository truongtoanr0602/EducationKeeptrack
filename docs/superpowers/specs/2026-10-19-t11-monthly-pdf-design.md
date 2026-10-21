# T11 — Thiết kế báo cáo tháng PDF thủ công

> Cập nhật 21/10/2026: dùng Cloudflare R2 private thay SeaweedFS/MinIO. [Thiết kế R2](../../../doc/cloudflare-r2.md) quy định cấu hình, quyền tải qua backend, kiểm thử và chuyển dữ liệu; thay thế hướng dẫn kho cũ. Ticket #28 chuyển cấu hình và adapter sang R2; nghiệm thu thực tế cần bucket test và credentials. CI và chuyển dữ liệu cũ lần lượt thuộc #29/#30.

Ngày: 19/10/2026. Trạng thái: đã được người dùng duyệt trong hội thoại;
ghi nhận duyệt ngày 20/10/2026.

Nguồn yêu cầu: [issue #12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12),
`docs/tickets/education-keeptrack/11-manual-monthly-pdf.md` và hướng thiết kế đã
được người dùng duyệt trong hội thoại. T05 và T07 đã hoàn thành.
Baseline trước công việc T11: `256be04b06f6f547cd4568a1908d6cc0cc811c8d`.

## 1. Mục đích và phạm vi đã thống nhất

Giáo viên chọn một lớp, một học sinh và một tháng; soạn nhận xét/lộ trình thủ
công; xem trước; xuất và tải PDF để tự chia sẻ. Báo cáo phải đủ căn cứ học tập,
chuyên cần, học phí và thanh toán của đúng học sinh, giữ tiếng Việt và đọc được
khi nhiều trang.

T11 bổ sung module Reporting, lưu bản nháp và metadata tại PostgreSQL, lưu PDF
riêng tư trên Cloudflare R2 qua S3 API. Có API, UI, cấu hình chạy local/Compose và kiểm thử đầy đủ.
Không gọi AI, không tự gửi phụ huynh, không xuất Word/DOCX, không tự quyết định
chính sách bảo lưu hoặc khóa/mở lại báo cáo đã chốt. T21 sẽ bổ sung quy trình
nhận biết báo cáo cũ và đối chiếu các bản cập nhật bằng sự kiện.

## 2. Ranh giới module

Tuân thủ ADR 0001–0004: mỗi module chỉ đọc/ghi DbContext của mình. Reporting
không tham chiếu Infrastructure hoặc bảng của Classroom, Learning, Billing.
Các DTO/hợp đồng đọc báo cáo đặt tại `Contracts`, được module sở hữu thực hiện.

| Module | Hợp đồng cần cung cấp |
| --- | --- |
| Classroom | Xác minh lớp/học sinh thuộc giáo viên; tên lớp/môn, hồ sơ/liên hệ, buổi học và điểm danh trong tháng; dấu phiên bản nguồn. |
| Learning | Các bài đánh giá trong tháng và kết quả đã xác nhận của đúng học sinh; dấu phiên bản bao gồm danh sách bài và các phiên bản liên quan. |
| Billing | Phí từng buổi, điều chỉnh, công nợ trước tháng/của tháng, phân bổ, tiền chưa phân bổ/giữ đối chiếu, thông tin người nhận và khả năng tạo QR cho đúng số tiền báo cáo; trạng thái và phiên bản. |
| Reporting | Bản nháp, tùy chỉnh hiển thị, ảnh chụp preview, bản xuất, trạng thái xử lý và kiểm soát tải file. |

Adapter PDF và S3 thuộc Infrastructure của Reporting. Các hợp đồng có kiểm tra
sở hữu phía server; owner lấy từ phiên xác thực, không lấy từ request body.
Tham chiếu liên module chỉ là ID, không có khóa ngoại xuyên schema.

## 3. Nội dung và quy tắc nghiệp vụ

### Hồ sơ và nhận xét

Bản nháp có khóa duy nhất theo giáo viên/lớp/học sinh/tháng và concurrency version.
Các trường sửa được: tên hiển thị học sinh, tên/thông tin liên hệ hiển thị của
phụ huynh, nhận xét, lộ trình và lựa chọn emoji tối giản từ danh sách cho phép.
Thông tin bỏ trống dùng mặc định từ hồ sơ nguồn; override chỉ tác động báo cáo.
Nhập liệu là văn bản thuần, không cho HTML hoặc URL ảnh/font do người dùng nhập.

Giới hạn đề xuất: tên 160 ký tự; thông tin liên hệ hiển thị 500 ký tự; nhận xét
và lộ trình mỗi trường 10.000 ký tự. Các giới hạn được kiểm tra tại API và UI.
Nhận xét/lộ trình chưa nhập được thể hiện là chưa có, không tự sinh nội dung.

### Chuyên cần và đánh giá

Chuyên cần lấy các buổi đã diễn ra trong tháng và điểm danh có mặt/vắng/chưa
điểm danh; giữ nhãn nghỉ có phép. Buổi dự kiến/hủy được phân biệt và không tính
thành buổi đã học. Dữ liệu thiếu được hiển thị, không suy ra là vắng hoặc bằng 0.

Mỗi bài đánh giá lấy revision có action `confirmed` gần nhất của học sinh.
Bản sửa chưa xác nhận không ghi đè kết quả chính thức cũ. Chưa có revision xác
nhận thì ghi rõ chưa có kết quả xác nhận; không lấy hai mức của bản nháp.
Các trạng thái không làm bài/không có câu đúng biểu diễn 0 theo mô hình Learning;
không giao bài/chưa đánh giá giữ nhãn và giá trị trống tương ứng.

Không tự tính một điểm chung hoặc đổi hai mức sang thang điểm khác. Báo cáo
hiển thị từng bài và ngày đánh giá để phụ huynh đối chiếu.

### Tài chính và thanh toán

PDF hiển thị căn cứ phí từng buổi trong tháng, điều chỉnh có lý do, phí sau
điều chỉnh, phân bổ thanh toán, dư nợ trước tháng và dư nợ tháng. Giữ riêng tiền
chưa phân bổ và tiền giữ để đối chiếu; không coi các khoản này là bảo lưu đã duyệt.

Tổng cần thu của báo cáo = dư nợ các kỳ trước tháng + dư nợ của tháng báo cáo.
Không cộng nghĩa vụ của các tháng sau vào tổng này. Số liệu là đối chiếu theo
dữ liệu đang lưu tại thời điểm preview, không tự tái dựng sổ đã khóa ở cuối tháng.
Ngày thực nhận tiền vẫn được ghi riêng với kỳ được phân bổ.

UI/PDF không cho sửa trực tiếp các số tiền. UI hướng về màn hình phí/điểm danh/
nhận tiền tương ứng để sửa nguồn, sau đó yêu cầu tạo preview mới.

Billing tạo thông tin thanh toán cho chính tổng cần thu và phiên bản báo cáo;
không dùng nguyên số tiền của endpoint QR hiện tại nếu endpoint đó cộng cả kỳ sau.
Ưu tiên QR động, sau đó QR tĩnh, rồi thông tin chuyển khoản bằng chữ. Nếu không
có người nhận, xuất được với cảnh báo chưa cấu hình và không tự bịa thông tin.
Tổng cần thu bằng 0 thì không hiển thị yêu cầu chuyển khoản hoặc QR yêu cầu trả tiền.
Thiếu QR không chặn xuất; nguồn tài chính chưa đồng bộ hoặc thiếu đơn giá thì
chặn xuất và hướng dẫn làm mới/bổ sung dữ liệu.

## 4. Preview, phiên bản và tính nhất quán

Một preview chứa payload bất biến của đúng học sinh, version bản nháp và dấu
phiên bản Classroom, Learning, Billing, người nhận thanh toán. Dấu Classroom
bao gồm hồ sơ, danh sách buổi và điểm danh liên quan; dấu Learning bao gồm bài
mới và revision; dấu Billing dùng ledger version cùng trạng thái projection;
người nhận có recipient version hoặc dấu chưa cấu hình.

Coordinator đọc snapshot qua các hợp đồng, rồi kiểm tra lại dấu phiên bản để
đảm bảo các phần có cùng một khoảng nguồn ổn định. Đồng thời đối chiếu Billing
với phiên bản buổi/điểm danh Classroom. Nếu nguồn thay hoặc projection pending,
không trả một preview được coi là sẵn sàng xuất. Phản hồi 409 yêu cầu làm mới;
không giữ request chờ nguồn vô thời hạn.

Server lưu snapshot kỹ thuật với preview ID không đoán được và thời hạn cấu
hình, mặc định 15 phút. Snapshot kỹ thuật không sửa bản nháp nghiệp vụ. Client
không gửi lại HTML hoặc số tiền để server tin dùng khi xuất.

Khi xuất: kiểm tra preview, bản nháp và nguồn; dựng HTML/QR/PDF từ snapshot;
kiểm tra lại phiên bản và quyền đọc trước khi công bố bản xuất. Nguồn thay
trong khi dựng hoặc upload thì không trả bản thành công; yêu cầu preview mới.
Thay đổi sau điểm kiểm tra nhất quán tạo một phiên bản nguồn mới, không sửa
file PDF đã xuất. Bản xuất ghi rõ dấu nguồn và thời điểm snapshot/xuất.

## 5. Dựng HTML/PDF

Chọn một template HTML phía server cho cả bản xem trước và đầu vào PDF.
UI dùng iframe sandbox để xem HTML đúng bản sẽ dựng; các trường được encode.
Template không có script và chỉ dùng tài nguyên đóng gói hoặc ảnh QR đã xác minh.

Adapter PDF dùng Chromium qua Playwright .NET với CSS in A4. Chờ font/ảnh tải
xong, dùng font đóng gói có tiếng Việt và glyph cho tập emoji cho phép. Có ngắt
trang, lặp tiêu đề bảng và chân trang; tránh cắt mất dòng/chữ hoặc tràn trang.
Không phụ thuộc font cài riêng trên máy lập trình viên hoặc tải CDN lúc xuất.

Renderer không mở các URL tùy ý; chặn request mạng ngoài, chỉ nhận HTML từ
template và tài nguyên nội bộ đã kiểm soát. Giới hạn số lượt dựng đồng thời và
timeout cấu hình để quản lý tài nguyên; lỗi dựng trả thông báo có thể thử lại.
Dependency/runtime và font kèm giấy phép sẽ được chốt trong kế hoạch triển khai.

## 6. Lưu trữ và chống lặp

Schema `reporting` có DbContext/migrations riêng. Các thực thể chính:

- `MonthlyReportDraft`: owner/class/student/month, display overrides, nhận xét,
  lộ trình, emoji, version, thời điểm lưu.
- `ReportPreview`: snapshot bất biến, dấu nguồn, draft version và expiry.
- `ReportExport`: ID, owner/class/student/month, preview ID, idempotency key,
  payload fingerprint, trạng thái pending/ready/failed, object key, SHA-256,
  byte length, content type, renderer/template version và thời điểm xuất.

Idempotency key có unique index trong phạm vi giáo viên. Gửi lại cùng lệnh
trả cùng bản xuất; dùng key cho payload khác trả 409. Sau khi đã ready, retry
không dựng lại hoặc thay bytes của bản cũ, kể cả khi nguồn sau đó thay đổi.
Lệnh pending có lease để không cho hai request cùng dựng và có thể phục hồi
sau dừng tiến trình; key thất bại có thể retry cùng payload an toàn.

PDF được upload dưới key không đoán được trong bucket riêng tư. Chỉ đánh dấu
ready sau khi upload hoàn tất và metadata đã lưu. Upload lỗi hoặc lưu metadata
lỗi không tạo đường tải thành công; retry đối chiếu object/checksum hoặc ghi lại
đúng key theo trạng thái pending. Object mồ côi do lỗi vẫn private và được xử lý
theo metadata khi retry; không xóa hàng loạt dữ liệu ngoài phạm vi bản xuất.

Cấu hình endpoint/bucket/region/access key/secret ở môi trường, không commit
credentials. Compose kết nối R2 ngoài qua HTTPS, không có volume object storage; kiểm thử dùng bucket R2 test riêng và
prefix ngẫu nhiên mỗi fixture trong bucket test cấp trước. Không tạo/xóa bucket; chỉ dọn object thuộc prefix. Bucket là cấu hình chung cho mọi PDF của deployment, đổi bucket cần chuyển đầy đủ object giữ nguyên key; không thay schema trong #28.

## 7. Quyền và đường tải

| Thao tác | Điều kiện |
| --- | --- |
| Đọc bản nháp, preview, danh sách bản xuất | Teacher, đúng owner, `CanRead`. |
| Lưu bản nháp/override | Teacher, đúng owner, `CanWrite`, expected draft version. |
| Tạo preview/xuất PDF từ nội dung đã lưu | Teacher, đúng owner, `CanRead`; không lưu ngầm bản nháp mới. |
| Cấp link/tải PDF | Phiên hợp lệ, đúng owner, `CanRead`, link chưa hết hạn, export ready. |

Các POST/PUT vẫn kiểm tra CSRF. Giáo viên hết hạn không sửa được nội dung hoặc
nguồn; có thể tạo snapshot kỹ thuật và metadata xuất từ dữ liệu đã có. Kiểm tra
này phải tách khỏi quy tắc chặn mọi POST khi `CanWrite == false` của module khác.
T11 không thêm bất kỳ đường gọi AI nào.

Download URL là đường API có token được bảo vệ, gắn owner/export ID và expiry,
mặc định 5 phút, dùng TimeProvider để kiểm thử. Token không thay thế phiên đăng
nhập. Mỗi lần tải kiểm tra lại tài khoản, phiên và sở hữu; token của giáo viên
khác hoặc tài khoản vừa bị khóa không tải được. API stream file private từ S3,
không redirect sang URL S3 dùng độc lập với trạng thái tài khoản.

Phản hồi dùng `application/pdf`, attachment filename an toàn và cache private/
no-store. Không public bucket, không đưa object credentials vào URL, không log
download token hoặc nội dung học sinh. Các bản PDF cũ giữ bytes bất biến.

## 8. API và UI đề xuất

Tiền tố API: `/api/reporting/classes/{classId}/students/{studentId}/months/{month}`.

| Endpoint | Chức năng |
| --- | --- |
| `GET /draft` | Đọc bản nháp đã lưu hoặc giá trị mặc định; không tạo bản nghiệp vụ. |
| `PUT /draft` | Lưu trường cho phép với expected version; 409 khi tranh chấp. |
| `POST /previews` | Tạo snapshot kỹ thuật/HTML từ dữ liệu đã lưu, trả ID và dấu nguồn. |
| `GET /previews/{id}/html` | Đọc HTML của preview còn hạn, đúng phạm vi và owner. |
| `POST /exports` | Nhận preview ID + draft version + idempotency key, dựng/lưu PDF. |
| `GET /exports` | Liệt kê metadata bản đã xuất của đúng học sinh/tháng. |
| `POST /exports/{id}/download-link` | Cấp token tải có thời hạn cho bản ready. |
| `GET /exports/{id}/file?token=...` | Kiểm tra quyền/token và stream PDF. |

UI có vùng Báo cáo tháng trong workspace lớp: chọn học sinh/tháng, sửa bản nháp,
lưu, xem trước, xuất và tải file. Hiển thị các trường chỉ đọc về học tập/tài
chính, cảnh báo QR/dữ liệu thiếu, trạng thái đang lưu/đang xuất và danh sách
bản đã xuất. Sau lỗi validation/conflict, giữ nội dung nhập; không xuất bản
chỉnh sửa chưa lưu. Khi kết quả xuất chưa rõ, retry đúng idempotency key.
Hết hạn dịch vụ: trường soạn thảo chỉ đọc, nút xuất/tải vẫn dùng được với dữ
liệu đã lưu. UI desktop/mobile không tràn ngang; preview cho phép cuộn bên trong.

## 9. Kiểm thử và tiêu chí hoàn tất

Tiếp tục dùng hai seam đã duyệt: API hệ thống với PostgreSQL thật và browser
desktop/mobile. T11 bổ sung dựng PDF thật, kho S3 thật và kiểm tra artifact.

- API: round-trip bản nháp, conflict version, giới hạn trường và encode HTML;
  hai giáo viên, lớp/học sinh khác phạm vi, Admin/anonymous, CSRF.
- Nguồn: kết quả xác nhận so với sửa nháp, trạng thái 0/thiếu, chuyên cần,
  điều chỉnh/phân bổ/nợ cũ và không cộng kỳ sau; chỉ một học sinh đi vào PDF.
- Nhất quán: nguồn/bản nháp/người nhận đổi trong khi tạo preview, render hoặc
  upload; pending projection/thiếu giá; không xuất thành công với dữ liệu trộn.
- Quyền: hết hạn vẫn xuất/tải nhưng không sửa; khóa hoặc thu hồi phiên trước
  tải bị chặn; token hết hạn/khác owner/khác export/giả mạo đều bị từ chối.
- Độ bền: gửi lặp và đồng thời, upload/lưu metadata lỗi, khôi phục pending;
  không có hai bản thành công cho cùng một lệnh.
- PDF: trích xuất và kiểm tra tên, nội dung xác nhận, số tiền, ngày và cảnh báo;
  dựng mẫu tiếng Việt/emoji/nhiều trang, rasterize và xem để kiểm tra glyph,
  ngắt trang và nội dung QR/fallback. Không chỉ so sánh byte PDF.
- UI: lưu/xem trước/xuất/download thực, bản nháp được giữ sau lỗi, hết hạn chỉ
  đọc, cảnh báo làm mới và QR thiếu, bố cục desktop/mobile.

Chạy typecheck thường xuyên, từng test khi thực hiện từng lát cắt, toàn bộ API/
browser suite ở cuối; review Standards/Spec theo skill `code-review`, ghi bằng
chứng trong `docs/verification/T11.md`, rồi commit phần triển khai vào nhánh hiện tại.

## 10. Tài liệu kỹ thuật đã kiểm tra

- [Playwright .NET Page / PdfAsync](https://playwright.dev/dotnet/docs/api/class-page#page-pdf):
  dựng PDF từ trang và CSS print; dùng làm cơ sở cho adapter Chromium.
- [Amazon S3 presigned URLs](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html):
  URL có thời hạn cấp quyền truy cập object. T11 chọn proxy API xác thực để việc
  khóa tài khoản được kiểm tra khi tải, cùng với thời hạn link ở tầng ứng dụng.

Tài liệu này là bản thiết kế để duyệt. Kế hoạch triển khai chi tiết được viết
sau khi người dùng duyệt bản này, theo skill `writing-plans`.
