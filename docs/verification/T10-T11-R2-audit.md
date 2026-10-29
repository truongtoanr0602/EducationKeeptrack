# Rà soát nghiệm thu T10, T11 và R2 — 27/10/2026

Phạm vi: đối chiếu mã nguồn hiện tại với [T10 #11](https://github.com/truongtoanr0602/EducationKeeptrack/issues/11), [T11 #12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12) và [R2 #28](https://github.com/truongtoanr0602/EducationKeeptrack/issues/28). Baseline: `2fefb5b`. Đây là nghiệm thu local, không phải kết quả Jenkins hay chuyển dữ liệu production.

## Kết quả kiểm tra mới

- `dotnet build EducationKeeptrack.slnx -c Release --nologo`: đạt, 0 warning, 0 error.
- Trong `ui`: `npm run typecheck` và `npm run build`: đạt.
- `npx playwright test e2e/cashflow-statistics.spec.ts e2e/monthly-reports.spec.ts --workers=1`: 8 đạt, trên desktop và mobile, với backend thật và cấu hình R2 test.
- `dotnet test tests/ApiTests -c Release --no-build --logger 'console;verbosity=minimal'`: 141 đạt, 0 lỗi, 0 bỏ qua, thời gian 3 phút 56 giây, dùng PostgreSQL thật và cấu hình bucket R2 test có sẵn.
- Đã dựng ảnh và xem cả 4 trang của `.local/t11-artifacts/long-report.pdf` được sinh trong lần chạy mới: tiếng Việt và biểu tượng sách hiển thị, nội dung nhận xét/lộ trình dài nối trang đọc được, không thấy chữ bị cắt hoặc chồng lấn. Trang cuối thưa nội dung, có thể tối ưu bố cục về sau.

Lần chạy đầu dùng cấu hình PostgreSQL test tại cổng 55432 khi server test chưa chạy. Build và test chạy chồng nhau còn gây khóa DLL trên Windows. Đã dừng test của lần chạy này, khởi động PostgreSQL riêng trong `.local/pgdata`, build lại thành công rồi mới chạy API tests. Không dùng kết quả lần chạy đầu để nghiệm thu.

## Đối chiếu phạm vi

| Phần | Bằng chứng trong repository | Giới hạn nghiệm thu |
| --- | --- | --- |
| T10: thực thu và công nợ | `CashflowStatisticsTests.cs`, `CashflowStatistics.tsx`, browser test đối chiếu tổng/chi tiết, sửa giao dịch và tải lại | Chưa thể nghiệm thu kết hợp khấu trừ bảo lưu khi T19/T20 chưa có. |
| T11: bản nháp và preview | `ReportingTests.cs`, `ReportPreviewTests.cs`, browser test sửa/lưu nội dung, cô lập học sinh, xung đột nguồn | Trạng thái hết hạn trong browser được mô phỏng ở response `/auth/me`; thực thi quyền backend cần dựa vào API tests. |
| T11: PDF và tải riêng tư | `ReportExportTests.cs`, `ReportDownloadTests.cs`, browser tải bytes có đầu `%PDF-`, xem trực quan mẫu 4 trang | Mẫu đã xem dùng biểu tượng sách; không khẳng định đã xem mọi emoji hoặc mọi tổ hợp nội dung. |
| #28: R2 | Adapter S3, cấu hình R2 trong `.env.example`, fixture bucket cấp trước và prefix ngẫu nhiên; Compose không còn service kho S3 | Thành công local không chứng minh Jenkins đã cấp secrets hoặc chạy cùng luồng. |

Các API tests hiện có kiểm tra SHA-256/kích thước PDF, tải trái owner, token hết hạn, khóa/thu hồi phiên, retry khi upload lỗi, dữ liệu nguồn đổi và metadata không được công bố ready sai. Fixture chỉ dọn object thuộc prefix của lần chạy, không tạo/xóa bucket.

## Công việc tiếp theo

1. [#29](https://github.com/truongtoanr0602/EducationKeeptrack/issues/29): Jenkins hiện chưa bind cấu hình R2 test; Chromium mới được cài ở bước browser, sau API tests. Cần chuẩn bị renderer trước API tests, cấp cấu hình cho cả API/browser và lưu bằng chứng bắt buộc khi nghiệm thu.
2. [#30](https://github.com/truongtoanr0602/EducationKeeptrack/issues/30): kiểm kê PDF cũ, diễn tập chuyển dữ liệu, đối chiếu checksum, backup/restore và rollback. Chưa thực hiện chuyển dữ liệu trong lượt rà soát này.
3. Giữ mẫu PDF nhiều trang làm căn cứ kiểm tra bố cục; mở rộng mẫu khi thay font, template hoặc tập emoji.
4. Đối chiếu kết quả cuối với từng tiêu chí trước khi cập nhật/đóng issue. Không coi trạng thái issue mở là bằng chứng chức năng chưa có.

Log local: `.local/current-audit-build.log`, `.local/current-audit-api.log`, `.local/current-audit-browser.log`. Credentials không được đưa vào biên bản.
