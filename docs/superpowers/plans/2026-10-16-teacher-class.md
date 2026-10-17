# T01 implementation plan

Goal: authenticated teachers create and reload their own classes.

Architecture: modular monolith, Identity and Classroom own separate PostgreSQL schemas/contexts. Classroom consumes identity and service access contracts; authentication never grants service access implicitly. React uses same-origin HttpOnly cookies and antiforgery headers. Existing spec #1, ticket #2 and ADRs are the approved design and API testing seam.

Execution: inline using implement and TDD. No external deployment.

- [x] Identity: API login/CSRF tests against real PostgreSQL; Identity accounts, revocable server sessions, idle/absolute limits and a controlled development provisioning command.
- [x] Classroom: API create/list/get/update with two teachers, unauthorized and forbidden access; owned records and read/write/AI access contract with explicit development grants only.
- [x] UI: React login/logout, class list and accessible creation dialog; common validation limits, saving/error/success states, failed input retention, reload and mobile verification.
- [x] Operations: versioned EF migrations, Compose persistent database/key volumes, same-origin nginx, Jenkins configuration with external credentials, README. Jenkins server execution remains unverified.
- [x] Full tests/builds and two-axis review completed; findings fixed. Commit contains only task files. Evidence: docs/verification/T01.md.

Files: src/Modules/Identity (domain/application/infrastructure/endpoints), src/Modules/Classroom (domain/application/infrastructure/endpoints), src/Contracts (public boundaries), src/Host (composition). tests/ApiTests tests public HTTP behavior with isolated PostgreSQL databases and controlled time. ui/src contains React components and styles; ui/e2e verifies the user flow. Infrastructure files at root.

Versions: .NET/EF/Identity 10.0.13 with SDK 10.0.402, Npgsql EF 10.0.3, PostgreSQL 18; React/TypeScript/Vite resolved and locked by npm. Provider compatibility checked against official NuGet metadata.

Review ruling: the initial Identity-local development-grant adapter violated ADR-0003. Move grants/policy into a real, minimal ServiceAccess schema/context; Identity exposes only an account-state contract. No registration, approval, period or quota workflows are added. Fix production HTTPS with a trusted proxy and an explicit TLS Compose override; fix Jenkins teardown credential scope.
