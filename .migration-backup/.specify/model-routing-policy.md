# Model Routing Policy

Use model strength according to clinical, financial, security, and architectural risk.

## Strong Models Only

- Finance logic, calculations, refunds, commissions, treasuries, cashier sessions, journals, supplier bills.
- Auth, roles, permissions, `RolePermissions`, `PermissionGuard`, route guard alignment.
- Patient access, patient portal, protected uploads, clinical records.
- EF migrations and database model changes.
- Production hotfixes.
- Cephalometry AI logic, landmark detection, diagnosis draft behavior.
- Architectural refactoring and cross-module integration.

## Medium Models Allowed

- UI cleanup within existing files.
- Documentation and spec updates.
- Tests based on existing patterns.
- Arabic copy improvements.
- Small component extraction inside the same module.

## Cheap/Small Models Allowed Only In Safe Mode

- Summarize files.
- Create checklists.
- Compare routes and labels.
- Draft docs.
- Generate test cases without applying them.
- Identify duplicated labels or obvious inconsistencies.
- Prepare reports for strong-model review.

## Cheap/Small Models Forbidden

- Create new modules.
- Create new API controllers.
- Create migrations.
- Touch finance calculations.
- Touch auth, roles, permissions, or patient access.
- Rename routes.
- Delete files.
- Modify deployment configs.
- Modify production settings.
- Make clinical AI claims.

## Safe Mode For Cheap Models

- Read-only by default.
- Must output a plan first.
- Must cite exact files.
- Must not edit code unless allowed files are explicitly listed.
- Must stop if uncertainty is high.
- Must mark unverified behavior as `Needs runtime verification`.
- Must ask for strong-model review before any finance, auth, patient, migration, or cephalometry AI work.
