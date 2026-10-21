# Education Keeptrack — Tracer-bullet vertical slices

Nguồn: [Spec v1.1 — Issue #1](https://github.com/truongtoanr0602/EducationKeeptrack/issues/1). Hiện có **26 ticket**: 21 lát cắt ban đầu và 5 lát cắt dịch vụ trả phí đã được người dùng duyệt.

Nguồn UI: [Project Stitch](design/Stitch_Project.md). Thiết kế cũ chưa bao gồm các màn hình đăng ký, chờ duyệt, khôi phục, Admin, kỳ sử dụng và hạn mức; cần bổ sung trong các ticket mới.

Mỗi ticket gồm dữ liệu, API, UI và kiểm thử. Txx là mã nội bộ; thứ tự số không phải thứ tự thực hiện sau khi thêm phụ thuộc. Làm ticket không còn blocker mở; DAG đã được kiểm tra không có vòng.

| Ticket | Tính năng | Blocked by |
| --- | --- | --- |
| T01 | [Đăng nhập và tạo lớp riêng của giáo viên](https://github.com/truongtoanr0602/EducationKeeptrack/issues/2) | Không |
| T02 | [Thêm học sinh và quản lý danh sách theo học](https://github.com/truongtoanr0602/EducationKeeptrack/issues/3) | [#2](https://github.com/truongtoanr0602/EducationKeeptrack/issues/2), [#26](https://github.com/truongtoanr0602/EducationKeeptrack/issues/26) |
| T03 | [Lập lịch và điểm danh buổi học trực quan](https://github.com/truongtoanr0602/EducationKeeptrack/issues/4) | [#3](https://github.com/truongtoanr0602/EducationKeeptrack/issues/3) |
| T04 | [Xem học phí theo buổi thực học và đơn giá hiệu lực](https://github.com/truongtoanr0602/EducationKeeptrack/issues/5) | [#4](https://github.com/truongtoanr0602/EducationKeeptrack/issues/4) |
| T05 | [Tự chấm bài bằng hai chỉ số bốn mức](https://github.com/truongtoanr0602/EducationKeeptrack/issues/6) | [#4](https://github.com/truongtoanr0602/EducationKeeptrack/issues/4) |
| T06 | [Ghi nhận thanh toán một phần và chuyển nợ cũ](https://github.com/truongtoanr0602/EducationKeeptrack/issues/7) | [#5](https://github.com/truongtoanr0602/EducationKeeptrack/issues/5) |
| T07 | [Tạo VietQR với QR tĩnh và thông tin chuyển khoản dự phòng](https://github.com/truongtoanr0602/EducationKeeptrack/issues/8) | [#7](https://github.com/truongtoanr0602/EducationKeeptrack/issues/7) |
| T08 | [Điều chỉnh tăng giảm học phí có lý do](https://github.com/truongtoanr0602/EducationKeeptrack/issues/9) | [#7](https://github.com/truongtoanr0602/EducationKeeptrack/issues/7) |
| T09 | [Sửa hoặc hủy khoản nhận tiền và đối chiếu lịch sử](https://github.com/truongtoanr0602/EducationKeeptrack/issues/10) | [#7](https://github.com/truongtoanr0602/EducationKeeptrack/issues/7) |
| T10 | [Thống kê tiền thực thu và công nợ theo kỳ học](https://github.com/truongtoanr0602/EducationKeeptrack/issues/11) | [#10](https://github.com/truongtoanr0602/EducationKeeptrack/issues/10) |
| T11 | [Soạn nhận xét và xuất báo cáo tháng PDF](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12) | [#6](https://github.com/truongtoanr0602/EducationKeeptrack/issues/6), [#8](https://github.com/truongtoanr0602/EducationKeeptrack/issues/8) |
| T12 | [Chuẩn bị hướng dẫn chấm và tải ảnh bài nộp riêng tư](https://github.com/truongtoanr0602/EducationKeeptrack/issues/13) | [#6](https://github.com/truongtoanr0602/EducationKeeptrack/issues/6) |
| T13 | [AI chấm bài và giáo viên sửa xác nhận hai mức](https://github.com/truongtoanr0602/EducationKeeptrack/issues/14) | [#13](https://github.com/truongtoanr0602/EducationKeeptrack/issues/13), [#27](https://github.com/truongtoanr0602/EducationKeeptrack/issues/27) |
| T14 | [Thử lại AI lỗi hoặc chuyển bài sang tự chấm an toàn](https://github.com/truongtoanr0602/EducationKeeptrack/issues/15) | [#14](https://github.com/truongtoanr0602/EducationKeeptrack/issues/14) |
| T15 | [Sửa hướng dẫn và chủ động chấm lại bài AI](https://github.com/truongtoanr0602/EducationKeeptrack/issues/16) | [#14](https://github.com/truongtoanr0602/EducationKeeptrack/issues/14) |
| T16 | [AI phác thảo nhận xét và lộ trình trên báo cáo tháng](https://github.com/truongtoanr0602/EducationKeeptrack/issues/17) | [#12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12), [#27](https://github.com/truongtoanr0602/EducationKeeptrack/issues/27) |
| T17 | [Xem biểu đồ hai chỉ số và độ đầy đủ dữ liệu](https://github.com/truongtoanr0602/EducationKeeptrack/issues/18) | [#6](https://github.com/truongtoanr0602/EducationKeeptrack/issues/6) |
| T18 | [Xem cảnh báo học tập có bằng chứng và ghi hành động hỗ trợ](https://github.com/truongtoanr0602/EducationKeeptrack/issues/19) | [#18](https://github.com/truongtoanr0602/EducationKeeptrack/issues/18), [#12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12), [#27](https://github.com/truongtoanr0602/EducationKeeptrack/issues/27) |
| T19 | [Ghi tiền trả trước và duyệt bảo lưu nghỉ ốm](https://github.com/truongtoanr0602/EducationKeeptrack/issues/20) | [#7](https://github.com/truongtoanr0602/EducationKeeptrack/issues/7) |
| T20 | [Khấu trừ bảo lưu kỳ sau và thể hiện số dư trên PDF](https://github.com/truongtoanr0602/EducationKeeptrack/issues/21) | [#20](https://github.com/truongtoanr0602/EducationKeeptrack/issues/20), [#12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12) |
| T21 | [Nhận biết PDF lỗi thời và xuất bản cập nhật](https://github.com/truongtoanr0602/EducationKeeptrack/issues/22) | [#12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12) |
| T22 | [Admin đăng nhập hai bước và xem hồ sơ đăng ký](https://github.com/truongtoanr0602/EducationKeeptrack/issues/23) | [#2](https://github.com/truongtoanr0602/EducationKeeptrack/issues/2) |
| T23 | [Giáo viên đăng ký và Admin đối chiếu thanh toán để cấp quyền](https://github.com/truongtoanr0602/EducationKeeptrack/issues/24) | [#23](https://github.com/truongtoanr0602/EducationKeeptrack/issues/23) |
| T24 | [Khôi phục mật khẩu và đổi email có thu hồi phiên](https://github.com/truongtoanr0602/EducationKeeptrack/issues/25) | [#24](https://github.com/truongtoanr0602/EducationKeeptrack/issues/24) |
| T25 | [Gia hạn, khóa và thực thi quyền sử dụng có hạn](https://github.com/truongtoanr0602/EducationKeeptrack/issues/26) | [#24](https://github.com/truongtoanr0602/EducationKeeptrack/issues/24) |
| T26 | [Admin bật tắt AI và giáo viên xem hạn mức theo kỳ](https://github.com/truongtoanr0602/EducationKeeptrack/issues/27) | [#26](https://github.com/truongtoanr0602/EducationKeeptrack/issues/26) |

## Thay đổi phạm vi đã chốt

- Ticket #2 dùng React + TypeScript + Vite, PostgreSQL và Identity/cookie cùng origin.
- #23–#27 bổ sung Admin 2FA, đăng ký/phê duyệt, khôi phục, kỳ/gia hạn/khóa và quyền/hạn mức AI.
- #3 thêm blocker #26 để nghiệp vụ lớp dùng kiểm tra quyền dịch vụ; #14, #17, #19 thêm blocker #27 để nhận AI có kiểm soát.
- #12 cho xuất dữ liệu cũ khi hết hạn, không sửa dữ liệu nghiệp vụ hoặc tải khi bị khóa.
- Tất cả là sub-issue của #1, nhãn ready-for-agent, Blocked by có native links và văn bản. Giá/số lượt, cách tính lượt cảnh báo và chi tiết email/runtime vẫn cần quyết định triển khai; nhãn workflow không thay thế các mục còn mở.

Kiểm thử qua API với dữ liệu thật, mô phỏng AI/email/QR; kiểm tra UI và PDF tương ứng. Cấu hình đã chốt được ghi trong GLOSSARY và các ADR. Không có mã nguồn sản phẩm được triển khai trong bước cập nhật tài liệu này.
