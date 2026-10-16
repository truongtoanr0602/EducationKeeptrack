# T01 implementation plan

Goal: authenticated teachers create and reload their own classes.

Architecture: modular monolith, Identity and Classroom own separate PostgreSQL schemas/contexts. Classroom consumes identity and service access contracts; authentication never grants service access implicitly. React uses same-origin HttpOnly cookies and antiforgery headers. Existing spec #1, ticket #2 and ADRs are the approved design and API testing seam.

Execution: inline using implement and TDD. No external deployment.

- [ ] Identity: write API login/CSRF tests against real PostgreSQL; implement Identity accounts, revocable server sessions, idle/absolute limits and a controlled development provisioning command.
- [ ] Classroom: API tests create/list/get/update with two teachers, unauthorized and forbidden access; implement owned records and read/write/AI access contract with explicit development grants only.
- [ ] UI: React login/logout, class list and accessible creation dialog; common validation limits, saving/error/success states, preserve failed input. Verify reload and mobile flow.
- [ ] Operations: versioned EF migrations, Compose with persistent database/key volumes, same-origin nginx, Jenkins builds/tests with external credentials, README.
- [ ] Run full tests/builds, two-axis code review against initial baseline, fix findings and commit only task changes.

Files: src/Modules/Identity (domain/application/infrastructure/endpoints), src/Modules/Classroom (domain/application/infrastructure/endpoints), src/Contracts (public boundaries), src/Host (composition). tests/ApiTests tests public HTTP behavior with isolated PostgreSQL databases and controlled time. ui/src contains React components and styles; ui/e2e verifies the user flow. Infrastructure files at root.

Versions: .NET/EF/Identity 10.0.13 with SDK 10.0.402, Npgsql EF 10.0.3, PostgreSQL 18; React/TypeScript/Vite resolved and locked by npm. Provider compatibility checked against official NuGet metadata.
