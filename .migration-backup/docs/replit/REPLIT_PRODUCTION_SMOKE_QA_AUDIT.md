# Sprint 1.5 — Production Smoke Test and Manual QA Audit

**Repository:** Aqlanf10/aqlan-dental  
**Audit date:** 2026-06-05  
**Auditor:** Replit Agent (automated curl + source-code cross-reference)  
**Branch:** qa/production-smoke-audit (created from main @ `2979749b35`)  
**Sprint trigger:** After PR #309 merged (2026-06-05T07:00:51Z)

---

## 1. Executive Summary

The production system is **partially functional**. The backend is alive and auth middleware is working correctly. All 12 previously-failing endpoints now return **401** for unauthenticated requests — confirming that auth runs before any DB query. However, the Sprint 1 enum migration deployment status **cannot be confirmed** from unauthenticated curl alone; authenticated smoke testing is required to prove the DB queries succeed.

Four additional backend routes that exist in source code return **404** in production, which means the Daily Operations dashboard, Messaging page, and Commissions section are completely broken for all authenticated users. Three of these are confirmed P1 bugs.

The frontend loads correctly. All auth-protected pages redirect to login. The two public display pages (`/clinic-display`, `/queue-display`) load without auth.

**Immediate action needed before next clinic session:**
1. Trigger a verified Railway redeploy to confirm Sprint 1 migration runs
2. Add missing `[Route]` attribute to `DailyOperationsController`
3. Investigate and fix `/api/messages` and `/api/commissions` 404s

---

## 2. Environment Tested

| Item | Value |
|---|---|
| Frontend URL | `https://aqlan-dental.vercel.app/` |
| Backend URL | `https://aqlan-dental-production.up.railway.app/` |
| Backend host | Railway (Singapore region — `railway-hikari / sin1.nzn2`) |
| Frontend host | Vercel (Next.js) |
| Health endpoint | `GET /health` → 200 |
| Health body | `{"status":"healthy","timestamp":"2026-06-05T07:03:52Z","version":"2026.05.28-finance-live"}` |
| Version string | ⚠️ **HARDCODED in Program.cs** — not from git; does NOT reflect deployed commit |
| Test method | Unauthenticated `curl` + source-code cross-reference |

---

## 3. Commit / Main Version Tested

| Item | Value |
|---|---|
| Main HEAD at audit time | `2979749b35fb998d77fab344415ec745e6d13c02` |
| PR #309 (GetTodayQueue + ILogger fix) | Merged 2026-06-05T07:00:51Z ✅ |
| Sprint 1 Phase 2 migration in source | `20260620000000_Sprint1_FixEnumColumnTypesPhase2.cs` ✅ |
| Sprint 1 deployed to Railway | **UNCONFIRMED** — health version string is static, Railway deploy log not accessible |
| Last known Railway deploy tag | `stable-after-lockout-timezone-fix-2026-05-25` (git tag → `f75f5a2000`) |

> **Critical note:** The `/health` version string `2026.05.28-finance-live` is a hardcoded literal in `Program.cs` line 74. It is NOT updated automatically from git. Consequently, it is **impossible** to determine the actual deployed commit from the health endpoint alone. If Railway does not auto-deploy on push, the production binary may pre-date the Sprint 1 migration.

---

## 4. Account Roles Tested

Automated curl testing was unauthenticated only (no production credentials held by auditor). All authenticated endpoint results are inferred from HTTP status codes and source-code review.

| Role | Username pattern | Auth tested |
|---|---|---|
| Admin | admin (configured via seed) | ❌ Not available to auditor |
| Reception | reception user | ❌ Not available to auditor |
| Accountant | accountant user | ❌ Not available to auditor |
| Doctor | doctor user | ❌ Not available to auditor |
| Patient Portal | patient self-registration | ❌ Not available to auditor |

**Recommendation:** Clinic admin should perform a manual authenticated smoke test using the checklist in Section 8.

---

## 5. Smoke Test Result Table

### 5A — Backend Core Health

| Check | URL | Result | Notes |
|---|---|---|---|
| Backend alive | `GET /health` | ✅ 200 | `status: healthy` |
| CORS (Vercel origin) | `GET /health` w/ Origin header | ✅ Correct | `access-control-allow-origin: https://aqlan-dental.vercel.app` |
| Auth middleware running | Any protected endpoint | ✅ 401 | Returns before hitting DB |
| Login (wrong creds) | `POST /api/auth/login` | ✅ 401 | Arabic error: `اسم المستخدم أو كلمة المرور غير صحيحة` |
| Refresh token (no cookie) | `POST /api/auth/refresh-token` | ✅ 401 | Arabic: `لا يوجد refresh token` |
| HSTS | Any endpoint headers | ✅ Present | `max-age=31536000; includeSubDomains` |
| Security headers | Any endpoint headers | ✅ Present | CSP, X-Frame-Options, nosniff, Permissions-Policy |
| Info disclosure | `server:` header | ✅ Safe | `railway-hikari` only; no ASP.NET version |

### 5B — Frontend Pages (HTTP Status)

| Page | URL | Status | Notes |
|---|---|---|---|
| Root | `/` | ✅ 307 | Redirect to login (unauthenticated) |
| Login | `/login` | ✅ 200 | Loads correctly |
| Dashboard | `/dashboard` | ✅ 307 | Redirect to login |
| Patients | `/patients` | ✅ 307 | Redirect to login |
| Appointments | `/appointments` | ✅ 307 | Redirect to login |
| Clinic Queue | `/clinic-queue` | ✅ 307 | Redirect to login |
| Clinic Display | `/clinic-display` | ✅ 200 | Public — loads without auth |
| Queue Display | `/queue-display` | ✅ 200 | Public — loads without auth |
| Finance V3 | `/finance-v3` | ✅ 307 | Redirect to login |
| Inventory | `/inventory` | ✅ 307 | Redirect to login |
| Employees | `/employees` | ✅ 307 | Redirect to login |
| Lab | `/lab` | ✅ 307 | Redirect to login |
| Messages | `/messages` | ✅ 307 | Redirect to login |
| WhatsApp | `/whatsapp` | ✅ 307 | Redirect to login |
| Settings / Users | `/settings/users` | ✅ 307 | Redirect to login |
| Patient Journey | `/patient-journey` | ✅ 307 | Redirect to login |
| Daily Operations | `/daily-operations` | ✅ 307 | Redirect to login (backend 404 — see §7) |

---

## 6. Previously Failing Endpoints Status

These 12 endpoints were documented as returning **500** in the Sprint 0 audit due to enum column type mismatch (integer in DB vs `HasConversion<string>()` in EF Core).

| Endpoint | Sprint 0 Status | Sprint 1.5 Status | Authenticated status |
|---|---|---|---|
| `GET /api/finance-v3/dashboard` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** — needs auth test |
| `GET /api/patient-journey/today` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/clinic-queue/today` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/inventory` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/suppliers` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/purchase-orders` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/employees` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/salaries` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/leaves` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/attendance` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/advances` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |
| `GET /api/whatsapp/dashboard` | ❌ 500 | ✅ 401 (unauth) | **Unconfirmed** |

**Key observation:** All 12 endpoints return 401, confirming auth middleware fires **before** any EF Core query reaches the DB. The 500 errors from Sprint 0 occurred because those users were authenticated. Without an authenticated test session, the true post-migration status cannot be verified via automated curl.

**Required verification:** Clinic admin logs in, navigates to each of these pages, and confirms they render data (not an Arabic error toast).

---

## 7. Broken Pages (Confirmed)

### 7A — Daily Operations Dashboard (`/daily-operations`)

- **Frontend:** Loads page shell (307 → login → dashboard layout)
- **Backend:** `GET /api/daily-operations` → **404**
- **Root cause:** `DailyOperationsController` in source has `[ApiController]` and `[Authorize]` class attributes but **no `[Route("api/daily-operations")]` class-level attribute**. All action methods will therefore not be routed.
- **Severity:** P1 — Daily operations report is completely broken for all staff
- **Fix:** Add `[Route("api/daily-operations")]` to the controller class declaration

### 7B — Messages Page (`/messages`)

- **Frontend:** Loads page shell
- **Backend:** `GET /api/messages` → **404**
- **Root cause:** `MessagesController` exists in source with `[Route("api/messages")]`, but the route is not responding in production. This suggests either: (a) the production binary pre-dates the controller's addition to the build, or (b) a registration/DI error preventing the controller from loading.
- **Severity:** P1 — In-clinic messaging is completely broken
- **Note:** `MessagesController.cs` was first committed 2026-05-23 (before the May 28 tag), so this is unexpected.

### 7C — Commissions Section in Finance V3 (`/finance-v3` → commissions tab)

- **Frontend:** Finance V3 page loads; commissions tab may fail to render data
- **Backend:** `GET /api/commissions` → **404**
- **Root cause:** `CommissionsController` exists in source with `[Route("api/commissions")]` but returns 404 in production. Same ambiguity as messages — either deployment or registration issue.
- **Severity:** P1 — Doctor commissions reporting is broken

### 7D — Backup Endpoint (`/api/backup`)

- **Backend:** `GET /api/backup` → **404**
- **Note:** BackupController exists in source. Lower severity as backup is not a daily-use feature.
- **Severity:** P2

---

## 8. Broken User Flows (Requiring Authenticated Verification)

These flows CANNOT be confirmed working without authenticated access. They are listed as requiring manual verification:

| Flow | Endpoint(s) | Risk |
|---|---|---|
| Staff login → finance dashboard loads | `GET /api/finance-v3/dashboard` | P0 if still 500 |
| Reception → today's queue | `GET /api/clinic-queue/today` | P0 if still 500 |
| Reception → patient journey today | `GET /api/patient-journey/today` | P0 if still 500 |
| Accountant → inventory list | `GET /api/inventory` | P1 |
| HR → employee list | `GET /api/employees` | P1 |
| HR → salaries / attendance / leaves | `GET /api/salaries`, `/api/attendance`, `/api/leaves` | P1 |
| Receptionist → send message | `POST /api/messages` (route 404) | P1 broken |
| Admin → daily operations report | `GET /api/daily-operations` (404) | P1 broken |
| Finance → commission report | `GET /api/commissions` (404) | P1 broken |
| Patient portal → self-register/view | `GET /api/patient-portal/me` (404) | P2 |

---

## 9. Failed API Endpoints

| Endpoint | Method | Status | Source exists? | Severity | Root cause |
|---|---|---|---|---|---|
| `/api/daily-operations` | GET | 404 | ✅ Yes | P1 | Missing class `[Route]` attribute |
| `/api/messages` | GET | 404 | ✅ Yes | P1 | Deployment/registration issue |
| `/api/commissions` | GET | 404 | ✅ Yes | P1 | Deployment/registration issue |
| `/api/backup` | GET | 404 | ✅ Yes | P2 | Deployment/registration issue |
| `/api/patient-portal/me` | GET | 404 | Likely | P2 | Route not confirmed in source |
| `/api/dashboard` | GET | 404 | No | Info | Correct path is `/api/finance-v3/dashboard` |
| `/api/lab` | GET | 404 | No | Info | Correct path is `/api/lab-orders` |
| `/api/sms` | GET | 404 | Partial | Info | Sub-paths like `/api/sms/dashboard` work (401) |

---

## 10. Console Errors

Automated curl testing does not produce browser console errors. Manual browser testing was not performed (no credentials). Based on the 404 routes identified above, the following console errors are **expected** when an authenticated user visits the indicated pages:

| Page | Expected console error | Source |
|---|---|---|
| `/daily-operations` | `404 GET /api/daily-operations` | Missing `[Route]` on controller |
| `/messages` | `404 GET /api/messages` | Route not responding |
| `/finance-v3` (commissions tab) | `404 GET /api/commissions` | Route not responding |
| Any page using `/api/notifications` | Should be 401 (route works) | n/a |

---

## 11. Security Observations

### 11A — CSP `'unsafe-inline'` and `'unsafe-eval'` (P2)

**Header observed on all responses:**
```
content-security-policy: default-src 'self'; 
  script-src 'self' 'unsafe-inline' 'unsafe-eval' https://www.google.com https://www.gstatic.com;
  ...
```
`'unsafe-inline'` allows inline `<script>` tags and event handlers. `'unsafe-eval'` allows `eval()`. Both significantly weaken XSS protection. **Recommendation:** Replace with nonces or hashes for `script-src`. This requires Next.js CSP nonce configuration on Vercel.

### 11B — Hardcoded Version String (P2)

`Program.cs` line 74 hardcodes `version = "2026.05.28-finance-live"`. This makes the health endpoint unreliable for deployment diagnostics. **Recommendation:** Set the version from an environment variable (`VERSION` or `RAILWAY_GIT_COMMIT_SHA`) at deploy time.

### 11C — 401 Empty Body (P3 / UX)

Unauthenticated requests to protected endpoints return `HTTP 401` with **empty body** (`content-length: 0`). The refresh-token endpoint correctly returns `{"message":"لا يوجد refresh token"}`, but other 401s have no body. **Recommendation:** Add a consistent 401 response body via `AddAuthentication().AddJwtBearer(opt => opt.Events.OnChallenge = ...)` to return `{"message":"غير مصرح"}`.

### 11D — Positive Security Findings ✅

| Item | Status |
|---|---|
| CORS restricted to `aqlan-dental.vercel.app` | ✅ Correct |
| HSTS: `max-age=31536000; includeSubDomains` | ✅ Present |
| `X-Frame-Options: SAMEORIGIN` | ✅ Present |
| `X-Content-Type-Options: nosniff` | ✅ Present |
| `Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()` | ✅ Present |
| No `X-Powered-By` header | ✅ Absent (good) |
| Server header shows only `railway-hikari` | ✅ No framework info |
| Login brute force protection | ✅ Rate limiting middleware registered |
| Refresh token in HttpOnly cookie | ✅ Cookie-based pattern |
| Auth error messages are generic | ✅ No username enumeration |

### 11E — Patient Name Visibility on Public Display (P2)

`GET /clinic-display` and `/queue-display` are publicly accessible (200, no auth). Based on source code (`ClinicQueueController.GetDisplay`), these pages display patient full names visible to anyone in the waiting room. This is expected clinic behavior but should be documented:
- Patient full name is visible on the public TV screen
- No patient-level consent toggle exists in the current codebase
- **Recommendation:** Add an option to show only patient number or first name on public display, configurable per-clinic in Settings.

---

## 12. Data Safety Observations

| Observation | Status |
|---|---|
| No production data was read | ✅ All requests unauthenticated |
| No production data was modified | ✅ No POST/PUT/DELETE issued |
| No real WhatsApp/SMS messages sent | ✅ Not attempted |
| No test patients created | ✅ Not attempted |
| No DB schema changes made | ✅ Audit only |
| No production settings changed | ✅ Not attempted |

---

## 13. Priority Table (P0 / P1 / P2)

| ID | Priority | Description | Blocks clinic use? | Recommended action |
|---|---|---|---|---|
| B-01 | **P0** | Sprint 1 enum migration deployment **UNCONFIRMED** — if not applied, all 12 previously-failing endpoints still return 500 for authenticated users | YES — entire finance/HR/queue/inventory blocked | Trigger verified Railway redeploy of main; confirm with authenticated test |
| B-02 | **P1** | `DailyOperationsController` missing class-level `[Route]` attribute → `/api/daily-operations` always 404 | YES — daily ops dashboard broken | Add `[Route("api/daily-operations")]` to controller class |
| B-03 | **P1** | `/api/messages` → 404 in production despite `MessagesController` existing in source | YES — in-clinic messaging broken | Investigate if controller is missing from published binary; re-deploy |
| B-04 | **P1** | `/api/commissions` → 404 in production despite `CommissionsController` existing in source | Partial — commissions tab in Finance V3 broken | Same as B-03 |
| B-05 | **P1** | Version string hardcoded — cannot determine deployed commit from health endpoint | Diagnostic only | Set `version` from `$RAILWAY_GIT_COMMIT_SHA` env var |
| S-01 | **P2** | CSP `'unsafe-inline'` + `'unsafe-eval'` in `script-src` — weakens XSS protection | No | Migrate to CSP nonces in Next.js |
| S-02 | **P2** | Patient full name visible on unauthenticated public display screens | Low — by design, but privacy risk | Add first-name-only or patient-number display option in Settings |
| U-01 | **P2** | 401 responses have empty body — no Arabic error message | No — UX only | Add 401 response body via JWT challenge event |
| B-06 | **P2** | `/api/backup` → 404 in production | No | Investigate deployment/registration |
| U-02 | **P3** | `/api/patient-portal/me` → 404 — patient portal self-service broken if route unregistered | Partial | Confirm patient portal controller route |

---

## 14. Recommended Next Sprint Scope

### Sprint 2A — Deployment Verification (Immediate — before next clinic session)

1. **Trigger a forced Railway redeploy** of the current `main` branch (commit `2979749b35`) and confirm the deployment completes successfully.
2. **Replace the hardcoded version string** in `Program.cs` with `Environment.GetEnvironmentVariable("RAILWAY_GIT_COMMIT_SHA") ?? "dev"` so every deployment is traceable.
3. **Perform authenticated smoke test** — clinic admin logs in and visits: finance dashboard, patient journey, clinic queue, inventory, employees. Confirm all return data (not 500 or error toast).
4. If any previously-failing endpoint still returns 500 for authenticated users: confirm EF Core migration was applied (`SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'Appointments'` — `Status` column must be `varchar`, not `integer`).

### Sprint 2B — Route Bugs (High priority, 1–2 hours)

5. **Fix `DailyOperationsController`** — add `[Route("api/daily-operations")]` to the class. This is a one-line fix (B-02).
6. **Investigate `CommissionsController` and `MessagesController` 404s** — check if the controllers are excluded from the published build (`Dockerfile`, `.csproj` includes), or if there is a DI/service registration error preventing them from loading.
7. **Verify `PatientPortalController` route** — confirm `/api/patient-portal/me` is reachable for authenticated patient portal users.

### Sprint 2C — Security Hardening (Medium priority)

8. **CSP nonces for Next.js** — implement `nonce`-based CSP via Next.js middleware (`next.config.js` + `middleware.ts`) to remove `'unsafe-inline'` and `'unsafe-eval'`.
9. **Consistent 401 response body** — add `OnChallenge` handler to JWT bearer options to return `{"message":"غير مصرح"}` on all 401 responses.

### Sprint 2D — UX / Observability (Lower priority)

10. **Public display name privacy** — add a clinic setting to show patient number only (not full name) on `/clinic-display` and `/queue-display`.
11. **Health endpoint versioning** — use git commit SHA from environment variable.
12. **Railway log streaming review** — now that `Console.WriteLine` in `AuthController` is replaced with `logger.LogError` (PR #309), verify Serilog sink is forwarding to Railway log stream correctly.

---

*End of Sprint 1.5 Production Smoke QA Audit*
