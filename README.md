# Education Keeptrack — T01 / T02 / T03

Teacher login/logout and owned class creation/listing. .NET 10 modular monolith, ASP.NET Core Identity, EF Core/Npgsql, PostgreSQL 18, React + TypeScript + Vite. Identity, Classroom and the minimal ServiceAccess implementation have independent schema, context and migration history. Classroom only consumes `IAccountAccess`; ServiceAccess consumes Identity's `IAccountState` contract. Neither reads Identity tables directly.

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

API routes below `/api/classes/{classId}`: `GET/PUT /schedule`, `GET/POST /sessions`, `POST /sessions/generate`, `PUT /sessions/{id}`, `GET /sessions/{id}/attendance`, `PUT /sessions/{id}/attendance/{studentId}`, `GET /sessions/{id}/history`, and `GET /attendance-missing`. List/reminder queries require `from`/`to` as `YYYY-MM-DD`; time inputs use `HH:mm:ss`. Schedule slots use `dayOfWeek` 0=Sunday through 6=Saturday. Generation takes `from`, `to`, `scheduleVersion`; manual sessions take `date`, `startTime`, `endTime`, `kind` (`regular`/`makeup`), optional `replacesSessionId`. Session updates take `status` (`planned`/`held`/`cancelled`), optional `reason`, `version`. Attendance updates take `status` (`unmarked`/`present`/`absent`), optional `reason`, `excused`, `version`; an unmarked row without a saved record has version 0. Times and calendar dates use `Asia/Ho_Chi_Minh`; each session additionally stores UTC start/end instants. Cross-midnight slots must be split into separate days.

Session creation/status changes and attendance changes save a versioned history/outbox entry in the source transaction. The background dispatcher delivers `ClassroomChange` through registered `IClassroomChangeHandler` implementations in memory, acknowledging only after all handlers succeed. Failed deliveries remain pending for retry and block later versions of the same session; consumers must deduplicate `EventId` and use source versions because delivery is at least once. Later modules consume this public contract rather than querying Classroom tables. No billing/learning consumers are registered yet.

## Docker Compose

Copy `.env.example` to `.env` and set a strong `POSTGRES_PASSWORD`. Run `docker compose up --build -d`. UI: http://127.0.0.1:8088. PostgreSQL and session keys use named volumes. `migrate` applies all module migrations before backend starts. For a local demo, provision using the same environment variables:

```powershell
docker compose run --rm -e ASPNETCORE_ENVIRONMENT=Development -e Provision__Email -e Provision__Password -e Provision__Until backend --provision-teacher
```

For HTTPS, set `TLS_CERT_PATH` and `TLS_KEY_PATH` to certificate/key files outside Git and run `docker compose -f compose.yaml -f compose.https.yaml up --build -d`. Visit https://localhost:8443 with a certificate valid for that host. TLS terminates at nginx; backend trusts forwarded protocol only from its configured proxy IP (Compose uses 172.30.110.3 on an isolated subnet). If that subnet conflicts with the host, change the subnet, static addresses and `ReverseProxy__KnownProxy` together. For another ingress topology, configure a trusted proxy address and ensure the immediate proxy sends the real HTTPS protocol; do not accept arbitrary forwarded headers. Production session/antiforgery cookies always carry Secure, HttpOnly and SameSite Strict. Only UI is exposed on loopback by default; backend/database stay private. Protect and back up the Data Protection key volume alongside the database. No infrastructure is deployed by this repository.

## Verification

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
