# 012 — Railway Unified Hosting Tasks

- [x] `DEPLOY-TASK-001` Inspect main, PR #839, CI and existing Railway production topology.
- [x] `DEPLOY-TASK-002` Record owner cancellation of Staging and update requirements/design/queue.
- [x] `DEPLOY-TASK-003` Prepare non-root standalone Dockerfile, ignore rules and healthcheck; declare mandatory build-time BACKEND_URL.
- [ ] `DEPLOY-TASK-004` Verify updated build and compiled rewrites. Previous foundation: 752 frontend tests, TypeScript/lint/build and standalone /login passed; CI passed on 638878d.
- [ ] `DEPLOY-TASK-005` Add frontend to existing production environment with the existing backend; no additional database/backend/Redis.
- [ ] `DEPLOY-TASK-006` Verify /login, /health, API and separately document authenticated/SignalR/PDF/upload coverage without synthetic writes to production.
- [ ] `DEPLOY-TASK-007` Update PR #839 with current evidence and remaining work; do not auto-merge.

The prior PR queue blob was not valid UTF-8. Restore its readable contents from the preserved local foundation, retaining the original backlog and updating only DEPLOY-RAILWAY-01.
