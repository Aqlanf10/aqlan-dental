# 008 Users Roles Permissions Requirements

## Current State

Evidence: `UserRole.cs`, `AuthorizationPolicyConfiguration.cs`, `PermissionGuard.cs`, `RolePermission`, `AuthController.cs`, `UsersController.cs`, `frontend/src/hooks/usePermissions.ts`, `frontend/src/lib/routePermissions.ts`, auth/authorization tests.

- `PERM-REQ-001`: Backend authorization SHALL be authoritative.
- `PERM-REQ-002`: Frontend route permissions SHALL align with backend policies.
- `PERM-REQ-003`: Role permissions SHALL not be hardcoded when `RolePermissions` owns the rule.
- `PERM-REQ-004`: Patient role SHALL not access staff endpoints.
- `PERM-REQ-005`: User-facing auth/permission errors SHALL be Arabic.

## Target State

Clear, auditable, least-privilege permissions across backend and frontend.

## Risks

Default allow, patient data leak, finance access leak, route/UI mismatch.

## Allowed Future Work

Permission tests, route alignment, admin settings UI improvements.

## Forbidden Future Work

Cheap model permission edits, frontend-only security, widening patient/staff access without tests.

## Acceptance Criteria

- WHEN permission changes THEN backend policy, frontend guard, specs, and tests SHALL align.
- WHEN no route permission matches THEN dashboard route SHALL deny access.
