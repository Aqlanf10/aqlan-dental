# 012 — Railway Unified Hosting Tasks

- [x] `DEPLOY-TASK-001` Inspect current GitHub main, open PRs, CI/deployment files, and live Railway topology without changing production.
- [x] `DEPLOY-TASK-002` Record the owner priority override and create requirements/design/tasks for the hosting migration.
- [x] `DEPLOY-TASK-003` Add a production-grade standalone Next.js Dockerfile, Docker ignore rules, and Railway healthcheck configuration.
- [x] `DEPLOY-TASK-004` Build and run the frontend standalone artifact; Next.js generated 95/95 pages and `/login` returned 200 locally. The proxied `/health` could not be verified in the isolated runner because external DNS resolution returned `EAI_AGAIN`; backend `/health` was independently verified as 200.
- [ ] `DEPLOY-TASK-005` Create isolated Railway Staging services and variables without copying production data connections. Requires runtime confirmation because it adds billable resources.
- [ ] `DEPLOY-TASK-006` Verify `/login`, `/health`, same-origin API, authentication, portal refresh, SignalR, PDFs, and protected uploads using synthetic staging data.
- [ ] `DEPLOY-TASK-007` Open a small PR with CI evidence, runtime evidence, risks, rollback, and the drift checklist. Do not merge or cut over production.
