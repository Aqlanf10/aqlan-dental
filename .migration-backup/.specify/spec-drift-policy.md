# Spec Drift Policy

Spec-code drift is a production risk. Specs are source-of-truth documents, not optional notes.

## Rules

1. Any feature PR must reference a spec folder.
2. Any changed behavior must update requirements, design, and tasks.
3. Any new route must update `specs/000-master-system/module-map.md`.
4. Any new permission must update `specs/000-master-system/constraints.md` and the relevant feature spec.
5. Any UI workflow change must update acceptance criteria.
6. Any finance change must update `specs/006-finance-stability/`.
7. Any cephalometry change must update `specs/005-cephalometry-workspace/`.
8. PR cannot be considered complete if specs and code disagree.

## PR Drift Checklist

- [ ] PR names the spec folder and requirement IDs.
- [ ] Changed routes are present in module map, sidebar, and route permissions where applicable.
- [ ] Changed permissions are reflected in backend policies and frontend permission keys where applicable.
- [ ] Requirements, design, tasks, and acceptance criteria match implemented behavior.
- [ ] Finance, auth, patient access, migrations, deployment, and clinical AI risks are explicitly marked.
- [ ] Any behavior not statically verifiable is marked `Needs runtime verification`.
- [ ] Tests are mapped to requirements.
- [ ] No duplicate screens, controllers, services, DTOs, or entities were introduced.
