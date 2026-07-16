# 011 — Design

## Execution Model

The stabilization program is evidence-first. Phase 0 changes governance and audit artifacts only; it does not repair discovered product defects. Each later slice starts from current `main`, proves one root cause, and updates this spec and the roadmap after its PR.

## Ordered Phases

| Phase | Scope | Exit gate summary |
|---|---|---|
| 0 | Freeze cephalometry and establish baseline | Saved drafts, repository snapshot, severity list, tests and CI baseline |
| 1 | Architecture and navigation | One owner per capability, no competing routes, route-permission tests |
| 2 | Central settings, identity, languages, printing | Settings-driven bilingual identity and shared print contract |
| 3 | Users, roles, granular permissions | Documented matrix and server tests for sensitive actions |
| 4 | Unified patient record and integrity | Canonical patient ID, duplicate controls, reviewed merge workflow |
| 5 | Appointments and capacity | Schedules, capacity, conflicts, leave/holiday rules, deterministic dates |
| 6 | Daily operations and queue | FIFO, no VIP, controlled emergency override, privacy-safe display |
| 7 | Doctor clinic and visit lifecycle | One visit and invoice draft per appointment, end-to-end state tests |
| 8 | Lab | Complete lifecycle, delay escalation, receipt and remake controls |
| 9 | Multi-currency finance | YER/SAR/USD preservation, treasury/cashier integrity, report rules |
| 10 | Inventory, administration, reports | Canonical links, auditability, unified printing |
| 11 | Basic orthodontics | Stable patient-owned orthodontic workspace |
| 12 | Cephalometry return gate | All prior gates plus green main CI and no open critical defects |

## Canonical Ownership

- Patients: `/patients` and the patient file.
- Appointments: `/appointments`, `/schedule`, and recall views.
- Check-in and active queue: `/daily-operations`; legacy queue/journey indexes redirect here.
- Doctor treatment: `/doctor-clinic` plus canonical patient/visit APIs.
- Lab: `/lab` and its existing subroutes.
- Finance and cashier: `/finance-v3` plus daily checkout integration.
- Orthodontics: patient-owned `/ortho` workspace.
- Cephalometry: part of the orthodontic case; frozen until Phase 12.

## Evidence Rules

- Static evidence: source, specs, migrations, tests, and CI metadata.
- Runtime evidence: authenticated workflow checks using synthetic or explicitly approved de-identified data.
- Every unverified behavior is labeled `Needs runtime verification`.
- Audit findings record severity, impact, evidence, and proposed owning phase; fixes are not mixed into Phase 0.

## Migration Safety

The historical migration chain and `StartupDatabaseMaintenance` are treated as production-critical. Phase 0 records discovery gaps, raw startup DDL, destructive `Down()` paths, non-idempotent operations, and snapshot drift. It does not rewrite old migrations.
