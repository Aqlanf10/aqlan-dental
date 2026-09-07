# 012 — Railway Unified Hosting Tasks

- [x] `DEPLOY-TASK-001` Inspect main, PR #839, CI and existing Railway production topology.
- [x] `DEPLOY-TASK-002` Record owner cancellation of Staging and update requirements/design/queue.
- [x] `DEPLOY-TASK-003` Prepare non-root standalone Dockerfile, ignore rules and healthcheck; declare mandatory build-time BACKEND_URL.
- [x] `DEPLOY-TASK-004` Updated Next.js production build passed, including TypeScript/lint; all four compiled rewrites target the existing backend. Previous foundation: 752 frontend tests, TypeScript/lint/build and standalone /login passed; CI passed on 638878d.
- [x] `DEPLOY-TASK-005` Created frontend service fa256133-9e4e-454d-a998-4b4dd4adf032 in the existing production environment; no additional database/backend/Redis. Public endpoint: https://frontend-production-3612.up.railway.app (replaces the nonworking initial domain). Build/deploy verification remains below.
- [ ] `DEPLOY-TASK-006` Verify /login, /health, API and separately document authenticated/SignalR/PDF/upload coverage without synthetic writes to production.
- [x] `DEPLOY-TASK-007` Update PR #839 with current evidence and remaining work; do not auto-merge.

The prior PR queue blob was not valid UTF-8. Restore its readable contents from the preserved local foundation, retaining the original backlog and updating only DEPLOY-RAILWAY-01.

Runtime build corrections: new service initially built main instead of the configured branch; an actual new deployment picked up the branch, unlike redeploy which reused the previous source snapshot. Select frontend/Dockerfile explicitly and copy the existing .npmrc before npm ci. Runtime source commit: ff19557d5d85af62c3c7dc0d58ead180666fd24a.

## Runtime evidence — 2026-09-07

- Source ff19557: GitHub CI run 34054993316 and Encoding Guard 34054993319 succeeded.
- Railway deployment 0413a33a-89a1-497e-aef6-d72ad8d04f0d succeeded; /login internal healthcheck passed; Next.js listens on 0.0.0.0:3000.
- Follow-up redeploy 577bc73b-f8f4-4d5d-94b1-cddea7ddcd69 also succeeded.
- Public frontend /login still returns HTTP 404 with x-railway-fallback: true; browser shows Railway Not Found, not the Next.js page. Frontend HTTP logs contain no routed requests.
- Domain d6c981ec-5930-4757-96c9-e97bfbecb8e3 is listed on the correct service/environment, target port 3000. Applying staged changes returned nothing_staged; configuration is already live.
- Existing backend /health returned HTTP 200 independently. Backend/PostgreSQL/Redis deployment IDs remain unchanged.
- Blocker: Railway public-domain routing. Available connector can create a domain or return an existing domain, but has no operation to update/delete/recreate an existing generated domain. Do not invent a custom domain or claim the migration is complete.
- Next operation: inspect/repair or regenerate only the frontend generated domain in Railway Networking, then verify /login, proxied /health and API, followed by authenticated UI/SignalR/PDF/upload checks. Do not modify database services or existing backend domain.
- Authentication, PDF, uploads and SignalR remain unverified. No synthetic production patient/financial records were created. Vercel and DNS retained; PR remains unmerged.

## Public routing repaired — follow-up 2026-09-07

The blocker above is resolved. Railway networking removal was committed separately through its infrastructure tool. The config view still displayed the old domain, but the domains API correctly returned an empty serviceDomains list. Calling generate_domain then created frontend-production-3612.up.railway.app on port 3000 (domain ID d70b9168-c2f6-47ea-91fd-f2bc21184e09). Do not use the former 72e1 hostname.

Deployment 93449253-58ae-46a0-a179-d656868257ee is SUCCESS on 1a298292 (documentation-only change after tested runtime ff19557). Backend/database/Redis stayed on their prior deployments. Public GET /login returns 200; browser renders both staff and patient sign-in forms and clinic branding. No application console error observed; browser-extension metadata errors are unrelated.

Authenticated workflow checks remain open; public availability does not prove patient/finance/PDF/SignalR workflows. No PR merge or Vercel shutdown performed.
