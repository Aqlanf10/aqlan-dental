# 001 Navigation And Module Organization Requirements

## Current State

Evidence: `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts`, `frontend/src/app/(dashboard)/`, `docs/agent-audit/`.

- `NAV-REQ-001`: The system SHALL keep one canonical sidebar in `Sidebar.tsx`.
- `NAV-REQ-002`: The system SHALL keep one canonical dashboard route, `frontend/src/app/(dashboard)/page.tsx`.
- `NAV-REQ-003`: Every dashboard route SHALL have explicit permission handling in `routePermissions.ts`.
- `NAV-REQ-004`: Daily operations SHALL remain the canonical reception workflow; `/clinic-queue` and `/patient-journey` must not become parallel daily workspaces.
- `NAV-REQ-005`: Any new route SHALL be added to `specs/000-master-system/module-map.md` before implementation.

## Target State

Navigation is predictable, Arabic RTL, role-aware, and has no duplicate module entries.

## Risks

Duplicate dashboard, duplicated sidebar links, route guard mismatch, hidden 403 loops, Arabic mojibake.

## Allowed Future Work

Clean labels, improve grouping, add missing explicit route permissions, add tests for route guard behavior.

## Forbidden Future Work

Creating `/dashboard2`, `/control-panel`, second finance/dashboard routes, or hiding backend permission gaps with frontend-only checks.

## Acceptance Criteria

- WHEN a user opens the app THEN the sidebar SHALL show only authorized module links.
- WHEN an unknown dashboard route is opened THEN access SHALL be denied by default.
- WHEN navigation changes THEN the module map SHALL be updated.
- Needs runtime verification for visual sidebar behavior.
