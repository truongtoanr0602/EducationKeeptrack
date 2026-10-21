# Thiết kế lưu trữ Cloudflare R2

Cập nhật 21/10/2026 theo quyết định dùng R2 thay SeaweedFS/MinIO. Đây là kiến trúc đích cho đặc tả tổng, thiết kế hệ thống và T11/T12; cấu hình và adapter được chuyển đổi trong #28; CI và chuyển dữ liệu cũ thuộc #29/#30. Nghiệm thu R2 cần credentials và kết quả kiểm thử thật.

## Kiến trúc và cấu hình

Reporting sở hữu metadata/PDF; Learning sở hữu metadata/ảnh bài nộp. Adapter S3 thuộc Infrastructure, giữ hợp đồng IReportObjectStore và S3ReportStore. PostgreSQL lưu object key, content type, kích thước và SHA-256; bucket là cấu hình chung của deployment, không lưu theo từng bản xuất. Đổi bucket cần chuyển toàn bộ object đã tham chiếu, giữ nguyên key trước khi đổi cấu hình để các bản xuất cũ vẫn tải được. Không lưu URL tải tạm thời hoặc credentials. #28 không thay schema.

R2 nằm ngoài Docker Compose, kết nối HTTPS qua `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`, signing region `auto`, ForcePathStyle=true. Reporting dùng các biến `Reporting__Endpoint`, `Reporting__Region`, `Reporting__Bucket`, `Reporting__AccessKey`, `Reporting__SecretKey`, `Reporting__ForcePathStyle`. Cấp bucket trước, không yêu cầu ứng dụng tạo bucket. Kiểm chứng checksum/signing với AWSSDK.S3 hiện tại bằng Put/Get/Head/Delete thực tế.

Bucket private, tắt public r2.dev và public custom domain. Tách bucket và credentials dev/test/production; secrets chỉ ở backend/Jenkins và giới hạn quyền theo bucket. Browser upload/download qua API cùng origin, không cần CORS trên R2 cho luồng mặc định. Key dùng định danh nội bộ và phiên bản, không chứa tên học sinh/email hoặc tên file người dùng nhập.

## Quyền truy cập và xử lý lỗi

Backend kiểm tra phiên, khóa tài khoản, CanRead/CanWrite và owner trước mỗi thao tác. Token tải ứng dụng hạn 5 phút vẫn yêu cầu đăng nhập; backend stream file với Cache-Control: no-store. Không cấp presigned URL R2 trong luồng mặc định vì URL còn hạn vẫn dùng được sau khi tài khoản bị khóa. Không log secrets, token hoặc nội dung ảnh.

Ghi object thành công rồi mới đánh dấu metadata ready. Giữ key riêng cho mỗi attempt xuất PDF, không ghi đè object đã ready. Retry có giới hạn; object mồ côi vẫn private và được đối chiếu theo metadata. Không trả link thành công khi upload lỗi, không fallback file public. R2 lỗi chỉ làm gián đoạn thao tác liên quan; module khác vẫn hoạt động. API kiểm tra kích thước và định dạng ảnh trước upload.

## Vận hành, kiểm thử và chuyển dữ liệu

Local kết nối bucket R2 dev qua Internet. Khi triển khai chuyển đổi, bỏ service s3, depends_on tương ứng và volume report-objects khỏi Compose; chỉ xóa dữ liệu cũ sau xác minh. Theo dõi lỗi, độ trễ, dung lượng và số thao tác. Không tự đặt lifecycle xóa ảnh khi retention nghiệp vụ chưa chốt. Backup object độc lập cùng metadata PostgreSQL và diễn tập khôi phục; dịch vụ lưu trữ không thay thế backup.

CI dùng bucket R2 test được cấp trước, credentials từ Jenkins binding và prefix ngẫu nhiên theo run. Fixture nhận TEST_S3_ENDPOINT, TEST_S3_ACCESS_KEY, TEST_S3_SECRET_KEY, TEST_S3_BUCKET; region auto. Chỉ dọn prefix do run tạo, không tạo/xóa bucket, không dùng production. Thiếu credentials phải báo chưa xác minh tích hợp, không âm thầm bỏ qua kiểm thử. Nghiệm thu PDF thật và ảnh (cùng T12), đối chiếu bytes, owner khác, token hết hạn, tài khoản khóa, lỗi mạng/quyền, retry và không có metadata ready khi upload thất bại.

Chuyển dữ liệu: kiểm kê metadata/object cũ; chuẩn bị bucket private đích; tạm dừng ghi ảnh/PDF; sao chép giữ key/content type; đối chiếu số lượng, kích thước và SHA-256 tự tính, không coi ETag luôn là MD5. Chuyển cấu hình rồi smoke test đọc/ghi qua API và kiểm tra trái quyền trước mở ghi. Giữ kho cũ chỉ đọc trong thời gian xác minh. Nếu R2 đã nhận ghi mới, đồng bộ chênh lệch trước rollback. Chỉ ngừng/xóa kho cũ sau backup và khôi phục thử thành công.

Tài liệu chính thức: [S3 compatibility](https://developers.cloudflare.com/r2/api/s3/api/), [S3 setup](https://developers.cloudflare.com/r2/get-started/s3/), [presigned URLs](https://developers.cloudflare.com/r2/api/s3/presigned-urls/).
