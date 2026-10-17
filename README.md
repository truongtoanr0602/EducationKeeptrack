# Education Keeptrack — T01

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
