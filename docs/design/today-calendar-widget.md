# Thiết kế widget lịch tại tab Hôm nay

Trạng thái: **bản thiết kế đã duyệt qua phỏng vấn, chưa làm mã**. Bản mẫu tương tác: `docs/design/today-calendar-prototype.html` (dữ liệu giả, mở trực tiếp bằng trình duyệt).

## 1. Mục tiêu

Giáo viên (3–4 lớp mỗi tuần, tối các ngày thường, cuối tuần có cả sáng và tối) cần, ngay ở tab Hôm nay:

- (a) nhìn trước tuần hoặc tháng có những buổi nào,
- (b) nhảy tới một ngày để điểm danh hoặc xem,
- (c) phát hiện chỗ thiếu: buổi chưa điểm danh, buổi quá hạn, tuần chưa tạo buổi, trùng giờ.

Ngoài phạm vi: kéo thả dời buổi, xuất sang Google Calendar, đổi tên/xóa/khôi phục lớp (đã bỏ).

## 2. Quyết định đã chốt

| # | Quyết định |
|---|---|
| Q1 | Bản đầu chỉ **đọc** + nhảy ngày; điểm danh dùng luồng hiện có. |
| Q2 | **Dải tuần** mặc định, mở thành **lưới tháng** bằng tay nắm/nút "Tháng". Danh sách buổi trong ngày giữ như hiện nay. Giữ ngày đang chọn khi đổi chế độ. |
| Q3 | Hiện cả **"Chưa tạo buổi"** (nét đứt, suy ra từ lịch tuần) + nút tạo hàng loạt. |
| Q4 | Quy mô: ≤ ~10 lớp, 1–4 buổi/ngày. Ô tháng hiện số buổi + chấm trạng thái, chưa lọc theo lớp. |
| Q5/Q22 | Giữ 4 tab cấp cao. Lớp gom 7 mục → 5: Học sinh · Lịch · Học phí · Học tập · Báo cáo. |
| Q6 | Tự dựng bằng CSS Grid, nạp lười, không thêm thư viện. |
| Q7 | Dấu **Quá hạn** cho buổi đã qua mà còn Dự kiến + dòng cảnh báo; chỉ cảnh báo hiển thị. |
| Q8 | Màu theo lớp (tính từ mã lớp), trạng thái bằng **ký hiệu và chữ**, có chú thích. |
| Q9 | Endpoint đọc `GET /api/today/range?from&to` (≤ 42 ngày), tính chỗ "Chưa tạo buổi" ở server. |
| Q10 | Tạo hàng loạt có **xem trước + xác nhận**, báo kết quả theo lớp. |
| Q11 | Dải tuần thay ba nút; giữ tên truy cập "Hôm trước/Hôm nay/Hôm sau"; vuốt, bàn phím, nhãn từng ngày. |
| Q12 | Desktop (≥ ~1100px): 7 cột tuần bên trái, chi tiết ngày bên phải. |
| Q20–24 | ADR 0005 + glossary trước; mục tiêu hiệu năng; 5 lát cắt; kết quả là tài liệu + bản mẫu. |

## 3. Bố cục

Điện thoại:

```text
┌ Hôm nay ───────────────────────┐
│ ‹  Tuần 23–29/11/2026   ›  [Hôm nay] [Tháng ▾]
│ T2  T3  T4  T5  T6  T7  CN
│ 23  24  25  26  27  28  29
│  ●   ●●  ○   ●   !   ●●  ●
│ ──── tay nắm kéo mở tháng ────
│ ⚠ 2 buổi chưa điểm danh · 1 quá hạn  [Tới ngày cũ nhất]
│ Thứ Sáu 27/11/2026
│ ┌ 18:00–19:30  Toán 7A   Quá hạn ┐   (thẻ buổi hiện có, bấm để điểm danh)
│ └ ...                            ┘
│ ┌ 19:45  Lý 8B   Chưa tạo buổi ┐ [Tạo buổi]
│ Chú thích: ● đã điểm danh ○ chưa tạo ! thiếu ⊘ hủy "bù" học bù
└────────────────────────────────┘
```

Desktop:

```text
┌ T2 ┬ T3 ┬ T4 ┬ T5 ┬ T6 ┬ T7 ┬ CN ┐ ┌ Chi tiết ngày đang chọn ───┐
│thẻ │thẻ │ ...                    │ │ danh sách buổi + điểm danh │
└────┴────┴────────────────────────┘ └────────────────────────────┘
```

## 4. Ký hiệu

| Ký hiệu | Nghĩa | Ghi chú truy cập |
|---|---|---|
| ● đặc | Đã điểm danh đủ | chữ "Đã điểm danh" |
| ○ viền nét đứt | **Chưa tạo buổi** (từ lịch tuần) | không gọi là "buổi học" (glossary) |
| ! | Chưa điểm danh hoặc **Quá hạn** | |
| gạch ngang | Đã hủy | |
| chữ "bù" | Học bù | |

Màu theo lớp chỉ để phân biệt; thông tin luôn có chữ/ký hiệu (WCAG 1.4.1). Nhãn ngày: "Thứ Ba 24/11, 2 buổi, 1 chưa điểm danh". Ô chạm tối thiểu 44px. "Quá hạn" chỉ xét 30 ngày gần nhất.

## 5. API `GET /api/today/range?from=yyyy-MM-dd&to=yyyy-MM-dd`

- Chỉ đọc, giới hạn 42 ngày, theo giáo viên đang đăng nhập.
- Trả theo ngày: danh sách buổi `{classId, className, subject, start, end, status, kind(makeup), students, present, absent}` và danh sách `unplanned: {classId, className, start, end}` suy ra từ **phiên bản lịch tuần có hiệu lực ngày đó** (lịch tuần có lịch sử đổi, nên tính ở server).
- Dùng lại: `SessionsOnAsync`/`StatusesAsync`, `MarksAsync`, `EligibleAsync`, `ActiveSchedulesAsync` (mở rộng theo ngày).
- Không đổi schema.

Tạo hàng loạt: gọi lại `POST /api/classes/{id}/sessions/generate` lần lượt từng lớp sau bước xem trước.

## 6. Tải dữ liệu và hiệu năng

- Ngày đang chọn tải trước bằng `/api/today`; widget nạp sau (stale-while-revalidate), tải sẵn tuần/tháng kế khi vuốt.
- Mục tiêu: Hôm nay tương tác < 2 s; chunk widget ≤ 12 KB gzip; `today/range` < 250 ms (42 ngày); đổi tuần/tháng từ cache < 100 ms. Gói chính hiện ~98 KB gzip (mốc ADR 150 KB).
- Đo: Lighthouse mobile 4G chậm + mốc thời gian Playwright.

## 7. Gom mục lớp 7 → 5

`Học tập` = Đánh giá | Chương trình & kiểm tra | Cá nhân hoá (ẩn khi không có Quyền AI). Đường dẫn mới `#/classes/{id}/learning/{assessments|program|personalization}`; đường dẫn cũ (`…/assessments`, `…/program/<tab>`, `…/personalization/<tab>`) tự chuyển. Đổi tên: "Học phí & thu tiền" → "Học phí", "Lịch & điểm danh" → "Lịch", "Báo cáo tháng" → "Báo cáo". "Lịch" của lớp chuyên cấu hình lịch tuần và buổi của riêng lớp; tổng quan xuyên lớp ở widget.

## 8. Kế hoạch năm lát cắt (mỗi lát một issue, một nhánh)

1. Tài liệu: cập nhật ADR 0005 và glossary ("Chưa tạo buổi", "Quá hạn").
2. `today/range` + kiểm thử API.
3. Widget lịch: dải tuần, lưới tháng, desktop, "Quá hạn" + dòng cảnh báo.
4. Tạo buổi hàng loạt: xem trước, kết quả theo lớp.
5. Gom mục lớp 7→5 + chuyển hướng (độc lập).

Chưa tạo issue GitHub cho tới khi được yêu cầu.

## 9. Kiểm thử

- API: `today/range` đúng khoảng, giới hạn 42 ngày, theo chủ sở hữu, chỗ "Chưa tạo buổi" theo đúng phiên bản lịch tuần, lớp khác giáo viên không lọt vào.
- e2e (desktop 1280 + mobile 390): đổi tuần/tháng giữ ngày chọn; chọn ngày hiện đúng buổi; điểm danh từ ngày khác; tạo hàng loạt có xem trước; "Quá hạn"; không cuộn ngang; tên truy cập "Hôm trước/Hôm nay/Hôm sau" còn nguyên; đường dẫn cũ của lớp tự chuyển.
- axe (WCAG 2.2 AA) trên Hôm nay ở ba chế độ; vùng chạm ≥ 44px trên điện thoại; bàn phím (mũi tên, PageUp/PageDown, Enter).

## 10. Rủi ro

- Lịch tuần có phiên bản: tính sai "Chưa tạo buổi" ở quá khứ/tương lai nếu dùng lịch hiện tại.
- Nhiều chấm trong ô tháng khi cuối tuần có 4+ buổi: dùng số buổi + chấm trạng thái.
- Lưới tháng trên màn hẹp: ô ≥ 44px nên phải để cao, kéo mở chỉ khi cần.
- Đổi tên mục lớp làm vỡ test và liên kết cũ: cần chuyển hướng.

## 11. Nguồn tham khảo

- Canonical, "Solving multiple calendar views": https://ubuntu.com/blog/solving-multiple-calendar-views
- Teamup mobile views: https://calendar.teamup.com/kb/how-to-use-calendar-views-on-teamup-mobile-apps/ ; gia sư: https://www.teamup.com/independent-tutor-appointment-system/
- SAP Fiori calendar usage: https://www.sap.com/design-system/fiori-design-android/v25-8/components/calendar/usage
- Google Calendar views: https://support.google.com/calendar/answer/6110849 ; Android Authority: https://www.androidauthority.com/google-calendar-view-button-fix-biggest-annoyance-3605774
- FullCalendar list view: https://fullcalendar.io/docs/list-view ; Schedule-X views: https://schedule-x.dev/docs/calendar/views
- TutorBird: https://www.tutorbird.com/?p=4620 ; IAmTutor: https://apps.apple.com/ke/app/iamtutor/id1569638746
- WCAG 1.4.1 Use of Color: https://www.w3.org/WAI/WCAG22/Understanding/use-of-color ; Android touch target 48dp: https://support.google.com/accessibility/android/answer/7101858

Giới hạn: các nguồn là tài liệu hỗ trợ, blog và hệ thống thiết kế, không phải nghiên cứu khả dụng chính thức.
