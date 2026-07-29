# 009 Reports Printing Tasks

- `REP-TASK-001`: Audit PDF identity usage.
- `REP-TASK-002`: Add Arabic PDF rendering regression test.
- `REP-TASK-003`: Improve report UI copy/layout.
- `REP-TASK-004`: Runtime render and inspect changed PDF.

---

## REP-OPS — Operational Reports

Implemented under `/api/reports/operations/details?type=...`.

- [x] `REP-OPS-001` `outstanding-balances` — patient/currency totals, paid and remaining.
- [x] `REP-OPS-002` `treated-patients` — who was treated and what was done.
- [x] `REP-OPS-003` `new-patients` — clinic-day-safe registrations and contact columns.
- [x] `REP-OPS-004` `income` — receipt rows and summaries split by currency.
- [ ] `REP-OPS-005` previous-period comparison (date presets and custom range are complete).
- [x] `REP-OPS-006` `treatment-progress` — per patient completed vs remaining.
- [x] `REP-OPS-007` `ortho-cases` — case, stage, doctor, visits and balance.
- [x] `REP-OPS-008` `returning-patients` — configurable absence gap and treatment change.
- [x] `REP-OPS-009A` CSV export after row shape stabilization.
- [ ] `REP-OPS-009B` PDF export and visual verification.

### Verification

- [x] Backend tests for branch scope, currencies, clinic-day boundaries, balances,
      returning patients, treatment progress and CSV.
- [x] Frontend contract tests for report types, routes, pagination and currency UI.
- [ ] Preview runtime verification before merge.
