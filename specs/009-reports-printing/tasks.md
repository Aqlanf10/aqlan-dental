# 009 Reports Printing Tasks

- `REP-TASK-001`: Audit PDF identity usage. Cheap model: read-only.
- `REP-TASK-002`: Add Arabic PDF rendering regression test. Strong/medium depending report.
- `REP-TASK-003`: Improve report UI copy/layout in existing files. Medium model.
- `REP-TASK-004`: Runtime render and inspect changed PDF. Needs runtime verification.

---

## REP-OPS — Operational Reports (owner directive 2026-07-28)

Ordered by operational value. Each is its own PR with a patient-access test.

- [ ] `REP-OPS-001` `GET /api/reports/operational/patients/outstanding` — debtor list
      (patient, phone, total, paid, remaining, oldest debt, last payment).
      **Highest value: this is the collections list the clinic cannot run today.**
- [ ] `REP-OPS-002` `patients/treated` — who was actually treated in a period.
- [ ] `REP-OPS-003` `patients/new` — new registrations, with contact columns.
- [ ] `REP-OPS-004` `income/daily` — day-by-day revenue, split by currency and method.
- [ ] `REP-OPS-005` `income/summary` — configurable daily/monthly/quarterly/yearly
      with previous-period comparison.
- [ ] `REP-OPS-006` `treatment-plans/progress` — per patient: done vs remaining.
- [ ] `REP-OPS-007` `ortho/cases` — case, stage, doctor, last visit, balance.
- [ ] `REP-OPS-008` `patients/returning` — returning patients with the absence gap in days.
- [ ] `REP-OPS-009` CSV/PDF export — only AFTER the row shape is stable.

### Not yet examined (stated so the matrix is not mistaken for complete)

- [ ] Deep read of `FinanceV3Controller.Reports.cs`, `LabReportsController.cs`,
      `DashboardController.cs` outputs — some requests may be partly served there.
- [ ] `frontend/src/app/(dashboard)/reports/operations/page.tsx` unread.

