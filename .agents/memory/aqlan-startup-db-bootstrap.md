---
name: Aqlan Dental Pro — startup DB bootstrap quirks
description: Non-obvious bugs in this repo's self-healing migration/bootstrap pipeline (StartupDatabaseMaintenance.cs) found only by testing against a truly empty database.
---

This codebase's ASP.NET Core API runs a custom startup pipeline (`StartupDatabaseMaintenance.cs`) that does self-healing schema reconciliation ("HOTFIX" blocks) *and* a from-scratch bootstrap path for genuinely empty databases, before EF's normal `MigrateAsync()`.

Two classes of bug were found here, both invisible unless you test against a truly empty (freshly created) Postgres database rather than one that already has `__EFMigrationsHistory`/`Users`:

1. **Mismatched reconciliation guards**: some of the HOTFIX blocks' `DELETE FROM __EFMigrationsHistory` guard conditions checked the wrong table/column for a given migration ID (copy-paste bug), causing valid history rows to be deleted every startup and EF to re-attempt already-applied migrations ("already exists" crash), which aborted seeding before any users existed.
2. **`ExecuteSqlRawAsync` + composite format strings**: EF's `Database.ExecuteSqlRawAsync(sql)` treats the string as a composite format string even with zero parameters. Running a full `GenerateCreateScript()` output through it crashes (`FormatException: Expected an ASCII digit`) because Postgres DDL legitimately contains literal `{` (array/JSONB default literals like `'{}'`). Fix: execute such scripts via a raw ADO `DbCommand` instead.

**Why this matters**: any change to this pipeline should be smoke-tested against a *brand-new* empty database, not just the long-lived dev DB — the empty-DB code path is a different branch that's easy to leave broken indefinitely otherwise.

**How to apply**: before trusting a fix to startup/migration code in this repo, `CREATE DATABASE` fresh, point the API at it via `ConnectionStrings__DefaultConnection`, and confirm it seeds + logs in on first boot.
