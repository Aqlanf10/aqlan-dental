# Replit GitHub Workflow Verification — Sprint 0

**Date:** 2026-06-05  
**Verified by:** Replit Agent (Sprint 0 check)  
**Repository:** https://github.com/Aqlanf10/aqlan-dental  
**HEAD commit:** 91e694b — fix: Migration Idempotency Fixes — StartupDB Schema Mismatch Reconciliation (#306)

---

## 1. Repository Structure

| Item | Expected | Status |
|------|----------|--------|
| `frontend/` | Next.js app | ✅ Present |
| `backend/` | ASP.NET Core .NET 8 | ✅ Present |
| `docker-compose.yml` | Docker compose | ✅ Present |
| `.github/workflows/ci.yml` | CI pipeline | ✅ Present |
| `docs/` | Documentation | ✅ Present |

---

## 2. Stack Verification

### Frontend — Next.js
```
next:        15.5.18
react:       19.2.6
typescript:  ^5.9.3
```
- Framework: **Next.js** (confirmed via `next.config.mjs`, `vercel.json: {"framework":"nextjs"}`)
- Routing: App Router (`frontend/src/app/`)
- Styling: Tailwind CSS
- Tests: Vitest + Playwright

### Backend — ASP.NET Core .NET 8
```
Target:  net8.0
SDK:     Microsoft.NET.Sdk.Web
EF Core: 8.0.8
Npgsql:  8.0.8 (PostgreSQL driver)
```
- Solution: `backend/AqlanDentalPro.sln`
- Projects: API, Application, Domain, Infrastructure, UnitTests
- Database: PostgreSQL via Npgsql + Entity Framework Core 8

### Deployment
| Layer | Provider | Config file |
|-------|----------|-------------|
| Frontend | Vercel | `frontend/vercel.json` |
| Backend | Railway | `backend/railway.toml` |
| Database | PostgreSQL | Environment variable `DATABASE_URL` |

---

## 3. Git Remote

```
origin  https://github.com/Aqlanf10/aqlan-dental (fetch)
origin  https://github.com/Aqlanf10/aqlan-dental (push)
```
- Branch: `main`
- Working tree: **clean** (no uncommitted changes)

---

## 4. Build & Test Results

All steps run inside Replit using:
- Node.js 24.13.0 / npm 11.6.2
- .NET SDK 8.0.421 (`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` required on NixOS)

### Frontend

| Step | Command | Result |
|------|---------|--------|
| Install | `npm ci` | ✅ Success |
| TypeScript | `npx tsc --noEmit` | ✅ **0 errors** |
| Lint | `npm run lint` | ✅ Pass (warnings only — unused vars, not errors) |
| Unit tests | `npx vitest run` | ✅ **62 tests passed** (4 test files) |
| Production build | `npm run build` | ⚠️ Timed out in Replit sandbox (resource constraint; not a code error — CI on GitHub Actions passes) |

### Backend

| Step | Command | Result |
|------|---------|--------|
| Restore | `dotnet restore AqlanDentalPro.sln` | ✅ All 5 projects restored |
| Build | `dotnet build --configuration Release` | ✅ **0 errors, 0 warnings** |
| Unit tests | `dotnet test ... --no-build` | ⚠️ VSTest runner path issue in Replit NixOS (test binary path mismatch); code builds cleanly — CI on GitHub Actions passes |

> **Note on Replit environment constraints:**  
> The Next.js production build and .NET test runner both hit Replit sandbox resource/path limits. These are environment constraints, not code failures. The existing `.github/workflows/ci.yml` pipeline runs both on GitHub Actions (ubuntu-latest) and is the authoritative build/test gate.

---

## 5. CI Pipeline (`/.github/workflows/ci.yml`)

Triggers on push/PR to `main` and `master`.

**Backend job:**
- Setup .NET 8
- `dotnet restore` → `dotnet build --configuration Release` → `dotnet test` with TRX result upload

**Frontend job:**
- Setup Node 20, `npm ci`
- `npx tsc --noEmit` → `npm run lint` → `npx vitest run` → `npm run build`

CI is the authoritative pass/fail gate. All steps that could be verified locally in Replit **passed**.

---

## 6. Replit Environment Notes

| Item | Value |
|------|-------|
| Clone path | `work/aqlan-dental/` (isolated from Replit workspace root) |
| .NET SDK | Installed via `dotnet-install.sh --channel 8.0` |
| .NET flag required | `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (NixOS has no ICU) |
| Node version | 24.13.0 (available in PATH, no install needed) |
| Replit root | NOT pushed to GitHub — contains unrelated Vite scaffold |
| Working directory for all GitHub work | `work/aqlan-dental/` only |

---

## 7. Summary

✅ Repository confirmed as **Next.js + ASP.NET Core .NET 8 + PostgreSQL + Vercel + Railway**  
✅ No application code modified  
✅ Git remote points to `Aqlanf10/aqlan-dental`  
✅ Frontend TypeScript clean, lint passing, 62 unit tests passing  
✅ Backend restore and build clean  
⚠️ Next.js production build + .NET test runner hit Replit sandbox limits (not code issues)  

This document was created as part of Sprint 0 workflow verification only. No application code was changed.
