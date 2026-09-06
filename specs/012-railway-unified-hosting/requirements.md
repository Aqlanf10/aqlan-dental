# 012 — Railway Unified Hosting Requirements

- Status: Active — staging foundation
- Owner directive: 2026-09-06
- Scope: host the Next.js frontend beside the existing ASP.NET Core backend on Railway without changing production traffic.

## Requirements

- `DEPLOY-REQ-001` Railway SHALL run the frontend, backend, PostgreSQL, and Redis as separate services rather than one combined container.
- `DEPLOY-REQ-002` A Railway Staging environment SHALL be created and verified before production DNS, Vercel, or the current production services are changed.
- `DEPLOY-REQ-003` The frontend SHALL use the existing Next.js standalone output and a non-root production container.
- `DEPLOY-REQ-004` Staging browser requests SHALL remain same-origin through Next.js rewrites. `NEXT_PUBLIC_API_URL` SHALL be unset for the Railway frontend and `BACKEND_URL` SHALL identify the backend service. The value SHALL be available during the frontend build because Next.js compiles rewrites at build time.
- `DEPLOY-REQ-005` Staging SHALL use an isolated PostgreSQL database and synthetic data. It SHALL NOT run tests that create, edit, or delete production patient or financial records.
- `DEPLOY-REQ-006` Secrets, connection strings, tokens, passwords, and environment values SHALL NOT be committed to Git, logs, screenshots, specs, or PR text.
- `DEPLOY-REQ-007` The frontend SHALL expose a healthcheck independent of authenticated application state. The initial healthcheck path is `/login`; backend health remains `/health`.
- `DEPLOY-REQ-008` Vercel and the current production domain SHALL remain available as rollback paths until the owner approves production cutover after runtime verification.
- `DEPLOY-REQ-009` Uploaded files SHALL keep using the existing production Railway volume. Staging uploads require a separate temporary volume; object-storage migration is explicitly deferred.

## Acceptance Criteria

- WHEN the frontend container is built THEN it SHALL produce and run `.next/standalone/server.js` as a non-root user.
- WHEN Railway checks the frontend THEN `/login` SHALL return a healthy response without requiring credentials.
- WHEN the staging frontend calls `/api/*`, `/hubs/*`, or `/uploads/*` THEN the request SHALL be proxied to the configured staging backend without exposing an internal Railway hostname to the browser.
- WHEN staging E2E runs THEN it SHALL use only staging services and synthetic credentials/data.
- WHEN staging deployment fails THEN production, Vercel, and production DNS SHALL remain unchanged.

## Runtime Verification

Needs runtime verification: Railway private networking, SignalR upgrade behavior through the Next.js service, authenticated staff and portal refresh flows, PDF downloads, and protected uploads.
