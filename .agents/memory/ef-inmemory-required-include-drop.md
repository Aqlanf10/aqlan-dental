---
name: EF Core InMemory provider silently drops rows on required Include
description: A required navigation's dangling FK makes InMemory's Include(...).ThenInclude(...) return null for the whole entity, not just the nav — Postgres does not do this.
---

When a query does `.Include(a => a.B).ThenInclude(b => b.C)` where `B.CId` is a **required** (non-nullable) FK configured via `HasOne(...).WithMany().HasForeignKey(...)` (no `.IsRequired(false)`), and the FK value doesn't match any actual `C` row (e.g. a test fixture left `CId` as `Guid.Empty`, or points to an entity that was never saved), EF Core's **InMemory** provider drops the *entire root entity* from the query result — not just the `C` navigation. `FirstOrDefaultAsync` returns `null` even though the same predicate without the `Include` chain finds the row fine.

Real PostgreSQL (or any relational provider) does a LEFT JOIN and just returns `C = null`; it never removes the row. This divergence only shows up in InMemory-backed unit tests and can look exactly like "the code has a bug" (a service method mysteriously returning early / not finding an entity) when the actual problem is an incomplete test fixture.

**Why this matters:** cost real debugging time tracing a correct production fix (`CommissionService.AutoFillFromServiceAsync`) that appeared to silently no-op in a new InMemory unit test, because the test's `Invoice` was seeded without a real `Patient`, and the service's `LoadLineItemAsync` does `.Include(i => i.Invoice).ThenInclude(inv => inv.Patient)`.

**How to apply:** when a repository/service method under test uses `Include(...).ThenInclude(...)` through a *required* relationship, always seed a real, saved entity on the required side of every hop in the chain — even if the test doesn't care about that entity's data. If a query mysteriously returns null/empty against InMemory despite the row existing, check every required Include hop for a dangling FK before assuming the production code is wrong.
