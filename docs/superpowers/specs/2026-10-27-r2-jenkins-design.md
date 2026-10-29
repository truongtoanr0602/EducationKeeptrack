# Nghiệm thu PDF/R2 bằng Jenkins — #29

## Mục tiêu và phạm vi

Thực hiện [#29](https://github.com/truongtoanr0602/EducationKeeptrack/issues/29): một lần CI thành công phải chứng minh xuất/tải PDF thật qua API và browser trên bucket R2 test được cấp trước. Thiếu secrets, kiểm thử bị bỏ qua, cleanup lỗi hoặc thiếu bằng chứng bắt buộc phải làm pipeline thất bại. Không chuyển PDF cũ (#30), không dùng production và không tạo/xóa bucket.

## Hiện trạng

Jenkins chưa cấp cấu hình R2 cho API/backend browser, cài Chromium sau API tests và chỉ lưu TRX với `allowEmptyArchive: true`. API fixture dùng prefix ngẫu nhiên nhưng chưa nằm trong prefix chung của job/run; browser backend ghi key thông thường nên chưa thể cleanup riêng từng run.

## Phương án đề xuất

Giữ adapter S3 hiện có, thêm prefix tùy chọn, mặc định rỗng để giữ đường dẫn các bản xuất hiện hữu. Jenkins cấp một prefix duy nhất cho mỗi run; API fixture và browser backend ghi trong các nhánh riêng. Công cụ kiểm thử chịu trách nhiệm xác minh cấu hình, dọn object và tạo bằng chứng an toàn, không đưa thao tác quản trị bucket vào ứng dụng.

Chỉ thêm credentials vào Jenkins sẽ chưa giải quyết cleanup browser. Tạo bucket riêng mỗi run trái tiêu chí bucket cấp trước. Vì vậy đề xuất dùng prefix chung, cùng một bucket test đã được cấp trước.

## Thay đổi dự kiến

1. Bind endpoint, bucket, access key và secret key test bằng Jenkins credentials; tắt shell tracing trước khi xử lý secrets. Gán cùng cấu hình cho API fixture và backend browser.
2. Tạo prefix `ekt-ci/<run-id>/` ngẫu nhiên, giữ trong tệp trạng thái riêng của run. API fixture thêm prefix con ngẫu nhiên; browser backend dùng nhánh riêng. Kiểm tra prefix hợp lệ và từ chối cleanup prefix rỗng, prefix cha hoặc key nằm ngoài phạm vi sở hữu.
3. Chuẩn bị Chromium và các thư viện hệ thống cho renderer .NET trước API tests. Cài browser Playwright UI phù hợp với phiên bản package của UI.
4. Chạy API tests hiện có, bao gồm checksum/nội dung, owner khác, token hết hạn, khóa/thu hồi phiên, kho lỗi/quyền sai và retry. Browser kiểm tra luồng sửa/lưu, preview, xuất/tải PDF thật và retry trên desktop/mobile. Bổ sung đối chiếu bytes/checksum với metadata bản xuất cho browser.
5. Dừng backend browser trước cleanup. Trong `post/always`, cleanup tất cả object thuộc prefix run, kể cả API fixture chưa kịp dispose khi test thất bại. Cleanup có timeout và báo lỗi; không được che lỗi kiểm thử ban đầu.
6. Lưu báo cáo TRX, báo cáo browser và bản tổng hợp chỉ gồm kết quả/đếm/checksum cần thiết. Không lưu PDF, request URL có token, trace, cookies, secrets hoặc backend logs chưa lọc. Thiếu artifact bắt buộc không được báo nghiệm thu thành công; kiểm tra báo cáo API không có test bị bỏ qua.
7. Viết hướng dẫn Jenkins credentials, yêu cầu agent Linux và lệnh chạy local dùng cùng công cụ; phân biệt kiểm chứng local với một build Jenkins thật.

## Kiểm chứng

- Kiểm thử prefix hợp lệ/không hợp lệ và ranh giới cleanup, upload/download với prefix giữ đúng checksum, cấu hình prefix mặc định giữ nguyên key.
- Chạy API/browser với R2 test thật; cố ý tạo object trong nhánh khác và xác minh cleanup không chạm tới object đó. Dọn riêng dữ liệu đối chứng do bài test sở hữu.
- Kiểm tra thiếu secrets/artifact và test bị bỏ qua làm bước nghiệm thu thất bại; bằng chứng xuất ra không chứa token/secrets.
- Kiểm tra cú pháp script và cấu trúc Jenkins có thể thực hiện local. Chỉ xác nhận nghiệm thu Jenkins sau khi có kết quả từ Jenkins thật; hiện chưa có kết nối Jenkins được xác định trong phiên này.

## Quyết định triển khai được đề xuất

Giữ logic lưu trữ trong Infrastructure; công cụ CI chỉ quản lý prefix test và báo cáo. Không đổi mô hình nghiệp vụ hoặc metadata PDF. Prefix deployment phải ổn định nếu dùng ngoài CI; đổi prefix sẽ thay đường dẫn vật lý tương tự đổi bucket và cần chuyển object trước. Môi trường production giữ prefix mặc định rỗng trong phạm vi #29.
