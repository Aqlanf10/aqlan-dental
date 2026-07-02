# 001 Navigation And Module Organization Design

- Frontend owner: `frontend/src/components/layout/Sidebar.tsx`.
- Route guard owner: `frontend/src/lib/routePermissions.ts`.
- Layout owner: `frontend/src/app/(dashboard)/layout.tsx`.
- Backend policy reference: `AuthorizationPolicyConfiguration.cs`.
- Permission key reference: `frontend/src/hooks/usePermissions.ts`.

Allowed files: sidebar, route permissions, route-specific pages when extending existing modules, specs.

Forbidden files unless explicitly approved: migrations, finance calculations, auth internals, deployment configs.

Design rule: route ownership must flow from module map -> sidebar/route permission -> existing page/API. If any layer is missing, update the spec before code.

Rollback plan: revert route/sidebar/spec edits together.
