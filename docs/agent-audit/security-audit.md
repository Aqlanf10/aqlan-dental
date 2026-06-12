# Security Audit — Aqlan Dental Pro
**التاريخ:** 2026-06-12 · مبني على فحص الكود الفعلي

## Strong Points (verified)
- **JWT validation** (`Configuration/JwtAuthenticationConfiguration.cs:32-42`): issuer, audience, lifetime, signing key all validated; `ClockSkew = 0`. Access 15min, refresh 7 days.
- **Staff refresh token** in HTTP-only cookie (`AuthController.cs:81`); frontend refresh queueing prevents stampedes (`lib/api.ts:33-102`).
- **401/403 separation**: `ErrorHandlingMiddleware.cs:30` maps `UnauthorizedAccessException`→403; unauthenticated→401 via JWT middleware; Arabic messages.
- **Centralized policies** (`AuthorizationPolicyConfiguration.cs`): AdminOnly, FinanceAccess/FinanceWrite, DoctorAccess, AdminOrReception, StaffOnly (excludes Patient), etc.
- **Doctor→patient restriction**: `PatientAccessService` limits doctors to linked patients (primary doctor / appointment / visit / treatment step / referral) — 492-line test suite (`Security/PatientAccessServiceTests.cs`).
- **CORS**: explicit origins only, wildcard `*.vercel.app` previously removed (C-01 fix).
- **Rate limiting** on auth/portal/booking + account lockout (5 attempts / 15 min).
- **Security headers middleware** (X-Frame-Options, nosniff, CSP, HSTS, Permissions-Policy).
- **WhatsApp webhook**: HMAC-SHA256 with `FixedTimeEquals` (timing-attack safe).
- **Production fail-fast** (`Program.cs:11-22`): refuses to start with placeholder JWT key / connection string.
- **Public endpoints** are explicitly `[AllowAnonymous]` and scoped (booking, TV queue display, portal auth) with reCAPTCHA + honeypot on public forms.
- No real secrets committed (placeholders only; root `.env` removed from tracking this sprint).

## Fixed in This Sprint
### Exception detail leak in 500 responses (High) — FIXED
Commit d07db58 added `detail = ex.Message, type = ex.GetType().Name` to PDF/lab 500 responses "for debugging" and was never removed. This leaked DB schema fragments, provider exception types (e.g. `NpgsqlException`) and file paths to any authenticated client.
**Removed from all 6 sites:** `PaymentsController` (receipt PDF), `InvoicesController` (invoice PDF), `ReportsController` (financial statement PDF), `LabOrdersController` (get, update-load, print ×2). Full details remain in server logs via `logger.LogError`. The unit test that previously *required* the leak (`PdfEndpointErrorHandlingTests.LabOrderPdf_ReturnsArabic500WithDetail`) was inverted to **forbid** it (`…WithoutExceptionDetails`).

## Open Items (prioritized)
| # | Item | Risk | Recommendation |
|---|---|---|---|
| 1 | Patient-portal **refresh token in localStorage** (`stores/patientAuthStore.ts:32-34`, `lib/portalApi.ts:107-123`) — staff side uses HTTP-only cookie | Medium (XSS persistence) | Move portal refresh token to HTTP-only cookie like staff flow |
| 2 | Staff **access token in localStorage** (`stores/authStore.ts:44`) | Medium (XSS) | Acceptable short-term (15min expiry); consider in-memory + silent refresh |
| 3 | **No doctor↔room restriction** (no DoctorRoom entity) | Medium (business rule) | Add room assignment entity + filter in queue/journey endpoints; Admin sees all |
| 4 | Scattered `User.IsInRole` checks (`PatientJourneyController.cs:507-509, 715-716`, `DashboardController.CanViewFinance`) | Low | Consolidate into policies / `PermissionGuard` |
| 5 | Hardcoded seed password `AqlanDental2024!` (`DbSeeder.cs:117,267,317`) | Low (env override + fail-fast exist) | Require `ADMIN_DEFAULT_PASSWORD` env in production docs (already documented in `.env.example`) |

## Required Behaviors Checklist (from business requirements)
- Unauthenticated → 401 ✅ · Unauthorized role → 403 ✅ · Doctor sees only linked patients ✅ (rooms: open item 3) · Reception scope enforced via AdminOrReception ✅ · Accountant via FinanceAccess/FinanceWrite ✅ · Admin everything ✅ · Central permissions: mostly ✅ (open item 4).
