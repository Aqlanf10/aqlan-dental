# Audit 001: Navigation And Structure

## Executive Summary

Aqlan Dental Pro is an existing full-stack dental-clinic system, not a greenfield
application. The dashboard uses Next.js App Router, a role-aware sidebar, and a
central route-permission manifest. The backend is a .NET 8 Clean Architecture API
with EF Core migrations. This audit is intentionally limited to navigation,
structure, route access, and user-facing shell consistency. No patient, financial,
or destructive schema change is included in this audit.

## Runtime And Entry Points

| Area | Technology | Entry point / command |
| --- | --- | --- |
| Frontend | Next.js 15, React 19, TypeScript, Tailwind, Zustand, React Query | `cd frontend; npm run dev` |
| Backend | .NET 8 Web API, EF Core, PostgreSQL | `cd backend; dotnet run --project src/AqlanDentalPro.API` |
| Local services | PostgreSQL and supporting services | `docker-compose up -d` |
| Frontend checks | ESLint, TypeScript, Vitest, Playwright | `npm run lint`, `npx tsc --noEmit`, `npm test`, `npm run test:e2e` |
| Backend checks | Build and test projects | `dotnet build`, `dotnet test` |

The root `README.md` is a historical planning document. Its declared frontend
versions and some planned integrations do not reliably describe the installed
application; `frontend/package.json` and project files are the source of truth.

## Navigation Map

The dashboard shell is `frontend/src/app/(dashboard)/layout.tsx`. Its sidebar is
`frontend/src/components/layout/Sidebar.tsx`; route access is centrally declared in
`frontend/src/lib/routePermissions.ts`.

| Area | Canonical route | Notes |
| --- | --- | --- |
| Dashboard | `/` | Admin dashboard. |
| Daily operations | `/daily-operations` | Reception workflow hub; legacy queue and journey index routes redirect here. |
| Patients | `/patients` | Patient records, visits, files, payments, and print views. |
| Clinic | `/appointments`, `/booking-requests`, `/schedule`, `/doctor-clinic`, `/prescriptions` | Scheduling and clinical workspaces. |
| Specialties | `/ortho`, `/ceph`, `/general`, `/surgery`, `/ortho-surgical` | Specialty workflows with restricted roles. |
| Communications | `/referrals`, `/messages`, `/whatsapp`, `/sms` | Role-specific outbound and internal communication. |
| Finance | `/finance-v3` | The canonical finance hub. |
| Inventory and labs | `/inventory/*`, `/lab/*` | Sidebar groups, with lab payables still displayed separately from finance. |
| Reports | `/reports` | Admin and Accountant. |
| Administration | `/doctors`, `/employees`, `/hr/*`, `/branches`, `/settings` | Admin and configured exceptions. |

## Findings

| ID | Location | Finding and user impact | Severity | Proposed response | Database change |
| --- | --- | --- | --- | --- | --- |
| NAV-001 | `README.md` | Documentation describes Next 14 and planned services, while the installed frontend is Next 15/React 19. Operators can run incorrect commands or make wrong support assumptions. | Medium | Replace the historical plan with a concise verified runbook in a later documentation-only change. | No |
| NAV-002 | `Sidebar.tsx`, `routePermissions.ts` | Route and sidebar roles are intentionally centralised and guarded by tests, but the menu is long and mixes primary workflows with specialist administration. New staff can struggle to locate a starting point. | Medium | Keep current routes; use low-risk grouping, labels, and responsive sidebar review only after browser verification. | No |
| NAV-003 | `/clinic-queue`, `/patient-journey` | These retained direct routes redirect to daily operations. This avoids 404s but leaves multiple names for the same workflow in code and search results. | Low | Document the canonical route and verify redirects; do not remove compatibility routes in Audit 001. | No |
| NAV-004 | `/finance-v3`, `/lab/payables`, `/inventory/suppliers` | Finance, lab payables, and inventory suppliers are distinct entry points. The overlap is clinically understandable but creates a navigation and reconciliation risk. | High | Document the canonical ownership and defer financial-domain consolidation to a dedicated approved finance audit. | Potentially, later only |
| NAV-005 | `routePermissions.ts` | The manifest uses default-deny, which is safer; every newly exposed dashboard route must be registered or it becomes inaccessible. | Medium | Add a route-manifest test for discovered dashboard pages before adding navigation entries. | No |
| NAV-006 | `patients/[id]/page.tsx` | A patient action uses `href="#"` for sending a reminder, presenting a command that cannot complete. | Medium | Replace with the existing reminder/message workflow after confirming the target and permissions. | No |
| NAV-007 | `Sidebar.tsx` | Sidebar styling has local hard-coded colour constants while the rest of the UI uses tokens. This can cause shell inconsistency across views. | Low | Tokenise only after a screenshot review; no visual redesign in this audit. | No |
| NAV-008 | dashboard route tree | A large number of feature pages are reachable only through detail pages, query tabs, or direct URLs. Their route registration and loading/error states need systematic browser coverage. | Medium | Build a route smoke matrix using authenticated Playwright tests. | No |

## Duplicate Or Compatibility Routes

* `/clinic-queue` and `/patient-journey` are compatibility redirects to the daily
  operations workspace.
* `/users` redirects to the settings permissions tab.
* Finance has one canonical dashboard route (`/finance-v3`); query-string tabs are
  views inside that workspace and should not become separate sidebar entries.
* Settings child pages remain direct-addressable but are intentionally not all
  duplicated in the sidebar.

## Broken Or Unverified Actions

* `patients/[id]/page.tsx` contains a reminder action targeted at `#`; it is not a
  usable workflow.
* The audit has not yet completed authenticated browser validation of every sidebar
  route, responsive breakpoint, and permission role. These are test work items,
  not an assumption that routes are broken.

## Safe Implementation Order

1. Verify each sidebar leaf route as an Admin in a browser and record redirect,
   loading, error, and console outcomes.
2. Verify a Reception user and an Accountant user against the same menu contract.
3. Add route smoke tests for every sidebar leaf and compatibility redirect.
4. Fix confirmed low-risk dead links and label inconsistencies only.
5. Perform a mobile sidebar and RTL screenshot pass.
6. Present the completed Audit 001 report before starting Audit 002.

## Risks Before Implementation

* Financial, lab, inventory, and patient workflows cross module boundaries; a
  navigation-only audit must not alter posting or clinical logic.
* Direct route compatibility is important for bookmarks and existing staff habits.
* Permission changes can expose protected data or hide required workspaces; changes
  must continue to use `ROUTE_MANIFEST` and existing backend authorization.

## Files Changed In This Audit

* `docs/AUDIT-001-NAVIGATION.md`: initial verified navigation and structure audit.

## Verification Record

| Command | Result |
| --- | --- |
| `npm run lint` | Completed with existing warnings only; no lint error stopped the command. Warnings include unused imports, an effect dependency warning, and `img` optimisation guidance. |
| `npx tsc --noEmit --pretty false` | Passed. |
| `npm test` | The local terminal transport detached while Vitest was running and did not return the final summary. No claim of a full-suite pass is made; CI must be used for the final result. |
| `npx vitest run src/__tests__/components/layout/SidebarNavigation.test.ts` | The runner started, but the same terminal transport issue prevented capture of the final summary. |

## Audit Boundary

No route, permission, database, patient, or financial behaviour has been changed
in this audit report commit. Confirmed low-risk fixes will be made only after the
browser route matrix establishes their destination and permission contract.
