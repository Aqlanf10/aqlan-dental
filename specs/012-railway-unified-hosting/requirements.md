# 012 — Railway Unified Hosting Requirements

- Status: Active — direct deployment in the existing production environment.
- Owner directive: 2026-09-06; the owner explicitly cancelled Staging and requested direct work.
- Scope: add the Next.js frontend beside the existing ASP.NET Core backend on Railway.

## Requirements

- `DEPLOY-REQ-001` Frontend, backend, PostgreSQL and Redis SHALL remain separate services.
- `DEPLOY-REQ-002` No Staging environment SHALL be created. Add only a frontend service to the existing aqlan-dental-pro production environment.
- `DEPLOY-REQ-003` Use Next.js standalone output and a non-root container. Declare BACKEND_URL as a builder-stage ARG and fail the build if absent, preventing localhost rewrites.
- `DEPLOY-REQ-004` Browser API, hub and upload requests SHALL remain same-origin. Leave NEXT_PUBLIC_API_URL unset. BACKEND_URL targets the existing backend and is supplied at build and runtime.
- `DEPLOY-REQ-005` Existing PostgreSQL and Redis SHALL be reused without changing their connections or data. Synthetic write tests SHALL run only in isolated CI, never against production records.
- `DEPLOY-REQ-006` Secrets SHALL NOT be committed or printed. The frontend requires no database, JWT or backend credentials.
- `DEPLOY-REQ-007` Frontend healthcheck SHALL use /login; verify the proxied backend /health separately.
- `DEPLOY-REQ-008` Keep Vercel and existing DNS available during verification and rollback. Deployment from PR #839 does not authorize automatic merging of it or unrelated PRs.
- `DEPLOY-REQ-009` Preserve existing backend and database volumes. Object storage migration is deferred.

## Acceptance Criteria

- The container builds and runs standalone as a non-root user.
- /login responds successfully and /health reaches the existing backend.
- /api, /hubs and /uploads proxy to the configured backend; protected routes preserve authorization.
- Failed frontend deployment does not replace Vercel or modify existing backend/database services.
- Report authentication, SignalR, PDF and upload verification individually; a login-page 200 alone does not prove readiness.

Needs runtime verification: authenticated staff/portal refresh, SignalR upgrades, PDFs and protected uploads.
