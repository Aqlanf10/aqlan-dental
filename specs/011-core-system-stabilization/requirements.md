# 011 — Core System Stabilization

- Status: Active, Phase 0 only
- Owner directive: 2026-07-17
- Priority flow: Patients -> Appointments -> Check-in -> Queue -> Doctor clinic -> Lab -> Accounting -> Next appointment

## Requirements

- `CORE-REQ-001` Cephalometric feature development SHALL remain frozen. Existing work SHALL stay preserved in independent draft PRs and SHALL NOT be merged while incomplete.
- `CORE-REQ-002` Phase 0 SHALL start from the latest `main` and record open PRs, CI, test baselines, production-critical failures, migration risk, duplicate routes, and duplicate modules before fixes begin.
- `CORE-REQ-003` The audit SHALL create `docs/roadmap/core-system-current-state.md` covering module state, working behavior, UI-only behavior, partial features, duplication, conflicting routes, critical defects, technical debt, hardcoded rules, permissions, data integrity, daily operations, appointments/queue, lab, finance/currencies, printing/identity, missing tests, and priorities.
- `CORE-REQ-004` The audit SHALL create `docs/roadmap/core-system-priority-plan.md` and an updateable task list. Work SHALL proceed through phases 0 to 12 in order without skipping an unmet exit gate.
- `CORE-REQ-005` Every business capability SHALL have one canonical owner route and backend owner. The existing dashboard, sidebar, patient module, appointment module, daily operations, doctor clinic, lab, finance, orthodontics, and cephalometry owners SHALL be extended instead of duplicated.
- `CORE-REQ-006` Configurable clinic identity, language, print identity, operating rules, and financial rules SHALL come from central settings rather than component or generator hardcoding. Arabic RTL and English LTR SHALL be supported, including an independently selectable print language.
- `CORE-REQ-007` Sensitive actions SHALL be checked by granular server permissions and reflected in the UI. Hidden controls SHALL NOT replace server authorization. Overrides, refunds, discounts, priority changes, and approvals SHALL be audited.
- `CORE-REQ-008` All modules SHALL reference one patient record and patient identifier. Duplicate detection SHALL warn on normalized phone, WhatsApp, name, birth date, and identity number where available; merging SHALL require explicit permission and review.
- `CORE-REQ-009` Appointment scheduling SHALL enforce doctor working hours, duration, capacity, conflicts, leave, holidays, branch/room availability, and configurable grace/overbooking rules. It SHALL support YER clinic-local dates without UTC day drift.
- `CORE-REQ-010` Daily operations SHALL own check-in and the active queue. Normal ordering SHALL be FIFO with a configurable grace rule. VIP priority SHALL NOT exist. Emergency priority SHALL require a medical reason, authorized approval, and an audit record.
- `CORE-REQ-011` One appointment SHALL NOT create duplicate active visits, queue items, or draft invoices. Repeated clicks and concurrent devices SHALL be handled through idempotency, locking, or database constraints.
- `CORE-REQ-012` Lab orders SHALL remain linked to the canonical patient, doctor, and visit and support the required lifecycle, delay alerts, escalation, receipt-before-delivery, remake tracking, cost, patient price, and payable state.
- `CORE-REQ-013` Finance SHALL support YER, SAR, and USD while preserving original amount/currency, transaction exchange rate, base/account value, payment method, treasury, user, and timestamp. Reports SHALL not add unlike currencies without an explicit converted total and rate.
- `CORE-REQ-014` Inventory, administration, and reports SHALL use the same canonical records, permissions, settings-driven print identity, and auditable financial links.
- `CORE-REQ-015` Basic orthodontics SHALL stabilize inside the patient record before new clinical AI or VTO work resumes.
- `CORE-REQ-016` Cephalometry SHALL resume only after navigation, central settings, identity/printing, permissions, unified patient record, appointments/capacity, daily operations/queue, doctor clinic, lab, multi-currency finance, end-to-end patient journey tests, critical-defect closure, and green `main` CI are all verified.
- `CORE-REQ-017` Each implementation slice SHALL use an independent branch and small PR containing phase, Spec ID, problem, root cause, changes, tests, and risks. Existing migrations SHALL not be edited casually and new migrations require chain review.
- `CORE-REQ-018` Tests, screenshots, logs, fixtures, commits, and PRs SHALL NOT contain real patient data. Runtime-only claims SHALL be marked `Needs runtime verification`.

## Phase 0 Acceptance

- All cephalometry work is committed, pushed, draft, and visibly paused.
- `main` is synchronized and its CI/test baseline is recorded, including failures before fixes.
- Critical findings are severity-ranked and supported by file, test, CI, or runtime evidence.
- Migration and duplicate-route risks are explicitly listed.
- The current-state report, priority plan, and task list identify the first small Phase 1 slice without implementing it in the Phase 0 PR.
