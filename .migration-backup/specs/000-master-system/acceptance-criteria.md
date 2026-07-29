# Global Acceptance Criteria

Every PR must satisfy these criteria unless the spec explicitly marks an exception.

- WHEN a PR changes behavior THEN the relevant spec SHALL be updated before merge.
- WHEN a PR adds a route THEN `module-map.md`, `Sidebar.tsx`, and `routePermissions.ts` SHALL be checked.
- WHEN a PR touches finance THEN finance tests SHALL be identified and strong-model review SHALL be required.
- WHEN a PR touches patient data THEN backend authorization and patient access tests SHALL be identified.
- WHEN a PR touches UI THEN Arabic RTL SHALL be preserved.
- WHEN a PR touches PDF/report output THEN clinic identity SHALL come from Settings or an approved fallback helper.
- WHEN a PR touches migrations THEN the migration risk SHALL be documented and reviewed by a strong model.
- WHEN a behavior cannot be verified statically THEN the PR SHALL include `Needs runtime verification`.
- WHEN tests cannot be run THEN the PR SHALL state why and what remains risky.
- WHEN a feature has no spec THEN implementation SHALL stop until a spec is created or updated.
