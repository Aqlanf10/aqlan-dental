# Agent Policy

This policy applies to Claude, Codex, Gemini, OpenCode, and any smaller model working on Aqlan Dental Pro.

## Required Read Before Editing

0. **Read `docs/governance/MANDATORY_SPRINT_QUEUE.md` first.** It is the single
   binding, sequenced backlog for this repository. Locate the first item not
   marked done and treat it as the only in-scope work, unless the user's request
   matches one of the queue's own narrow exceptions (P0 production incident,
   read-only audit, finishing an already-open PR, or a merge-conflict fix). If
   the user asks for something that is not the current queue item and is not
   covered by an exception, say so explicitly and ask before proceeding — do not
   silently work out of order.
1. Read `.specify/constitution.md`.
2. Read `specs/000-master-system/module-map.md`.
3. Read the target feature spec under `specs/`.
4. Inspect actual owners in `frontend/src/app/`, `frontend/src/components/`, `frontend/src/lib/`, `backend/src/`, and `backend/tests/`.
5. Inspect `frontend/src/components/layout/Sidebar.tsx` and `frontend/src/lib/routePermissions.ts` before any route/navigation change.
6. Inspect related controllers, DTOs, services, entities, validators, and tests before adding anything new.

## Strict Creation Rule

No agent is allowed to create a new route, controller, service, or database entity unless it first proves that no existing one already owns the responsibility.

The proof must name exact files searched and the conclusion. If proof is weak, stop and write a report.

## How To Choose The Right Module

- Dashboard/navigation: `frontend/src/app/(dashboard)/page.tsx`, `Sidebar.tsx`, `routePermissions.ts`.
- Daily operations: `frontend/src/app/(dashboard)/daily-operations/`, `DailyOperationsController.cs`, `PatientJourneyController.cs`, `ClinicQueueController.cs`.
- Doctor clinic: `frontend/src/app/(dashboard)/doctor-clinic/`, doctor schedule and patient journey APIs.
- Orthodontics: `frontend/src/app/(dashboard)/ortho/`, `frontend/src/components/ortho/`, `OrthoCasesController.cs`, `OrthoService`, `OrthoCaseQueryService`.
- Cephalometry: `frontend/src/app/(dashboard)/ceph/`, `frontend/src/components/ceph/`, `CephController.cs`, `CephService`, ceph AI services.
- Finance: `frontend/src/app/(dashboard)/finance-v3/`, `FinanceV3Controller*.cs`, `FinanceService`, `FinanceSettingsReader`, `TreasuryResolutionService`.
- Lab/inventory: `frontend/src/app/(dashboard)/lab/`, `frontend/src/app/(dashboard)/inventory/`, `LabOrdersController.cs`, `InventoryController.cs`, lab/inventory entities.
- Users/roles: `UsersController.cs`, `AuthController.cs`, `RolePermission`, `PermissionGuard`, `AuthorizationPolicyConfiguration.cs`, `frontend/src/hooks/usePermissions.ts`.

## API, DTO, Service, Entity Rules

- Search controllers for the route and resource name first.
- Search `backend/src/AqlanDentalPro.Application/DTOs/` before adding DTOs.
- Search `backend/src/AqlanDentalPro.Application/Validators/` before adding validators.
- Search `backend/src/AqlanDentalPro.Infrastructure/Services/` and DI registration before adding services.
- Search `AppDbContext.cs` and `backend/src/AqlanDentalPro.Domain/Entities/` before adding entities.
- Do not edit migrations unless the spec explicitly requires a database change and a strong model reviews it.

## Permissions And Patient Safety

- Backend authorization is authoritative. Frontend route checks are not enough.
- Do not widen patient access without updating the relevant spec and tests.
- Keep Arabic error messages in `message` fields for user-facing failures.
- Patient portal routes must stay under patient-specific auth boundaries.

## Settings And Business Rules

- If a setting exists, use it. Examples: `clinic.*`, `finance.*`, rooms, services, payment methods, lab pricing, AI settings.
- Do not hardcode finance thresholds, receipt identity, doctor title, PDF footer, or clinical AI language when settings or existing helpers exist.

## Deployment Safety

- Railway backend and PostgreSQL are production-sensitive; Vercel frontend builds from `main`.
- Do not change deployment configs, package files, environment assumptions, or upload storage behavior unless the spec explicitly covers it.
- Respect `.github/workflows/ci.yml` and `.github/workflows/encoding-guard.yml`.

## Tests And PR Size

- Update tests near the changed module. Existing examples include finance, auth, permissions, daily operations, queue, lab, ortho, ceph, surgery, and frontend tests.
- Open small PRs. One spec, one module, one coherent behavior change.
- Every PR must include spec ID, changed files, forbidden files not touched, tests, risks, rollback plan, and drift checklist.
