# 008 Users Roles Permissions Design

- Backend policies: `AuthorizationPolicyConfiguration.cs`.
- Permission guard: `PermissionGuard.cs`.
- Controllers: `AuthController.cs`, `UsersController.cs`.
- Entities: `User`, `Doctor`, `RolePermission`, `PatientAccount`.
- Frontend: `usePermissions.ts`, `routePermissions.ts`, auth stores/pages, settings roles/users tabs.
- Tests: `Authorization/`, `Permissions/`, `PortalAuth/`, finance permission tests.

Allowed files: auth/permission owners and tests.

Forbidden files: finance logic except permission attributes with finance spec, migrations unless approved, patient access weakening.

Rollback: revert permission changes and restore route/policy alignment.
