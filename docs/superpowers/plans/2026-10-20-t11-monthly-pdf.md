# T11 Monthly PDF Implementation Plan

> Cập nhật 21/10/2026: dùng Cloudflare R2 private thay SeaweedFS/MinIO. [Thiết kế R2](../../../doc/cloudflare-r2.md) quy định cấu hình, quyền tải qua backend, kiểm thử và chuyển dữ liệu; thay thế hướng dẫn kho cũ. Ticket #28 chuyển cấu hình và adapter sang R2; nghiệm thu thực tế cần bucket test và credentials. CI và chuyển dữ liệu cũ lần lượt thuộc #29/#30.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Giáo viên lưu nhận xét/lộ trình, xem trước và tải PDF tháng riêng tư của đúng học sinh, từ nguồn nhất quán.

**Architecture:** Reporting sở hữu bản nháp, snapshot và bản xuất trong schema riêng. Classroom, Learning và Billing cung cấp snapshot qua Contracts; HTML preview và PDF dùng chung template server. Chromium dựng PDF, S3 lưu bytes riêng tư và API xác thực kiểm tra mỗi lần tải.

**Tech Stack:** .NET 10, EF Core/Npgsql hiện tại, React/TypeScript/Vite, Microsoft.Playwright 1.63.0, AWSSDK.S3 4.0.104.4; PdfPig 0.1.16 chỉ dùng trong tests; Chromium, Cloudflare R2 qua S3 API, Noto Sans/Noto Emoji có giấy phép kèm theo.

**Spec:** `docs/superpowers/specs/2026-10-19-t11-monthly-pdf-design.md`, đã được người dùng duyệt. Ticket: [#12](https://github.com/truongtoanr0602/EducationKeeptrack/issues/12). Baseline ứng dụng `256be04b06f6f547cd4568a1908d6cc0cc811c8d`; commit thiết kế `6cda9af`.

## Global Constraints

- Theo ADR0001–0004: mỗi module chỉ đọc/ghi DbContext riêng; liên module qua Contracts; không khóa ngoại xuyên schema.
- Owner lấy từ phiên xác thực; mọi tham chiếu lớp/học sinh/preview/export phải cùng scope.
- "tên 160 ký tự; thông tin liên hệ hiển thị 500 ký tự; nhận xét và lộ trình mỗi trường 10.000 ký tự"; văn bản thuần được encode.
- "Tổng cần thu của báo cáo = dư nợ các kỳ trước tháng + dư nợ của tháng báo cáo." Không cộng kỳ sau; không sửa tiền trên preview.
- "revision có action `confirmed` gần nhất"; bản nháp mới không thay kết quả chính thức đã xác nhận.
- Preview mặc định 15 phút; link tải mặc định 5 phút; expiry dùng TimeProvider, cấu hình được.
- Hết hạn được preview/xuất/tải dữ liệu đã lưu; lưu bản nháp cần CanWrite. Khóa tài khoản chặn cả tải; POST/PUT vẫn cần CSRF.
- PDF private trên S3; file ready bất biến; token tải không thay thế đăng nhập, không redirect ra S3.
- Không AI, Word/DOCX, tự gửi phụ huynh, nghiệp vụ bảo lưu hoặc quy trình T21 trong T11.
- API tiền trả chuỗi số nguyên đồng; nội bộ dùng decimal; không cộng tiền qua JavaScript Number.
- Giữ các file untracked hiện có. Commit từng lát cắt vào nhánh hiện tại theo skill implement; không push/đóng issue tự động.

## Review Focus

1. Bản sửa nháp sau xác nhận phải giữ kết quả chính thức cũ và nhãn thiếu/0 đúng — Task 2.
2. Kỳ tương lai có nợ không được làm tăng QR/tổng báo cáo tháng đang chọn — Tasks 2–3.
3. Lưu DB lỗi sau upload không được phát hành file nửa chừng hoặc xuất hai lần khi retry — Task 4.
4. Tài khoản bị khóa/thu hồi phiên sau khi cấp token vẫn không tải được file — Task 5.
5. Văn bản dài, glyph emoji và nội dung giống HTML không làm mất chữ, chạy script hoặc truy cập mạng tùy ý — Tasks 3 và 6.

## File structure và hợp đồng chung

- `src/Contracts/ReportSources.cs`: DTO snapshot và các public source interfaces; không phụ thuộc EF/Reporting.
- `src/Modules/{Classroom,Learning,Billing}/Infrastructure/*ReportSource.cs`: đọc snapshot bằng context của chính module, với transaction repeatable-read và scope owner.
- `src/Modules/Reporting/Domain/{MonthlyReportDraft,ReportPreview,ReportExport}.cs`: dữ liệu và chuyển trạng thái tương ứng.
- `src/Modules/Reporting/Application/{DraftService,ReportSourceCoordinator,PreviewService,ExportService,DownloadService}.cs`: mỗi file phụ trách một luồng, không truy vấn context module khác.
- `src/Modules/Reporting/Infrastructure/{ReportingDatabase,ReportingRepository,ReportHtmlTemplate,ChromiumPdfRenderer,S3ReportStore,DownloadTokens}.cs`: lưu dữ liệu, dựng nội dung/PDF, lưu object và bảo vệ token.
- `src/Modules/Reporting/ReportingModule.cs`: DI, endpoint authorization, mapping lỗi. Đăng ký tại `src/Host/Program.cs`, migration riêng trong `--migrate`.
- `ui/src/{MonthlyReports,ReportDraftForm,ReportPreview,ReportExports}.tsx` và `reporting-api.ts`: container, từng phần UI và DTO/API client.
- `tests/ApiTests/{ReportingTests,ReportPreviewTests,ReportExportTests,ReportDownloadTests}.cs`, `ReportingFixture.cs`, `ui/e2e/monthly-reports.spec.ts`: chỉ các seam đã duyệt.

Các kiểu dưới đây là tên/hợp đồng cho các task; DTO nội dung là records bất biến, collections sắp thứ tự ổn định trước khi tính stamp:

```csharp
record ReportScope(string OwnerId, Guid ClassId, Guid StudentId, DateOnly Month);
record ReportProfile(string Name, string? ParentName, string? Phone, string? Email);
record ReportSession(Guid Id, DateOnly Date, TimeOnly StartTime,
    string SessionStatus, string AttendanceStatus, bool Excused);
record ClassroomReportSnapshot(string Stamp, string FinancialSourceStamp,
    string ClassName, string Subject, ReportProfile Profile,
    IReadOnlyList<ReportSession> Sessions);
record ReportAssessment(Guid Id, DateOnly Date, string Title, string Status,
    int? Completion, int? Correctness, bool HasConfirmedResult);
record LearningReportSnapshot(string Stamp, IReadOnlyList<ReportAssessment> Assessments);
record ReportFinance(decimal OldDebtVnd, decimal PeriodFeesVnd, decimal PeriodPaidVnd,
    decimal PeriodHeldVnd, decimal PeriodDebtVnd, decimal UnallocatedVnd);
record ReportFeeLine(Guid SessionId, DateOnly Date, decimal? UnitPriceVnd, decimal? AmountVnd);
record ReportAdjustment(Guid Id, string Direction, decimal AmountVnd, string Reason);
record ReportAllocation(Guid ReceiptId, DateOnly ReceivedOn, string Month, decimal AmountVnd);
record ReportRecipient(string BankBin, string BankName, string AccountNo, string AccountName);
record BillingReportSnapshot(string Stamp, string FinancialSourceStamp, string Status,
    long Version, long RecipientVersion, ReportFinance Finance,
    IReadOnlyList<ReportFeeLine> Fees, IReadOnlyList<ReportAdjustment> Adjustments,
    IReadOnlyList<ReportAllocation> Allocations, ReportRecipient? Recipient);
record ReportPayment(string Kind, byte[]? QrPng, ReportRecipient? Recipient,
    string AmountVnd, string Content, IReadOnlyList<string> Warnings);
interface IClassroomReportSource { Task<ClassroomReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct); }
interface ILearningReportSource { Task<LearningReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct); }
interface IBillingReportSource {
    Task<BillingReportSnapshot?> ReadAsync(ReportScope scope, CancellationToken ct);
    Task<ReportPayment> PaymentAsync(ReportScope scope, BillingReportSnapshot snapshot, CancellationToken ct);
}
```

Stamp là SHA-256 của nội dung/version canonical theo scope; phải phát hiện thêm bài/buổi, sửa hồ sơ, điểm danh, revision và người nhận. `FinancialSourceStamp` của Classroom/Billing cùng định nghĩa trên các session/attendance versions mà Billing đối chiếu, gồm lịch sử ảnh hưởng dư nợ. Đây là hợp đồng dữ liệu, không truy cập bảng khác schema.

Reporting định nghĩa `ReportResult<T>(T? Value, int Status, Dictionary<string,string[]>? Errors)` và các DTO request:
`DraftInput(string? DisplayName,string? ParentDisplayName,string? ContactDisplay,string Comment,string Roadmap,string Emoji,long Version)`,
`ExportInput(Guid PreviewId,long DraftVersion,Guid IdempotencyKey)`.
Emoji cho phép: `none`, `🙂`, `⭐`, `📘`; mặc định `none`. Tên phụ huynh dùng giới hạn tên 160. Các override rỗng trở về nguồn, không xóa hồ sơ.

## Task 1: Lưu/đọc bản nháp với đúng scope và quyền

**Files:** tạo `Reporting.csproj`, `ReportingModule.cs`, `Domain/MonthlyReportDraft.cs`, `Application/DraftService.cs`, `Infrastructure/ReportingDatabase.cs`, `ReportingRepository.cs`, migrations; thêm phần Classroom của `Contracts/ReportSources.cs` và `Classroom/Infrastructure/ClassroomReportSource.cs`; sửa ClassroomModule, Host.csproj, Program.cs, EducationKeeptrack.slnx, TestApplication.cs; test `ReportingTests.cs`, `ReportingFixture.cs`.

**Interfaces:** `DraftService.ReadAsync(ReportScope,CancellationToken)` và `SaveAsync(ReportScope,DraftInput,CancellationToken)` trả `ReportResult<DraftView>`. `DraftView` có Scope IDs/month, các trường DraftInput, Version, SavedAt nullable; bản chưa lưu có version 0. API prefix `/api/reporting/classes/{classId}/students/{studentId}/months/{month}` dùng `GET /draft`, `PUT /draft`.

- [ ] Viết `ReportingTests.Draft_roundtrip_is_owner_scoped_versioned_and_does_not_change_source_profile`: GET version 0; PUT comment `Tiến bộ về phân số`, roadmap `Luyện 10 bài`, version 0 thành 1; GET đúng nội dung; tên nguồn không đổi; stale version trả 409; giáo viên khác trả 404. Fixture tạo lớp/học sinh qua API, theo mẫu TuitionTests, không query DB để xác nhận hành vi.
- [ ] Chạy `dotnet test tests/ApiTests --filter FullyQualifiedName~Draft_roundtrip`; xác nhận thất bại do endpoint chưa có.
- [ ] Thêm module/context schema `reporting`; unique draft owner/class/student/month, Version concurrency token. ClassroomReportSource kiểm tra hồ sơ thuộc lớp kể cả đăng ký đã dừng, không làm mất lịch sử. Implement hai endpoint, validate giới hạn spec, allow GET CanRead và PUT CanWrite; không INSERT bản nháp trong GET.
- [ ] Thêm `Draft_limits_csrf_foreign_scope_and_read_only_expiry`: vượt 160/500/10000 hoặc emoji ngoài danh sách trả 400; thiếu CSRF 400; hết hạn GET được, PUT 403; anonymous/Admin bị từ chối. Kiểm tra tên/contact override chỉ nằm trong draft.
- [ ] Chạy `dotnet test tests/ApiTests --filter FullyQualifiedName~ReportingTests`, migrate PostgreSQL thật; phải pass. Commit `feat(reporting): persist scoped monthly report drafts` với danh sách file task.

## Task 2: Preview từ snapshot nhất quán của ba module

**Files:** hoàn tất `Contracts/ReportSources.cs`; tạo `Learning/Infrastructure/LearningReportSource.cs`, `Billing/Infrastructure/BillingReportSource.cs`, đăng ký hai module; tạo `Reporting/Domain/ReportPreview.cs`, `Application/ReportSourceCoordinator.cs`, `PreviewService.cs`; cập nhật ReportingDatabase/repository/migration; test `ReportPreviewTests.cs`.

**Interfaces:** `ReportSourceCoordinator.CaptureAsync(ReportScope,CancellationToken)` trả `ReportResult<ReportSourceSnapshot>` gồm ba snapshot trên và CapturedAt. `ValidateAsync(ReportScope,ReportSourceSnapshot,CancellationToken)` trả bool bằng cách đọc lại stamp. `PreviewService.CreateAsync(ReportScope,CancellationToken)` trả `ReportResult<PreviewView>`; PreviewView có Id, DraftVersion, SourceStamp, ExpiresAt, Warnings, IsExportable. Endpoint `POST /previews`, chỉ nhận scope, không nhận tiền/HTML client.

- [ ] Viết `Preview_uses_confirmed_results_and_exact_month_obligations`: xác nhận 75/50 rồi sửa nháp 25/25; preview vẫn 75/50. Bài không làm có 0/0, bài chưa xác nhận có null và nhãn thiếu. Phí tháng trước 500000, tháng này 900000, phân bổ 700000, kỳ sau 300000: oldDebt 0, periodPaid 200000, periodDebt 700000 và tổng cần thu 700000. Không đưa học sinh thứ hai vào snapshot.
- [ ] Chạy `dotnet test tests/ApiTests --filter FullyQualifiedName~Preview_uses_confirmed`; phải thất bại trước code.
- [ ] Implement source reads bằng repeatable-read context riêng; Learning chọn latest `confirmed` theo Version, không lấy current draft. Billing tái sử dụng PeriodBalance cho debt/held, lọc kỳ <= tháng, giữ fee lines và reasons; không cộng kỳ sau. Profile, title/list IDs và source versions có trong stamp.
- [ ] Implement capture hai lượt stamp; bắt buộc FinancialSourceStamp tương ứng và Billing ready. Nguồn thay trả 409, thiếu giá/pending trả 409 có hướng dẫn. Snapshot preview bất biến, TTL 15 phút; đọc draft cùng scope/version, không tự lưu draft mới khi giáo viên hết hạn.
- [ ] Thêm `Preview_rejects_changed_sources_pending_fees_and_missing_rate` và `Preview_changes_when_new_assessment_profile_or_recipient_changes`: thay nguồn giữa hai lượt đọc qua fixture decorator ở public source boundary, kiểm tra 409; stable capture có cùng stamp; test fingerprint bắt thêm bài và sửa hồ sơ.
- [ ] Chạy `dotnet test tests/ApiTests --filter FullyQualifiedName~ReportPreviewTests`; phải pass. Commit `feat(reporting): capture consistent monthly report previews`.

## Task 3: Xuất PDF thật, lưu private S3 và metadata ready

**Files:** tạo `Domain/ReportExport.cs`, `Application/ExportService.cs`, `Infrastructure/ReportHtmlTemplate.cs`, `ChromiumPdfRenderer.cs`, `S3ReportStore.cs`, `ReportingOptions.cs`, `Assets/Fonts/` kèm LICENSE; sửa Reporting.csproj, context/repository/migration/ReportingModule, Host Program.cs, Dockerfile, compose.yaml, .env.example, ApiTests.csproj, ReportingFixture.cs; tạo `ReportExportTests.cs`.

**Interfaces:** `ReportHtmlTemplate.Render(ReportPreview,ReportPayment) -> string`; `IReportPdfRenderer.RenderAsync(string html,CancellationToken) -> Task<byte[]>`; `IReportObjectStore.PutAsync(string key,byte[] pdf,CancellationToken) -> Task`, `OpenAsync(string key,CancellationToken) -> Task<Stream>`. `ExportService.ExportAsync(ReportScope,ExportInput,CancellationToken)` trả `ReportResult<ExportView>`; ExportView có Id, State, CreatedAt, ByteLength, Sha256, DraftVersion, SourceStamp, Warnings. `POST /exports`, `GET /exports`, `GET /previews/{id}/html`.

- [ ] Viết `Export_creates_private_pdf_with_vietnamese_confirmed_results_finances_and_fallback`: dùng dữ liệu Task 2, draft tiếng Việt, tải bytes qua đường file có quyền (Task 5 sẽ hoàn thiện token) hoặc giữ test chờ endpoint Task 5; các assertions PDF cuối phải qua API. Trong lát cắt này kiểm tra export metadata/HTML qua API: ready, `application/pdf`, byte length > 0, HTML chỉ học sinh chọn, tổng 700000 và cảnh báo thiếu QR. Test thật renderer/storage được hoàn tất qua download seam ở Task 5, không thêm endpoint test-only.
- [ ] Chạy test export qua API, xác nhận fail trước implementation. Chuẩn bị Cloudflare R2 qua S3 API thật cho fixture bằng config bên dưới; không thay storage bằng in-memory để nhận test pass.
- [ ] Pin packages như Tech Stack; tạo renderer Chromium A4 từ template shared, font Noto Sans regular/bold và Noto Emoji từ upstream chính thức, đóng gói license và asset trong output. Whitelist emoji có nhãn chữ kèm theo. Dùng ảnh data URI, encode text, tắt script/network ngoài và chờ `document.fonts.ready`/ảnh trước PDF. Giới hạn render concurrency 2, timeout 60 giây; browser dùng service lifecycle, context/page riêng mỗi render và được dispose.
- [ ] Billing `PaymentAsync` dùng số tiền oldDebt + periodDebt chính snapshot; đọc recipient bằng context Billing; tái sử dụng IVietQrGenerator/validation ảnh cho QR động -> static -> text. Không gọi endpoint đòi CanWrite khi Reporting đang xuất với CanRead. Tổng 0 không QR yêu cầu trả tiền; thiếu recipient có warning, không chặn PDF; revalidate recipient/source sau generate.
- [ ] S3ReportStore dùng bucket private cấu hình chung deployment (đổi bucket phải chuyển toàn bộ object giữ key), path-style cho R2, key `reports/{owner-scope-hash}/{idempotencyKey}/{attemptId}.pdf`, không nhận key/endpoint từ người dùng. Mỗi attempt có key riêng; metadata ready chọn đúng object thắng để worker mất lease không ghi đè bytes của bản đã công bố. ReportingOptions: S3 Endpoint/Region=auto/Bucket/AccessKey/SecretKey/ForcePathStyle=true, PreviewMinutes=15, DownloadMinutes=5, RenderTimeoutSeconds=60, RenderConcurrency=2. Options được dùng lazy để endpoint module cũ vẫn chạy khi S3 chưa cấu hình; xuất thiếu cấu hình trả 503 có thông báo, không fallback local public file.
- [ ] Dùng bucket R2 test cấp trước, secrets từ môi trường/Jenkins; TEST_S3_ENDPOINT, TEST_S3_ACCESS_KEY, TEST_S3_SECRET_KEY, TEST_S3_BUCKET, region auto. Prefix ngẫu nhiên mỗi run; chỉ dọn object của run, không tạo/xóa bucket hoặc dùng production. Thiếu credentials phải báo chưa xác minh. Cài Chromium trước API tests. Bỏ service s3/volume/depends_on cũ sau chuyển dữ liệu được đối chiếu.
- [ ] Thêm chế độ Host `--install-report-browser` gọi Playwright installer trước khởi tạo app. Docker runtime Ubuntu Noble .NET10 cài Chromium/deps khi build với root, đặt `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright`, rồi chạy app bằng APP_UID có quyền đọc font/browser. Cài browser tương ứng đúng Playwright version cho local/tests; không dùng browser từ npm version khác làm dependency mặc định.
- [ ] Export validate snapshot, render/upload, validate lại nguồn/draft/quyền đọc, rồi mới ready. HTML endpoint trả CSP/sandbox phù hợp và no-store, không có script. Chạy test task + `dotnet build EducationKeeptrack.slnx -c Release`; phải pass. Commit `feat(reporting): render and store private monthly PDFs`.

## Task 4: Chống lặp, tranh chấp và phục hồi lỗi xuất

**Files:** sửa ReportExport.cs, ExportService.cs, ReportingRepository.cs, ReportingDatabase.cs/migration nếu cần; thêm `Application/ExportRecovery.cs`; mở rộng ReportExportTests.cs và fixture decorators.

**Interfaces:** `ExportRecovery.TryAcquireAsync(Guid exportId,CancellationToken) -> Task<Guid?>` trả attempt/lease-owner ID khi lấy lease thành công, null nếu đang có lease hợp lệ; repository giữ lease owner UUID, LeaseUntil và fencing check trên mọi chuyển trạng thái của attempt. Unique owner/idempotency key; fingerprint của scope + preview ID + draft version. Lease 120 giây với render timeout 60 giây; request thứ hai có lease đang giữ trả 409 `export-in-progress`, UI giữ nguyên key để retry.

- [ ] Viết `Export_retry_after_lost_response_returns_same_file_and_changed_payload_conflicts`: hai lệnh cùng key trả cùng export ID và checksum khi ready; payload khác trả 409; chỉ một metadata ready. `Export_concurrent_requests_never_publish_two_files` kiểm tra qua list/download API, không đọc bảng private.
- [ ] Chạy các test trên, xác nhận hành vi fail trước hardening; implement reserve pending trong transaction ngắn, release lock trước render/network, CAS fencing khi ready. Không giữ DB transaction/lock trong toàn bộ render/upload.
- [ ] Viết `Export_upload_or_final_save_failure_has_no_downloadable_partial_result`: storage boundary ném lỗi; interceptor Reporting ném sau save trước commit. API không trả ready; cùng lệnh retry an toàn, export ID ổn định và các object attempt đều private; không thay bytes của một ready export.
- [ ] Viết `Export_recovers_expired_lease_after_restart_and_rejects_source_change_during_render_or_upload`: reopen host/database bằng fixture hiện có, advance clock >120 giây cho pending; worker cũ mất lease không được publish. Cho worker cũ hoàn tất upload sau khi worker mới ready, tải lại vẫn có checksum của worker mới. Nguồn/draft/người nhận đổi trong renderer/store decorator phải trả 409 và không công bố file.
- [ ] Khi ready, replay được trả trước kiểm tra preview còn hạn/nguồn hiện tại, nhưng sau ownership/CanRead. Pending retry sau preview hết hạn chỉ phục hồi nếu snapshot còn hợp lệ và nguồn không đổi; nếu không trả 409 cần preview/key mới. Failed retry cùng payload không tạo thêm export ID; lỗi stale kết thúc bản pending không ready.
- [ ] Chạy `dotnet test tests/ApiTests --filter FullyQualifiedName~ReportExportTests`; phải pass. Commit `fix(reporting): make PDF export retries and recovery atomic`.

## Task 5: Tải có hạn và kiểm tra quyền trên PDF thật

**Files:** tạo `Infrastructure/DownloadTokens.cs`, `Application/DownloadService.cs`, `tests/ApiTests/ReportDownloadTests.cs`; sửa ReportingModule/fixture và hoàn tất các PDF assertions trong ReportExportTests.

**Interfaces:** `DownloadTokens.Issue(string owner,Guid exportId,DateTimeOffset expiresAt) -> string`, `Validate(string token,string owner,Guid exportId,DateTimeOffset now) -> bool` dùng ASP.NET DataProtection purpose riêng. `DownloadService.IssueAsync(ReportScope,Guid exportId,CancellationToken)` trả DownloadLinkView(Url,ExpiresAt); `OpenAsync(ReportScope,Guid exportId,string token,CancellationToken)` trả ReportResult<ReportFile>, ReportFile chứa Stream/FileName/ByteLength. Endpoints đúng spec `/exports/{id}/download-link` và `/exports/{id}/file?token=...`.

- [ ] Viết `Download_requires_current_owner_session_and_unexpired_bound_token`: owner tải 200; token giả/khác export/owner không tải; TimeProvider +5 phút hết hạn; anonymous 401, giáo viên khác 404/403 không lộ file; bucket/object không truy cập anonymous.
- [ ] Chạy test để xác nhận fail; implement link TTL 5 phút, scope owner/class/student/month/export, kiểm tra ready và CanRead cả lúc cấp/tải. Mọi POST cần CSRF. Stream S3 qua server, attachment filename chỉ dùng ID/tháng an toàn; no-store; che token khỏi access log hoặc chuyển sang cơ chế log path không query cho route này.
- [ ] Viết `Expired_teacher_exports_saved_content_but_cannot_edit_and_blocked_account_cannot_download`: hết hạn PUT draft 403, POST preview/export/download link vẫn thành công từ bản đã lưu; draft version không đổi; khóa/thu hồi phiên sau cấp token bị chặn. Recheck quyền trước publish và trước stream, không gọi AI.
- [ ] Hoàn tất `Export_creates_private_pdf...`: dùng PdfPig mở bytes tải qua API; assert tên/nhận xét tiếng Việt, 75/50 thay vì draft25/25, trạng thái thiếu/0, tổng700000, nợ kỳ sau không cộng, absence của học sinh khác và warning QR. Thêm zero-debt, static/manual fallback và recipient-changed cases.
- [ ] Viết `Pdf_long_vietnamese_text_and_allowed_emoji_remain_readable_across_pages`: nhận xét/lộ trình 10000 ký tự hợp lệ, page count >1, đủ đoạn đầu/cuối và lặp tiêu đề. Rasterize mẫu bằng PDF viewer/tool chuẩn khi kiểm tra artifact; xem ảnh để xác nhận emoji/glyph/ngắt trang, không suy từ text extraction rằng emoji đã hiển thị đúng.
- [ ] Chạy `dotnet test tests/ApiTests --filter 'FullyQualifiedName~ReportDownloadTests|FullyQualifiedName~ReportExportTests'`; phải pass. Commit `feat(reporting): authorize expiring PDF downloads`.

## Task 6: UI bản nháp, preview và xuất/tải trên desktop/mobile

**Files:** tạo `ui/src/reporting-api.ts`, MonthlyReports.tsx, ReportDraftForm.tsx, ReportPreview.tsx, ReportExports.tsx; tích hợp tại Roster.tsx; thêm style trong style.css, `ui/e2e/monthly-reports.spec.ts`.

**Interfaces:** `MonthlyReports({classId,students,canWrite,onDenied})` nhận roster hiện có, tự chọn tháng; UI children nhận DTO reporting-api đã bám API Tasks 1–5. `ReportDraftForm` giữ draft cục bộ; `ReportPreview` iframe sandbox không allow-scripts/same-origin, chỉ URL HTML được API xác thực; `ReportExports` dùng idempotency ref giữ lệnh chưa rõ kết quả.

- [ ] Viết browser test `teacher edits a saved monthly draft, previews, exports and downloads only the selected student`: tạo dữ liệu bằng API, chọn học sinh/tháng, lưu tiếng Việt, preview iframe có đúng học sinh và tổng, tải PDF với event download và mở bytes kiểm tra `%PDF-`; không có trường ghi đè tổng tiền. Chạy `npx playwright test monthly-reports.spec.ts --project desktop`; phải fail trước UI.
- [ ] Implement các component: lưu trước preview/export; đang có chỉnh sửa chưa lưu thì chặn xuất và hướng dẫn lưu. Validation/409/403 giữ nội dung nhập. Sau refresh giữ class hash và học sinh/tháng qua query params riêng. Khi đổi scope, invalidate preview và pending state của scope cũ; không dùng key/request cho học sinh mới.
- [ ] Thêm browser assertions cho HTML-like comment `<script>...</script>` hiện dạng text, không thực thi; nút tài chính mở nguồn đúng class mà không đổi sang hash giả; thiếu QR vẫn xuất có cảnh báo. Bản xem trước có ngữ nghĩa đối chiếu nguồn/phiên bản.
- [ ] Thêm `uncertain export retries the same command and source conflict preserves draft`: route fetch commit rồi abort response, retry giữ preview/key; 409 source-changed yêu cầu preview mới và giữ text; không tự gửi export lần nữa bằng key khác.
- [ ] Thêm `expired service keeps report fields read-only but saved preview export works`: UI không mở quyền ghi khi nhận metadata xuất; pending/unverified/blocked xử lý qua onDenied và server authority. Kiểm tra mobile không tràn ngang và iframe cuộn riêng.
- [ ] Chạy `npm --prefix ui run typecheck` và `npm --prefix ui run build`; chạy browser suite T11 cả desktop/mobile với backend thật/S3 thật. Commit `feat(ui): add manual monthly PDF workflow`.

## Task 7: CI, mẫu kiểm chứng, review và hoàn tất

**Files:** sửa compose.ci.yaml, Jenkinsfile, README.md, .env.example và `.gitignore` nếu có artifact tạm mới; tạo docs/verification/T11.md. Không đưa credentials, PDF học sinh hoặc test logs cá nhân vào Git.

- [ ] Dùng bucket R2 test cấp trước, secrets từ môi trường/Jenkins; TEST_S3_ENDPOINT, TEST_S3_ACCESS_KEY, TEST_S3_SECRET_KEY, TEST_S3_BUCKET, region auto. Prefix ngẫu nhiên mỗi run; chỉ dọn object của run, không tạo/xóa bucket hoặc dùng production. Thiếu credentials phải báo chưa xác minh. Cài Chromium trước API tests. Bỏ service s3/volume/depends_on cũ sau chuyển dữ liệu được đối chiếu.
- [ ] Ghi lệnh local restore/migrate/install-browser/run và S3 config; test pipeline và Compose config render không lộ secret. Không dùng cấu hình production cloud để chạy integration tests.
- [ ] Chạy `dotnet test EducationKeeptrack.slnx -c Release --logger 'console;verbosity=minimal'` với real PostgreSQL + S3, rồi `npx playwright test --workers=1` từ ui với demo cô lập; cả hai phải 0 failed/0 skipped. Chạy `npm --prefix ui run build`, `dotnet publish src/Host -c Release`, `dotnet ef migrations has-pending-model-changes --project src/Modules/Reporting --startup-project src/Host --context ReportingDatabase --configuration Release` và `git diff --check`; phải thành công, model không còn thay đổi chưa migrate.
- [ ] Lưu mẫu PDF/ảnh rasterize trong `.local/t11-artifacts/`, mở ảnh nhiều trang để kiểm tra tiếng Việt/emoji/QR. Verification doc ghi test count, công cụ trích xuất/rasterize, lỗi đã tái hiện trước fix và kết quả; chỉ commit mẫu đã loại dữ liệu nhạy cảm nếu cần artifact repo.
- [ ] Dùng skill `code-review` với baseline ứng dụng nêu trên, hai trục Standards/Spec; sửa lỗi và chạy lại test liên quan. Với lỗi/chỉnh behavior mới cần chạy lại phần/full suite phù hợp, không lặp vô cớ sau khi đã đạt.
- [ ] Commit `docs(reporting): record T11 verification and runtime setup`; kiểm tra git log/status, chỉ file T11 được commit. Nếu cập nhật demo đang chạy, giữ URL localhost cũ, kiểm tra API/UI sau restart và ghi cách dùng; không push hoặc gửi PDF cho người khác.

## Kiểm tra kế hoạch và thứ tự thực thi

Tasks 1→2→3→4→5→6→7 có phụ thuộc tuần tự; không giao đồng thời các task sửa cùng contracts/context. Task 3 chứng minh metadata/HTML và kho thật; toàn bộ assertions về PDF qua HTTP download hoàn tất ở Task 5, trước khi công bố tính năng xong. Test helpers chỉ tạo dữ liệu qua API; failure injection được đặt tại renderer/store/commit boundary, không dùng để thay renderer/PDF/S3 trong các ca nghiệm thu thành công.

Coverage: draft/scope/limits → 1; nguồn/confirmed/fee/version → 2; shared HTML/QR/PDF/S3 → 3; retry/race/restart → 4; expiry/blocked/private download/real PDF → 5; thao tác người dùng/mobile → 6; runtime, artifact, full regression và review → 7. Cả năm Review Focus đều có test nêu tên tại task sở hữu.

Phiên bản package trên được kiểm tra từ NuGet metadata khi lập kế hoạch:
[Playwright](https://api.nuget.org/v3-flatcontainer/microsoft.playwright/index.json),
[AWSSDK.S3](https://api.nuget.org/v3-flatcontainer/awssdk.s3/index.json),
[PdfPig](https://api.nuget.org/v3-flatcontainer/pdfpig/index.json).
Khi restore, xác nhận compatibility .NET10 và browser binary; thay version nếu thực sự cần phải được ghi lý do và kiểm thử lại, không tự nâng package của các module cũ.

Kế hoạch đang chờ người dùng duyệt và chọn phương thức thực thi. Khuyến nghị Native: các task phụ thuộc chặt vào hợp đồng nguồn và state export; triển khai nối tiếp trong phiên hiện tại rồi review độc lập ở cuối giảm giao tiếp lặp.
