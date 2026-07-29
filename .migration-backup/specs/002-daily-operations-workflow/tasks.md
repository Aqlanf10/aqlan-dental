# 002 Daily Operations Tasks

- `DO-TASK-001`: Audit daily operation action ownership. Cheap model: yes, read-only.
- `DO-TASK-002`: Add tests for changed queue/status transitions. Medium model.
- `DO-TASK-003`: Improve Arabic copy in existing modals. Medium model.
- `DO-TASK-004`: Verify quick payment respects cashier session. Strong model.
  — ✅ Done 2026-07-10 (strong model, code-verified). Verdict: PASS, enforced at
  every layer with no bypass path:
  (1) Frontend: both daily-operations payment entries (quick payment
  `handleQuickPayment` and the direct-payment button) block without
  `activeCashierSession`, with a clear Arabic message and a red/green session
  indicator on the button itself.
  (2) Backend hard gate: `PaymentService.CreatePaymentAsync` and
  `RefundPaymentAsync` throw «يجب فتح صندوق الكاشير...» without an open
  session — server-side, so the client guard is defense-in-depth, not the
  security boundary. `SupplierRefundService` (supplier bills + credit-note
  refunds) carries the same open-session requirement.
  (3) No bypass: `db.Payments.Add(` exists ONLY inside `PaymentService` and
  `SupplierRefundService`; `CheckoutService` explicitly creates no payments
  (documented in its own code).
  (4) Pinned by test: `FinanceV3PR245BlockerTests` asserts the rejection when
  no session is open.
- `DO-TASK-005`: Runtime smoke daily check-in -> call -> room -> close flow. Strong/medium with runtime.
