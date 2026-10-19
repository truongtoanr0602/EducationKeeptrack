# Education Keeptrack — T01 / T02 / T03 / T04 / T05 / T06

Teacher login/logout, owned classes, roster, attendance, actual tuition and manual assessment. .NET 10 modular monolith, ASP.NET Core Identity, EF Core/Npgsql, PostgreSQL 18, React + TypeScript + Vite. Identity, Classroom, Billing, Learning and the minimal ServiceAccess implementation have independent schema, context and migration history. Business modules consume `IAccountAccess`; ServiceAccess consumes Identity's `IAccountState` contract. Billing reads Classroom only through `IClassroomBillingSource` snapshots and `ClassroomChange` events, without cross-schema reads or foreign keys.

## Local development

Requirements: SDK in `global.json`, Node 22+, PostgreSQL 18 (or Docker with Compose 2.24.4+ for the TLS override).

Set `ConnectionStrings__Database` in the shell to a local PostgreSQL connection string (database must exist). Keep credentials outside Git. Then:

```powershell
dotnet tool restore
dotnet run --project src/Host -- --migrate
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Provision__Email = 'your-demo-email@example.test'
# Set Provision__Password to a strong password of at least 12 characters.
# Set Provision__Until to an explicit future UTC ISO timestamp.
dotnet run --project src/Host -- --provision-teacher
dotnet run --no-launch-profile --project src/Host --urls http://127.0.0.1:5080
# In another terminal:
cd ui
npm ci
npm run dev
```

Open http://127.0.0.1:5173. Sign in, create a class, reload and sign out. Provisioning is Development-only, explicitly confirms/grants a demo account and refuses to overwrite existing accounts. No account or password is hardcoded; no public registration is exposed.

On a class card, choose **Danh sách học sinh** to create a student with optional parent/contact details or enroll an existing student belonging to the current teacher. The selected class survives reload through the URL fragment. Student profiles are shared across that teacher's classes; editing a profile updates its contact details everywhere. Names are not identity keys, so different students can have the same name.

Enrollment records keep their start date and a versioned history of **Đang học / Nghỉ có phép / Dừng học** changes, including effective dates and server timestamps. A stopped enrollment keeps its end date and cannot reopen; use a new enrollment starting after its end date. Overlapping enrollment periods and concurrent duplicate enrollments in the same class are rejected. History is retained; no balance or reserve transfers occur between classes. A failed enrollment after profile creation retains the created profile ID for retry, rather than creating another student.

T02 uses the existing `IAccountAccess` contract on every roster route. The full production approval/renewal/locking workflow in dependency [#26](https://github.com/truongtoanr0602/EducationKeeptrack/issues/26) is still open; the current ServiceAccess implementation remains the controlled development grant described below. T02 does not implement that separate workflow.

Roster API: `GET/POST /api/students`, `GET/PUT /api/students/{id}`, `GET/POST /api/classes/{classId}/enrollments`, and `PUT /api/classes/{classId}/enrollments/{id}`. Profile fields are `name`, `parentName`, `phone`, `email`, `notes`; profile edits also require the current `version`. Enrollment creation takes `studentId`, `startDate` (`YYYY-MM-DD`); status changes take `status` (`active`, `leave`, `stopped`), `effectiveDate`, and the current `version`. A change date cannot precede the start or latest history entry. Stopping uses the effective date as the end date. Stale writes return 409, invalid input 400, and another teacher's references 404. After a conflict, **Tải lại danh sách** refreshes the expected version while retaining the open form's input. All writes require CSRF. Date input defaults use Vietnam time; dates are calendar dates, while recorded timestamps are UTC.

## Weekly schedules and attendance (T03)

Open a class's **Danh sách học sinh** and use **Lịch và điểm danh** below its roster. Save multiple weekly days/times and an effective date, choose a date range, then **Tạo buổi theo lịch**. Each immutable schedule revision applies from its effective date; generating an existing slot again keeps its session. Schedule changes never move/delete existing sessions. Generation rejects overlaps atomically rather than creating a partial range. An empty weekly schedule stops recurring slots from its effective date. Operational request limits are 100 weekly slots and 366 calendar days per range.

Use **Tạo buổi riêng / học bù** for a specific date/time. A makeup may reference a cancelled session in the same class; only one non-cancelled replacement may reference that source. Overlapping times, including cancelled sessions, are rejected to preserve the original session record. Sessions begin **Dự kiến**; explicitly change to **Đã diễn ra** before marking attendance. Cancelling requires a reason, preserves prior marks/history and is terminal; create a separate makeup when needed. This slice does not calculate tuition or transfer balances.

Attendance starts **Chưa điểm danh**, separately from the enrollment's status. Choose **Có mặt** or **Vắng**, with optional absence reason and **Nghỉ có phép**. Green present rows and grey absent rows include saved text labels; unsaved selections do not change the saved label/color. Session-day eligibility uses the teacher's enrollment start/end dates (inclusive). Previously saved marks remain readable if enrollment dates later change. A student added later with an enrollment covering the session date appears unmarked. Only held sessions contribute to the missing-attendance reminder for the selected date range; planned/cancelled sessions do not. The reminder does not close or finalize a billing month.

Schedule, session and attendance updates use expected versions. Attendance edits also advance the parent session version. Conflict/error/expiry preserves open form input; **Tải lại điểm danh** refreshes row versions for retry, and **Tải lại lịch và buổi** refreshes schedule/session versions. Expired service is read-only, pending/unverified accounts are denied and account blocking revokes access; Admin role has no default access. The open production ServiceAccess dependency described under T02 still applies.

API routes below `/api/classes/{classId}`: `GET/PUT /schedule`, `GET/POST /sessions`, `POST /sessions/generate`, `PUT /sessions/{id}`, `GET /sessions/{id}/attendance`, `PUT /sessions/{id}/attendance/{studentId}`, `GET /sessions/{id}/history`, and `GET /attendance-missing`. List/reminder queries require `from`/`to` as `YYYY-MM-DD`; time inputs use `HH:mm:ss`. Schedule slots use `dayOfWeek` 0=Sunday through 6=Saturday. Generation takes `from`, `to`, `scheduleVersion`; manual sessions take `date`, `startTime`, `endTime`, `kind` (`regular`/`makeup`), optional `replacesSessionId`. Session updates take `status` (`planned`/`held`/`cancelled`), optional `reason`, `version`. Attendance updates take `status` (`unmarked`/`present`/`absent`), optional `reason`, `excused`, `version`; an unmarked row without a saved record has version 0. Times and calendar dates use `Asia/Ho_Chi_Minh`; each session additionally stores UTC start/end instants. Start/end times must fall on the same Vietnam calendar date; overnight sessions are not supported in this slice.

Session creation/status changes and attendance changes save a versioned history/outbox entry in the source transaction. The background dispatcher delivers `ClassroomChange` through registered `IClassroomChangeHandler` implementations in memory, acknowledging only after all handlers succeed. Failed deliveries remain pending for retry and block later versions of the same session; consumers must deduplicate `EventId` and use source versions because delivery is at least once. Batch selection rotates across session heads so a failed backlog does not prevent other sessions from being attempted. Billing now consumes this contract; Learning consumers remain a later ticket.

## Actual tuition (T04)

The class workspace includes **Học phí theo buổi**. Enter a whole-dong unit price and effective date, then choose the tuition month. Each student's details list the Vietnam calendar date/time, attendance/session basis, captured unit price and fee. Eight held/present sessions at 150,000 VND produce 1,200,000 VND. Absent, unmarked, planned and cancelled sessions produce no fee; a makeup is a distinct session charged once when held/present. These are accrued tuition amounts, separate from receipts or outstanding debt in later tickets.

Billing owns immutable effective rates and persisted per-session/per-student fees. A session captures its rate when first processed as held, including absent/unmarked sessions so later attendance corrections retain the same price. Rate creation also reads current Classroom snapshots under the Billing class lock to freeze already-held sessions before changing the plan, even if event delivery is delayed. A new rate cannot overlap an interval containing a priced/already-held session covered by an existing rate. Existing rate dates cannot be overwritten. Initial pricing and filling an unpriced gap are supported; historical financial adjustments require the separate adjustment workflow. Planned future sessions capture the rate effective on their session date when they become held.

The monthly response is `ready`, `pending`, or `missing-rate`. Pending means Classroom source versions or the rate refresh have not caught up; displayed totals belong to the previous persisted projection. Missing-rate shows unpriced held/present lines explicitly and labels totals as partial. UI refreshes after attendance changes in the workspace and polls GET only while pending, without extending idle sessions. Month selection survives reload in the URL. Errors/conflicts/expiry retain the rate draft; reloading refreshes the expected rate version for retry. ServiceAccess enforces all reads/writes, expired teachers can view old fees, and Admin has no default access. The production ServiceAccess dependency #26 remains open.

API: `GET/POST /api/billing/classes/{classId}/rates` and `GET /api/billing/classes/{classId}/tuition?month=YYYY-MM`. POST takes `effectiveFrom` (`YYYY-MM-DD`), `unitPriceVnd` (whole-dong string, up to 18 digits), and the current rate-plan `version` (0 initially). GET rates returns that version and immutable rate entries. Tuition returns an aggregate `version`, `rateVersion`, status, total, students and per-session lines with source versions/rate IDs. Money is stored as PostgreSQL `numeric(18,0)` and returned as decimal integer strings; UI uses `BigInt` formatting, including totals above JavaScript's safe integer range. All writes require CSRF; foreign classes return 404, stale price versions return 409 and invalid input returns Vietnamese validation errors.

Classroom supplies repeatable-read owner-scoped snapshots from its own context. Billing consumes its durable outbox, applies only newer source versions, and commits fees, aggregate version, inbox event ID and `tuition.changed` outbox entry atomically. Pricing commits its `rate.changed` event and durable refresh flag together. Workers resume pending refresh/delivery after restart; duplicate deliveries cannot add a fee twice. Billing's `ITuitionChangeHandler` contract offers at-least-once, ordered-per-class notifications with fair batch rotation; downstream handlers must deduplicate event IDs. No Reporting consumer is registered yet. Tests stop/reopen the real host with the same PostgreSQL database after a subscriber fails before acknowledgment and verify replay, unchanged fees/version and durable rate events.

## Receipts and debt (T06)

The tuition workspace includes **Thanh toán và công nợ**. Each student shows debt before the selected month, debt in that month, total debt across all periods, and money not allocated. Periods retain their original fees and show **Chưa trả / Trả một phần / Đã trả đủ**. Enter the actual receipt date, a positive whole-dong amount and optional note; history lists each receipt and its period allocations. A 700,000 VND receipt against 500,000 VND old debt and 900,000 VND new fees allocates 500,000/200,000 and leaves 700,000 debt. Changing the selected month changes the old/current split, while total debt still includes all persisted periods.

API: `GET /api/billing/classes/{classId}/payments?month=YYYY-MM` returns the aggregate `version`, projection `status`, total debt and student periods/receipts. `POST /api/billing/classes/{classId}/payments` takes `studentId`, `amountVnd` (positive integer string, up to 18 digits), `receivedOn` (`YYYY-MM-DD`, Vietnam calendar date), optional `note` (1000 characters), a UUID `idempotencyKey`, and the current aggregate `version`. The authenticated teacher is recorded as the actor, separately from the actual receipt date and UTC entry timestamp. Only students belonging to that teacher's class can receive payments; no cross-class balances are transferred. All writes require CSRF and current service write access; expired teachers can still read history.

Receipt, oldest-first allocations, aggregate version and `receipt.recorded` outbox entry commit together under the same PostgreSQL class lock used by pricing and fee projections. A stale version returns 409 without changing balances. An identical idempotency key/payload returns the original receipt even with an old version; reusing the key for different data returns 409. Pending projections or missing prices block new receipts across the class's full history, so allocations never silently use known stale/partial fees. Money stays exact in storage and JSON strings. Excess stays on the receipt as `unallocatedVnd`; it is not automatically applied to later fees, refunded or turned into approved reserve. Prepaid/reserve policy belongs to its own tickets.

`POST /api/billing/classes/{classId}/payments/{receiptId}/corrections` accepts
`action` (`cancel` or `replace`), required `reason` (up to 1000 characters),
`idempotencyKey` and the current payment `version`. Replacement also takes a positive
`amountVnd`, `receivedOn` and optional `note`. The original receipt remains with
`cancelled` or `replaced` status, its original amount/date/allocations, and a correction
containing teacher, edit timestamp, replacement ID and before/after receipt snapshots.
Replacement creates a new active receipt; it can subsequently be corrected itself.
The actual receipt date is independent of the edit timestamp.

Corrections atomically replay the student's active allocations in actual receipt-date
order (entry timestamp and ID break ties), paying the oldest debt first. Existing
unallocated credit stays separate, including a replacement's original surplus (capped
by its corrected amount). Existing period holds remain backed by surviving
historical allocations, up to the remaining backing amount; corrections never silently
spend held money. The correction, allocation changes, version and durable
`receipt.corrected` outbox event commit together. Retries return the saved correction
result even after later changes; conflicting payloads or stale versions return 409.
Pending/missing fees block corrections; `needs-review` permits reversing mistakes
while retaining other holds. Updated versions invalidate old transfer requests and
provide a change signal for future report refresh handling. PDF generation is not yet
implemented. The UI exposes required reasons, before/after history and exact-request
retry after an uncertain response; after reload, check history before entering again.

The UI preserves drafts on validation/conflict/expiry. **Tải lại công nợ** obtains the latest version for a conflict retry. If a server/network failure leaves the save outcome uncertain, the form locks its values and **Thử lại khoản nhận tiền** resends the exact request with its original key/version; an automatic retry never creates a new receipt. This pending request exists only in the current tab; after closing/reloading, inspect receipt history before recording again. QR scans or transfer images never mark tuition paid.

API tests cover the worked debt example, repeat/concurrent requests, stale versions, database failure after saving but before commit, ownership, CSRF, read-only expiry, incomplete fee projections, overpayment and exact large amounts. Playwright exercises the form, per-period statuses, allocation history, reload and a lost response after the server commits on desktop/mobile.

If attendance correction or session cancellation reduces a paid period below its allocations, Billing atomically records a durable hold for the excess. The report returns `needs-review`, exposes `allocationExcessVnd` per student/period and displays **Tiền giữ để đối chiếu**. Held money is excluded from usable payments even if fees later increase again; immutable receipt allocations remain visible in history. New receipts are blocked pending the dedicated financial correction workflow; replaying an already saved receipt remains safe and available. This ticket deliberately provides no automatic release, transfer or refund of held money.

## Manual assessment (T05)

Select a class and use **Đánh giá học tập** to create an assessment with a date and manual grading default. Learning snapshots the students enrolled on that date, creates one submission per assessment–student, and stores data in its own PostgreSQL schema/context. Classroom supplies the roster through `IClassroomLearningSource`; there are no cross-schema reads or foreign keys. AI is visibly unavailable and rejected by the API until the AI slice is implemented.

Each student has independent **Lượng bài hoàn thành** and **Kết quả làm đúng** button groups (25/50/75/100). **Không làm bài** records 0/0; **Không có câu đúng** records a selected completion level and correctness 0. **Không giao bài** and **Chưa đánh giá** keep null values and are excluded from statistics. Empty selections never become zero. Saving a draft preserves the previous confirmed result; confirmation replaces the official result and retains all revisions. History and the selected assessment survive reload. Expired accounts can read saved assessments but cannot create, edit or confirm them.

API: `GET/POST /api/learning/classes/{classId}/assessments`, `GET .../{assessmentId}`, `PUT .../{assessmentId}/submissions/{studentId}`, and `PUT .../{assessmentId}/submissions/{studentId}/method`. Creation takes `title` (160 characters), `date` and `defaultMethod: "manual"`. Result writes take `status` (`assessed`, `not_done`, `no_correct`, `unassigned`, `unassessed`), nullable `completion`/`correctness`, `version` and `confirm`. Method writes take `method: "manual"` and `version`, and are allowed only before the first result write. All writes require CSRF and current write access. Version conflicts return 409 without partially saving history; the UI retains selections and offers to load the latest version before retrying.

Confirmed revisions also serve as a durable outbox for `ILearningResultChangeHandler`. Notifications carry the teacher, class, assessment, student, revision version and result. Delivery retries failed subscribers, orders confirmations per submission and resumes after restart; consumers must deduplicate the event ID. Drafts emit no confirmation notification. No Reporting/AI consumer is supplied by this ticket.

For a separate local API during browser testing, set `API_PROXY_TARGET` before starting Vite (default `http://127.0.0.1:5080`).

## Payment QR (T07)

The payments workspace includes teacher-private recipient settings and a recipient preview. `GET/POST /api/billing/recipient` reads/saves `bankBin`, `bankName`, `accountNo`, `accountName`, `version` (0 initially), and optional `staticQrBase64`. Use a six-digit Vietnamese bank BIN, a 6–19 digit account number and an uppercase unaccented recipient name (5–50 characters). This validates input format; it does not look up account ownership at the bank. Static images must decode as PNG, at most 1 MB and 2048×2048 pixels. A null image preserves the existing image only for unchanged recipient details; an empty string removes it. Changing the recipient removes the old static QR unless a replacement is supplied.

`POST /api/billing/classes/{classId}/transfer-request` takes `studentId`, `month`, the current payments `version` and `recipientVersion`. It uses total outstanding debt across periods, including old debt, and a transfer memo containing the month and student identifier. Zero debt and missing recipient configuration return validation errors; stale versions, incomplete fees and changes during QR generation return 409. Creating or scanning a QR never records a receipt.

The server fetches a PNG using [VietQR Quick Link](https://www.vietqr.io/qr-dong), with an eight-second timeout and bounded image size. The bank account, amount and memo are sent to that provider. Provider failures or invalid images fall back to the teacher's static QR, then complete manual transfer details. Amounts beyond the provider's thirteen-digit limit use the same fallback. Static QR requires manually entering the exact amount and memo; the UI labels each mode and warns when manual entry is required. Browser image failures also try the static image before manual instructions. Visible requests are discarded on local context changes and revalidated every five seconds; failed verification hides the QR. All API responses are private/no-store, owner-scoped and subject to existing role, CSRF and service-access policies. Configuration and transfer generation require write access.

## Cashflow and obligation-period statistics (T10)

Each class's tuition workspace includes **Thống kê thu tiền và công nợ** with two
separate views. **Thực thu theo ngày nhận** filters an inclusive range of actual
Vietnam calendar dates. **Công nợ theo kỳ học** filters the obligation month,
regardless of when payment was received. Receiving 500,000 VND on November 5 for
October debt increases November cashflow by 500,000 and reduces October debt by
500,000; allocation detail is not counted as a second receipt.

`GET /api/billing/classes/{classId}/cashflow?from=yyyy-MM-dd&to=yyyy-MM-dd` returns
active receipt details, total received and unallocated amounts, `timeZone`
(`Asia/Ho_Chi_Minh`), source `version` and projection `status`.
`GET /api/billing/classes/{classId}/period-balances?month=yyyy-MM` returns adjusted
fees, paid amounts, held amounts and outstanding debt for that exact month, with
per-student allocations linking to their receipt IDs and actual receipt dates.
Gross allocated amounts include held funds; paid amounts exclude those holds.

Both reports use one repeatable-read Billing snapshot per request and existing
durable receipts, allocations, adjustments, holds and ledger versions. No new
schema or separate cached aggregate is needed. Replaced/cancelled receipts are
excluded from current totals; their history remains in the payment workspace.
Only active receipt amounts contribute to cashflow, so adjustments, allocations
and eventual reserve redemption cannot count as newly received money. Reserve
redemption itself is a later ticket.

The UI shows applied filters, version and incomplete/held-data notices, remembers
successful filters across reload, and opens receipt/period reconciliation detail.
Reloading or local source changes obtain current statistics; another tab's changes
are visible on manual reload. Failed loads hide unverified totals and preserve
filter inputs. Statistics use the existing teacher ownership/read-access policy,
including read access after service expiry; Admin has no default student access.

## Docker Compose

Copy `.env.example` to `.env` and set a strong `POSTGRES_PASSWORD`. Run `docker compose up --build -d`. UI: http://127.0.0.1:8088. PostgreSQL and session keys use named volumes. `migrate` applies all module migrations before backend starts. For a local demo, provision using the same environment variables:

```powershell
docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Development -e Provision__Email -e Provision__Password -e Provision__Until backend --provision-teacher
```

For HTTPS, set `TLS_CERT_PATH` and `TLS_KEY_PATH` to certificate/key files outside Git and run `docker compose -f compose.yaml -f compose.https.yaml up --build -d`. Visit https://localhost:8443 with a certificate valid for that host. TLS terminates at nginx; backend trusts forwarded protocol only from its configured proxy IP (Compose uses 172.30.110.3 on an isolated subnet). If that subnet conflicts with the host, change the subnet, static addresses and `ReverseProxy__KnownProxy` together. For another ingress topology, configure a trusted proxy address and ensure the immediate proxy sends the real HTTPS protocol; do not accept arbitrary forwarded headers. Production session/antiforgery cookies always carry Secure, HttpOnly and SameSite Strict. Only UI is exposed on loopback by default; backend/database stay private. Protect and back up the Data Protection key volume alongside the database. No infrastructure is deployed by this repository.

## Verification

Fee adjustments are recorded in Billing with direction, an exact positive VND amount,
month, reason, teacher and timestamp. Use `POST /api/billing/classes/{classId}/adjustments`
with `studentId`, `month` (`yyyy-MM`), `direction` (`increase` or `decrease`),
`amountVnd` (a decimal string), `reason`, `idempotencyKey` (UUID), and the current
tuition `version`. The tuition report includes adjustment history and adjusted totals;
its session lines retain their original basis. A reduction cannot exceed that period's
fee obligation. Any payment allocation exceeding the reduced obligation is held for
review, never automatically reused, refunded or converted into reserve. An increase
can create new debt even after full payment. Changes publish a durable
`adjustment.recorded` tuition-version event in the same transaction.

The tuition details panel provides the adjustment form and history. A transport
failure retains the exact request for safe retry, including after collapsing details.
Existing payment/QR version checks consume the updated balance. Apply the Billing
`FeeAdjustments` migration with the normal `--migrate` command before using this flow.

`TEST_DATABASE_CONNECTION` must identify a dedicated PostgreSQL test server with permission to create databases. Tests create random `ekt_test_*` databases, migrate all modules and drop only their own database afterward. They never use an in-memory substitute. All test credentials are generated fixture data, not application accounts.

```powershell
dotnet test tests/ApiTests
cd ui
npm run build
# With the local API running and a provisioned teacher:
# Set DEMO_EMAIL and DEMO_PASSWORD in the test shell.
npm test
```

For the built Compose UI, set `E2E_BASE_URL` to its HTTP/HTTPS URL before `npm test`; this skips starting Vite. With a development self-signed certificate only, set `E2E_IGNORE_HTTPS_ERRORS=true`. Browser traces are disabled to avoid capturing login credentials.

API cases cover ownership including direct-ID read/edit, reload across sessions, CSRF, invalid input, expired/forged/revoked sessions, security stamp change/blocking, idle and absolute expiry, pending/unverified accounts and expired service read-only access. Playwright runs desktop and 390px mobile flows against the real API; Vite starts automatically. Jenkins requires a Linux agent with .NET/Node/Docker and secret-text credentials `ekt-ci-postgres-password` / `ekt-ci-demo-password`; it builds, runs isolated PostgreSQL API tests and browser flows.

## Security and extension boundaries

- Teacher session: idle 2h, absolute 24h. Admin session policy: idle 30m, absolute 8h. GET requests/polling never renew activity; CSRF-validated writes and explicit user input activity do. Logout revokes the server session; account blocking or security-stamp changes invalidate it on the next request.
- Authentication is separate from read/write/AI permission. `IAccountAccess` returns explicit capabilities. ServiceAccess owns the T01 development grants, denies AI, denies pending/unverified users and allows only reading after expiry. Extend that module with approval/period/quota policy in the service-access ticket; this is not a production billing or approval workflow.
- Admin password-only login is denied until its dedicated TOTP workflow exists. Registration, email verification/recovery/change, Admin approval and service quotas remain follow-up tickets; do not open public registration before those are delivered.
- Class input limits: name 120, subject 80, optional description 500 characters (API and UI). Client owner IDs are ignored; ownership comes from the verified session. Direct-ID access to another teacher's class returns 404.
- Migration execution is an explicit operation, not performed on every application startup. Later schema changes must add migrations, not use `EnsureCreated`.
- UI follows the Stitch navy/light palette and Literata/Manrope typography; no fabricated statistics, OAuth, QA panels or persistence promises. Network errors retain form values; expiry retains the draft only in the open tab until login. No authentication material is stored in localStorage.

Official version references: [Npgsql EF 10 release](https://www.npgsql.org/efcore/release-notes/10.0.html), [Identity EF package](https://www.nuget.org/packages/Microsoft.AspNetCore.Identity.EntityFrameworkCore/10.0.13).
