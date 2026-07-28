# 006 Finance Tasks

- `FIN-TASK-001`: Read-only finance route/controller/service map. Cheap model: yes.
- `FIN-TASK-002`: Add a focused finance regression test. Strong model.
- `FIN-TASK-003`: Adjust finance UI copy only. Medium model if no logic.
- `FIN-TASK-004`: Any calculation/refund/treasury change. Strong model only.
- `FIN-TASK-005`: Runtime verify cashier and receipt flows. Strong model.
- `FIN-TASK-006`: Add explicit finance list loading/error/empty-state regression coverage. Strong model.

---

## CORE-LAB — Lab Order Financial Linkage (PR #778)

- [x] `CORE-LAB-001` Extract the create-only linkage into an idempotent
      `LabOrderFinanceSyncService`; call it from Update and from the send transition.
- [x] `CORE-LAB-001` Add Cost / Currency / ExchangeRateToYer to `UpdateLabOrderRequest`
      with Arabic validation for the three supported currencies.
- [x] `CORE-LAB-002` Gate every entry into `sent` (draft AND remake) on an active lab,
      a positive cost, a rate for non-YER, and a work description.
- [x] `CORE-LAB-003` Shared `EditLabOrderModal` to complete and send a draft from both
      the lab screen and daily-operations.
- [x] `CORE-LAB-004` Refuse the sync when no branch resolves, instead of writing Guid.Empty.
- [x] `CORE-LAB-005` Serialise check-then-create with a per-order advisory lock.
- [x] `CORE-LAB-006` Add the missing per-patient authorization to all seven `{id:guid}`
      endpoints that lacked it.
- [x] `CORE-LAB-007` Read clinic identity from Settings in the PDF footer; use the clinic
      day, not the host day.
- [ ] **Follow-up:** re-point `Create` at `LabOrderFinanceSyncService` so one code path
      owns the linkage.
- [ ] **Follow-up:** verify `CORE-LAB-005` under real concurrency — the advisory lock is
      PostgreSQL-only and unit tests run on InMemory, so it is currently proven by
      reading, not by a race test.

