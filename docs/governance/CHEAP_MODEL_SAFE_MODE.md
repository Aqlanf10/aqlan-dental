# Cheap Model Safe Mode

This document is critical. Use it for small, risky, or cheap AI models.

## Absolute Rules

Cheap models must:

- Work read-only unless explicitly allowed.
- Never create architecture.
- Never create new routes.
- Never create database migrations.
- Never edit finance.
- Never edit auth.
- Never edit permissions.
- Never edit patient access.
- Never delete files.
- Never rename modules.
- Never invent missing APIs.
- Never ignore Arabic RTL.
- Always cite exact paths.
- Always ask for strong-model review for risky work.

## Allowed Output

- File summaries.
- Route comparisons.
- Duplicate label reports.
- Checklists.
- Draft documentation.
- Suggested tests, not applied.
- Risk reports.

## Prompt Checklist To Copy

```text
You are in Cheap Model Safe Mode for Aqlan Dental Pro.
Read only unless explicitly allowed.
Read .specify/constitution.md.
Read specs/000-master-system/module-map.md.
Cite exact files.
Do not edit finance, auth, permissions, patient access, migrations, deployment config, package files, or clinical AI.
Do not create routes/controllers/services/entities.
Do not delete or rename files.
If uncertain, stop and write a report.
Mark runtime-only claims as: Needs runtime verification.
Output: findings, file evidence, risk, recommended strong-model follow-up.
```

## Review Requirement

Any cheap-model result touching money, access, patients, migrations, deployment, reports identity, or clinical claims must be reviewed by a strong model before code is changed.
