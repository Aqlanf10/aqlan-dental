# 012 — Railway Unified Hosting Design

Spec IDs: `DEPLOY-REQ-001..009`

## Current Design Evidence

- `frontend/next.config.mjs` already sets `output: "standalone"` and owns same-origin rewrites for `/api`, `/hubs`, `/uploads`, and `/health`.
- `backend/railway.toml` already builds the backend Dockerfile and checks `/health`.
- `backend/Dockerfile` runs as a non-root user and prepares `/data/uploads` for the existing Railway volume.
- Railway production currently contains separate `aqlan-dental`, PostgreSQL, and Redis services. The backend and database have attached volumes.
- Vercel currently serves the production frontend and remains the rollback path.

## Allowed Files — Foundation Slice

- `docs/governance/MANDATORY_SPRINT_QUEUE.md`
- `specs/012-railway-unified-hosting/*`
- `frontend/Dockerfile`
- `frontend/.dockerignore`
- `frontend/railway.toml`

## Forbidden Files — Foundation Slice

- Backend source and migrations.
- Finance, auth, permissions, patient access, and clinical source.
- `frontend/package.json` and `frontend/package-lock.json`.
- Existing mobile PR branches and mobile configuration.
- Production secrets, variables, domains, database contents, and volume contents.

## Service Topology

| Environment | Service | Responsibility |
|---|---|---|
| Staging | frontend | Next.js standalone server and same-origin proxy |
| Staging | backend | ASP.NET Core API and SignalR |
| Staging | PostgreSQL | Isolated synthetic/test data |
| Staging | Redis | Isolated cache/realtime dependency |
| Production | existing services | Unchanged during this slice |

The Railway frontend sets `BACKEND_URL` to the staging backend's private URL and leaves `NEXT_PUBLIC_API_URL` unset. This keeps browser traffic on the frontend origin while the Next.js server resolves the backend privately. The setting must exist for both build and runtime.

## Container Design

- Node.js 22 Alpine multi-stage image.
- `npm ci` from the committed lockfile.
- `npm run build` in the builder stage.
- Only standalone output, static assets, and public assets copied to the runtime stage.
- Runtime uses an unprivileged `nextjs` user and Railway's injected `PORT`.

## Risks

- A missing build-time `BACKEND_URL` can compile localhost rewrites into the frontend.
- SignalR WebSocket proxying needs runtime verification on Railway.
- Cloning production variables can accidentally connect Staging to production data; database references must be reviewed before the first deploy.
- A separate staging backend/database/Redis/volume adds Railway usage cost.

## Rollback Plan

- Delete or stop only the new staging frontend/service resources if verification fails.
- Revert this PR to remove deployment configuration.
- Production Vercel, Railway backend, databases, volumes, and DNS remain unchanged; no data recovery is required for this foundation slice.
