# 012 — Railway Unified Hosting Design

Spec IDs: `DEPLOY-REQ-001..009`

## Owner decision

The owner cancelled the earlier Staging requirement on 2026-09-06 and authorized direct deployment in the existing Railway project. Preserve existing services, data, volumes, Vercel and DNS.

## Allowed files

- docs/governance/MANDATORY_SPRINT_QUEUE.md
- specs/012-railway-unified-hosting/*
- frontend/Dockerfile, frontend/.dockerignore, frontend/railway.toml

Forbidden: backend source/migrations, finance/auth/permissions/patient-access source, package manifests/lockfiles, mobile branches, secrets and database contents.

## Topology

| Environment | Service | Action |
|---|---|---|
| production | frontend | Add Next.js standalone service from PR #839 branch |
| production | aqlan-dental | Reuse existing API |
| production | postgres | Reuse existing database and volume |
| production | redis | Reuse existing cache |

Live build root: /frontend; Dockerfile path: frontend/Dockerfile relative to the repository. RAILWAY_DOCKERFILE_PATH explicitly selects this file. Railway rejected selecting railway.toml as deprecated, so live healthcheck/build settings were configured on the frontend service through the Railway API; the initial TOML is not selected as the live config. Builder ARG BACKEND_URL must be nonempty. NEXT_PUBLIC_API_URL remains unset to preserve same-origin cookies and API requests. The first deployment may use the existing backend public HTTPS endpoint; private networking is an optional later optimization after its port and reachability are verified. No backend secrets are copied.

## Container

Node 22 Alpine multi-stage; copy the existing .npmrc before npm ci so the lockfile peer-dependency mode is preserved; Next.js build; standalone/public/static copied to a non-root runner. Railway PORT and HOSTNAME=0.0.0.0. Healthcheck /login plus separate API verification.

## Risks and rollback

- Docker builds require ARG to receive Railway variables: https://docs.railway.com/builds/dockerfiles#using-variables-at-build-time
- Missing BACKEND_URL must fail before compilation. Updating it requires rebuilding compiled rewrites.
- Authentication cookies, SignalR and protected downloads need live verification. Do not weaken authorization to pass checks.
- Production data is shared: write tests stay in isolated CI.
- A frontend service adds resource usage; no duplicate database/backend/Redis is provisioned.
- If frontend verification fails, retain Vercel and correct or stop only the new frontend deployment. Existing DNS, backend and volumes remain intact.
