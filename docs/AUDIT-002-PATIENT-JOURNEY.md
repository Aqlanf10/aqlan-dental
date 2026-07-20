# Audit 002: Patient Journey and Financial Handoff

Date: 2026-07-21

## Scope

This audit maps the real patient workflow from registration to a completed
visit, including the handoff into finance. It distinguishes a clinical visit
completion from a financial settlement; they must not be presented as the same
business event.

## Canonical Workflow

| Step | Canonical UI | Server action | Owner |
| --- | --- | --- | --- |
| Register patient | `/patients` | `POST /api/patients` | Reception |
| Book appointment or walk in | `/appointments` or `/daily-operations` | `POST /api/appointments` | Reception |
| Arrival and intake | `/daily-operations` | `POST /api/patient-journey/{appointmentId}/intake` | Reception |
| Waiting, call, room, visit start | `/daily-operations` | Journey and queue endpoints | Reception / Doctor |
| Clinical handoff | `/daily-operations` | `POST /api/patient-journey/{visitId}/handoff-to-reception` | Doctor |
| Draft invoice | `/daily-operations` | `POST /api/patient-journey/{visitId}/create-draft-invoice` | Finance |
| Payment and receipt | `/daily-operations` | `POST /api/payments` + receipt PDF | Reception |
| Operational checkout | `/daily-operations` | `POST /api/patient-journey/{id}/checkout` | Reception |
| Reconciliation and period close | `/finance-v3` | cashier, journal, accounting-period endpoints | Accountant / Admin |

Legacy `/clinic-queue` and `/patient-journey` routes redirect to the canonical
daily-operations workspace. Patient-journey detail routes redirect to the
canonical patient record.

## Corrections Applied

1. The daily-operations page now consumes its `?tab=` query parameter through
   a strict allow-list. Queue and checkout links no longer silently open the
   default arrivals view.
2. Financial-closure validation now requires `FinanceAccess`, rather than only
   the broad staff policy.
3. A reception user can validate a balance but cannot self-approve a
   `ManagerOverride`; that exception is restricted to Admin or Accountant and
   remains audit logged.

## Confirmed Risks

### Financial closure is not yet enforced by operational checkout

`CheckoutAsync` marks the visit and appointment complete after a clinical
handoff. It does not call `ValidateFinancialClosureAsync`, create a payment,
or require an invoice/contract allocation. `CheckoutRequest.PaymentAmount` is
explicitly documented as informational only. The validation endpoint also had
no frontend caller at audit time.

This is not safe to describe as a final financial close. It is only an
operational departure/visit completion. An outstanding balance must remain
visible in Finance V3 and the patient account until it is paid, moved to an
approved instalment plan, or approved as a documented exception.

### Payment posting remains non-atomic

The intended production workflow is payment -> receipt -> cashier session ->
treasury -> allocation -> ledger posting. The current paths still span
`/api/payments` and the separate checkout mutation. A failure or manual
shortcut between them can leave a visit completed while its financial document
is unpaid or unallocated.

## Required Next Implementation

1. Introduce an explicit `CloseVisit` decision in daily operations with three
   outcomes: paid, approved credit/instalment, or clinical departure with an
   open balance.
2. Require the financial-closure validation result before the selected outcome
   is persisted. Only Admin/Accountant may approve an override, and its reason
   must be retained in the audit trail.
3. Post payment, receipt, cashier-session link, treasury link, allocation and
   accounting entry in one transaction or one durable workflow with compensating
   actions.
4. Add end-to-end coverage for appointment, walk-in, partial payment,
   multi-currency payment, approved credit, failed payment, and retry/double
   click cases.

## Verification Notes

The deployed URL responds and redirects unauthenticated requests to login.
Interactive authenticated browser verification is currently blocked by the
local browser-runtime bootstrap failure, so this audit does not claim a live
end-to-end completion test.
