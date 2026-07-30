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
- [x] `CORE-LAB-008` Keep completed drafts off the books until `sent`; cancel unpaid
      sent-order trails atomically and block cancellation/deletion after payment.
- [x] `CORE-LAB-009` Return currency/rate/total/lab fields in the list DTO so editing
      a SAR/USD row cannot silently overwrite it as YER.
- [ ] **Follow-up:** re-point `Create` at `LabOrderFinanceSyncService` so one code path
      owns the linkage.
- [ ] **Follow-up:** verify `CORE-LAB-005` under real concurrency — the advisory lock is
      PostgreSQL-only and unit tests run on InMemory, so it is currently proven by
      reading, not by a race test.


---

## CORE-FIN-LAB-ADJ — Commission Correction After a Lab-Cost Change

Closes the last open half of the lab↔finance link. `CORE-LAB` made the lab cost
enterable and billable; this makes a *change* to that cost reach the doctor's
commission, which is the only place the cost is actually deducted.

- [x] `CORE-FIN-LAB-ADJ-001` `DoctorCommissionAdjustment` entity + EF configuration
      (no navigation properties, by design) + idempotent startup DDL. Purely additive:
      one new table, no existing table touched, no migration edited.
- [x] `CORE-FIN-LAB-ADJ-002` `CommissionAdjustmentService`: unpaid commissions
      recalculated in place, paid ones corrected by a separate signed line.
- [x] `CORE-FIN-LAB-ADJ-003` Idempotency by measured delta — the amount raised is
      `correct − paid − corrections already raised`, so repeat runs converge.
- [x] `CORE-FIN-LAB-ADJ-004` `LabCostForCommission`: the draft / cancelled / currency
      rules in one place, shared by `CommissionService.AutoFillFromServiceAsync` and
      the resync so they cannot drift.
- [x] `CORE-FIN-LAB-ADJ-005` `CommissionLineWriter`: the OnPaymentCollection carve-out
      extracted so a second copy of it cannot be written without the carve-out.
- [x] `CORE-FIN-LAB-ADJ-006` `CommissionBalance`: earned + corrections − paid, shared by
      the payment cap and the settlement summary so they cannot disagree.
- [x] `CORE-FIN-LAB-ADJ-007` Resync wired into every lab-order write (edit, status
      change, cancel, delete), inside the caller's transaction.
- [x] `CORE-FIN-LAB-ADJ-008` Endpoints: list corrections, doctor settlement summary,
      per-order resync, admin backfill sweep, admin cancel-with-reason.
- [x] `CORE-FIN-LAB-ADJ-009` Frontend panel on the commissions tab, with a real error
      state and a sweep that reports what it left untouched.
- [x] `CORE-FIN-LAB-ADJ-010` Tests: 21 backend (14 service, 7 resolver) + 4 payment-path
      + 7 frontend. Verified non-vacuous by sabotage — removing the paid freeze fails 7,
      removing the prior-corrections term fails 2.

### Deliberately not done

- **No automatic clawback.** A negative correction lowers the ceiling on the doctor's
  next payment; it never reaches into the treasury or reverses a posted disbursement.
  Recovering money already handed over is a decision for the owner, not a background job.
- **No link invention.** The backfill sweep re-syncs only line items that ALREADY carry
  a `LabOrderId`. An ambiguous historical record is reported, never guessed — the same
  rule the auto-link resolver follows.

### Follow-ups

- [ ] The corrections panel is read-and-cancel only. A settlement PDF listing a doctor's
      corrections alongside their accrued commission is not built.
- [ ] `ResyncLabOrderAsync` is exercised through the lab-order controller paths by the
      existing lab tests, but there is no end-to-end test that edits an order over HTTP
      and asserts the correction appears. Worth adding when integration coverage grows.
