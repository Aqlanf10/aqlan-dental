# 001 Navigation Cleanup Tasks

Spec ID: `001-navigation-and-module-organization`

These tasks started as implementation follow-ups after the documentation-only audit.

## Implementation Status

- `NAV-TASK-001` done in #590/#591: Accountant can open the approved lab summary/report/payables routes without opening all lab orders.
- `NAV-TASK-002` done in #590/#591: BranchManager lab settings access is aligned with specific settings-lab route rules, while generic `/settings` remains Admin-only.
- `NAV-TASK-003` done in #590: `/` remains the canonical dashboard; `/clinic-command-center` is hidden from the global sidebar.
- `NAV-TASK-004` done in #590: dashboard queue shortcuts target `/daily-operations?tab=queue`.
- `NAV-TASK-005` done in #591: root dashboard route guard behavior is explicit and tested.
- `NAV-TASK-006` done in #591: Accountant direct `/patients` access remains closed, matching the sidebar.
- `NAV-TASK-007` done in #591: OralSurgeon direct `/general` access remains closed, matching the sidebar.
- `NAV-TASK-008` done in #591/spec map: `/ortho-surgical` stays a contextual shared workspace, not a global sidebar module.
- `NAV-TASK-009` done in #591: `/lab/dashboard` is labeled as a lab summary surface.
- `NAV-TASK-010` done in #591: lab settings are kept canonical in the settings hub.
- `NAV-TASK-011` done in #591: prescriptions are grouped with doctor clinic navigation.
- `NAV-TASK-012` covered by route permission regression tests for the implemented mismatches; a generated inventory report can be added later if desired.
- `NAV-TASK-013` remains a manual runtime checklist item for a future browser pass across all roles.
- `NAV-TASK-014` done in #591: module ownership notes reflect the implemented navigation decisions.

| Task ID | Goal | Files allowed to edit | Files forbidden to edit | Model allowed | Tests required | Acceptance criteria | Risk |
|---|---|---|---|---|---|---|---|
| `NAV-TASK-001` | Add/adjust lab subroute route permissions so Accountant can open only intended lab report/payable pages. | `frontend/src/lib/routePermissions.ts`, route guard tests, `specs/001-*` | Backend finance/lab logic, migrations, package files | strong | Add/extend route permission tests; runtime smoke by Accountant | Accountant-visible lab links no longer redirect away, or links are hidden. | High |
| `NAV-TASK-002` | Align BranchManager lab settings links with route guard. | `Sidebar.tsx`, `routePermissions.ts`, permission tests, `specs/001-*` | Backend settings logic, migrations, secrets | strong | Route guard tests for BranchManager/Admin | BranchManager either can open approved lab settings routes or never sees those links. | High |
| `NAV-TASK-003` | Decide and implement command center placement. | `Sidebar.tsx`, `/clinic-command-center/page.tsx`, dashboard docs/specs | Finance/auth/migrations/deployment | medium with owner confirmation | Visual/runtime smoke as Admin | Only one route is presented as the main dashboard/control panel. | Medium |
| `NAV-TASK-004` | Replace dashboard queue shortcut redirect hop. | `frontend/src/app/(dashboard)/page.tsx`, tests if present | Redirect stubs, backend queue APIs | medium | Lightweight UI link test or manual runtime check | Dashboard queue shortcut targets `/daily-operations?tab=queue`. | Medium |
| `NAV-TASK-005` | Document or implement explicit root dashboard route-guard behavior. | `routePermissions.ts`, dashboard layout tests/specs | Auth backend, migrations | strong | Route guard tests for Admin and non-admin root access | Admin can open `/`; non-admin redirects consistently; default-deny remains intact. | Medium |
| `NAV-TASK-006` | Decide Accountant patient navigation. | `Sidebar.tsx`, `routePermissions.ts`, finance patient account docs/specs | Patient access service/backend policy unless separately approved | strong | Route guard and patient privacy tests if changed | Accountant patient-list access is either visible and approved or removed from route guard. | High |
| `NAV-TASK-007` | Decide OralSurgeon access to `/general`. | `Sidebar.tsx`, `routePermissions.ts`, specs | General clinical backend unless approved | strong | Route guard tests | Sidebar and route permissions agree for OralSurgeon. | High |
| `NAV-TASK-008` | Decide `/ortho-surgical` visibility. | `Sidebar.tsx`, `routePermissions.ts`, ortho/surgery specs | Ortho/surgery clinical logic | medium/strong if permissions change | Route guard smoke; contextual link smoke | `/ortho-surgical` is documented as hidden contextual or visible in the correct group. | Medium |
| `NAV-TASK-009` | Rename or merge `/lab/dashboard`. | `Sidebar.tsx`, lab page labels/specs | Lab APIs, finance calculations, migrations | medium | Visual/runtime check as Admin/BranchManager/Accountant | Staff no longer confuse lab overview with main dashboard. | Medium |
| `NAV-TASK-010` | Rationalize lab settings links. | `Sidebar.tsx`, settings hub docs/specs | Settings backend, migrations | medium after permission decision | Runtime check for Admin/BranchManager | Lab settings appear either in settings only or in both places with clear owner-approved rationale. | Medium |
| `NAV-TASK-011` | Re-group prescriptions and patient segments. | `Sidebar.tsx`, specs | Prescriptions/patient APIs | medium | Visual/runtime check by role | Prescriptions and segments are in clinic-staff mental model groups. | Low |
| `NAV-TASK-012` | Add route inventory regression report/check. | docs/specs or a non-runtime script if later approved | Runtime app code unless separately scoped | cheap-readonly for report; medium if adding script | Documentation check or test if script added | Future PRs can detect sidebar/route guard mismatch before merge. | Low |
| `NAV-TASK-013` | Runtime-check sidebar for all roles. | No code for audit; screenshots/report docs only | Runtime code, permissions | cheap-readonly for checklist; medium to run browser | Manual/browser verification report | Each role sees only links it can open. | Medium |
| `NAV-TASK-014` | Update module map after implementation decisions. | `specs/000-master-system/module-map.md`, traceability docs | Runtime code | cheap-readonly/docs | Markdown diff check | Module map matches implemented navigation ownership. | Low |
