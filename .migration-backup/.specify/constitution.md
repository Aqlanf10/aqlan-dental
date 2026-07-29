# Aqlan Dental Pro Constitution

This constitution is mandatory for every future change to Aqlan Dental Pro. It is based on the current repository evidence in `CLAUDE.md`, `frontend/src/app/`, `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts`, `backend/src/`, `backend/tests/`, `.github/workflows/`, and existing `docs/`.

## Non-Negotiable Principles

1. Arabic RTL is mandatory. The root layout uses `dir="rtl"` and `lang="ar"` in `frontend/src/app/layout.tsx`; all user-facing UI and errors must respect Arabic RTL.
2. No duplicate dashboard or control panel. The canonical staff dashboard route is `frontend/src/app/(dashboard)/page.tsx`, and the sidebar already contains one dashboard entry.
3. No duplicate module routes. Navigation is centralized in `frontend/src/components/layout/Sidebar.tsx`; route access is centralized in `frontend/src/lib/routePermissions.ts`.
4. Existing modules must be extended, not recreated. Examples: daily operations lives under `frontend/src/app/(dashboard)/daily-operations/` and `backend/src/AqlanDentalPro.API/Controllers/DailyOperationsController.cs`; finance lives under `/finance-v3` and `FinanceV3Controller*`.
5. Finance changes require high caution. Finance is spread across `FinanceV3Controller*`, `FinanceService`, `TreasuryResolutionService`, `FinanceSettingsReader`, `FinanceSettingsKeys`, finance DTOs, and many finance tests.
6. Patient access and privacy rules must not be weakened. `PatientAccessFilter`, `RequirePatientAccessAttribute`, `PatientAccessService`, production upload auth, and patient portal policies are safety boundaries.
7. Database migrations must not be edited casually. `CLAUDE.md` documents a historically fragile migration chain, and migrations live under `backend/src/AqlanDentalPro.Infrastructure/Data/Migrations/`.
8. No hardcoded business rules when settings exist. Settings are represented by `Setting`, `SettingsController`, `FinanceSettingsKeys`, `FinanceSettingsReader`, and `FinanceClinicIdentity`.
9. No fake AI or fake clinical claims. AI features must be backed by real providers/services and explicit draft language.
10. Cephalometry AI is draft-only until doctor review. Evidence: `CephAiDraftService`, `CephAiLandmarkDraftService`, `CephAiDraftResultDto`, `AiSettingsController`, and ceph tests. Needs runtime verification for the exact UI review flow.
11. PDF/report identity must use settings, not hardcoded text. Evidence: `FinanceClinicIdentity`, `PdfService`, `CephReportPdfGenerator`, ortho/lab PDF generators, and `CLAUDE.md`.
12. Every implementation must link to a spec ID.
13. Every PR must say which spec it implements.
14. If the requested feature has no spec, the agent must create or update the spec first.
15. If unsure, write a report, not code.

## Project-Specific Examples

- Do not create `/dashboard2`, `/control-panel`, or a second `/daily` screen. Extend the existing dashboard or daily operations workspace.
- Do not create a parallel `/finance` feature. Use `/finance-v3`, `frontend/src/app/(dashboard)/finance-v3/`, and `backend/src/AqlanDentalPro.API/Controllers/FinanceV3Controller*.cs`.
- Do not add a second lab order API. Use `LabOrdersController`, `LabOrderQueryService`, lab DTO/types, and the lab routes under `frontend/src/app/(dashboard)/lab/`.
- Do not bypass `routePermissions.ts`, `PermissionGuard`, or `RolePermissions` with hardcoded UI-only checks.
- Do not print clinic identity literals in a new PDF generator; read `clinic.*` and finance receipt settings.

## Enforcement

Any agent that cannot prove the correct owner module, route, controller, DTO, service, permissions, and tests must stop and produce a written report. Code changes without an applicable spec are non-compliant.
