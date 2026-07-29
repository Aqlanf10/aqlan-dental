---
name: EF enum columns need their DB-level default checked after type-changing migrations
description: A migration that converts an enum column's storage type (int -> varchar via HasConversion<string>) can silently drop the column's DB-level DEFAULT even though the EF model still declares HasDefaultValue.
---

Pattern: an entity property is an enum, mapped with `.HasConversion<string>().HasDefaultValue(SomeEnum.DefaultMember)`. A later migration converts the column's storage (e.g. int → varchar) via raw SQL (`ALTER COLUMN ... TYPE varchar USING ...`) and forgets to re-add `SET DEFAULT`. The EF model-level default is unaffected (it still shows up in `dotnet ef migrations` snapshots), so nothing looks wrong by reading the code or the model — only the live DB is missing the default.

**Why this matters:** when the CLR default of the enum (its `0` member) is also the value a new row actually gets, EF's SaveChanges omits that column from the INSERT (an optimization that trusts the DB default exists). If the DB default is missing, Postgres rejects the row as a NOT NULL violation — a `23502` crash on every insert of a "default-valued" row, which reads like a business-logic bug (e.g. "sending a lab order crashes") rather than a schema drift issue. This has hit `SupplierBills.Status` (`BillStatus.Unpaid` = 0) in this codebase; `Conversations.ConversationType` has the same latent mismatch (found, not yet fixed — out of scope at the time).

**How to apply:** after any migration that changes an enum-backed column's storage type, verify the DB-level default directly (`SELECT column_default FROM information_schema.columns WHERE table_name = '...' AND column_name = '...'`) — don't trust the EF model or `HasDefaultValue` alone. When auditing for this class of bug, search for `HasConversion<string>()` + `HasDefaultValue(...)` pairs and cross-check each one's live column default, not just the ones already reported as broken.
