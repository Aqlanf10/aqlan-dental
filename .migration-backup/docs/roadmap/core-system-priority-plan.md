# Core System Priority Plan

- Status: Proposed by Phase 0 audit
- Spec: `specs/011-core-system-stabilization/`
- Operating journey: Patients -> Appointments -> Check-in -> Queue -> Doctor clinic -> Lab -> Accounting -> Next appointment

## Rules Of Execution

1. Work proceeds in phase order. A later phase cannot start while its predecessor's
   exit gate is unmet, except for a documented production/security incident.
2. Every slice starts from current `main`, uses an independent branch, and opens a
   small draft PR with phase, Spec ID, problem, root cause, tests, and risks.
3. Product fixes are not mixed with audit/governance PRs.
4. Existing migrations are not edited casually. Any schema change starts with chain,
   snapshot, startup-maintenance, and rollback review.
5. Runtime claims use synthetic data and are labeled `Needs runtime verification`
   until observed in an authenticated environment.
6. Each merged PR updates `core-system-execution-checklist.md` and this plan when its
   findings or order change.

## Urgent Safety Track

The following findings are release blockers even though their owning feature phases
occur later. They may enter an incident-priority PR after Phase 0 when reproduction
is complete:

- `CORE-F-001`: cross-route duplicate visit risk.
- `CORE-F-002`: migration/startup schema ownership risk for deployment changes.
- `CORE-F-003`: queue reorder request contract mismatch.
- `CORE-F-007`: mixed-currency totals presented without valid currency semantics.

An exception PR must remain narrowly scoped and may not be used to start unrelated
work from a later phase.

## Phase Plan

| Phase | Goal | Required evidence | Exit gate |
|---:|---|---|---|
| 0 | Freeze ceph and establish the baseline | Draft PR state, tests, CI, routes, permissions, migrations, severity report | Reports approved; first Phase 1 slice selected |
| 1 | Establish canonical architecture and navigation | Route inventory, one owner per capability, redirect policy, route/role contract tests | No competing active owner routes; sidebar/guards/server policy agree |
| 2 | Centralize settings, identity, languages, and printing | Settings schema, shared identity resolver, logo source, Arabic/English and print-language fixtures | UI and supported documents resolve one settings-driven identity/language contract |
| 3 | Enforce users, roles, and granular permissions | Action matrix, server guards, audit events, negative authorization tests | Sensitive actions are denied server-side unless explicitly granted and are audited |
| 4 | Unify patient record and integrity | Identifier contract, duplicate fixtures, identity number, merge review/audit | One patient owner; duplicate warnings and reviewed merge pass integration tests |
| 5 | Stabilize appointments and capacity | Working hours, duration, leave, holidays, room/branch capacity, conflict/concurrency/date tests | Deterministic booking with no silent conflict or clinic-day drift |
| 6 | Stabilize check-in and queue | FIFO/grace config, no VIP, emergency approval/reason/audit, privacy-safe display | Queue contract, role matrix, ordering, two-device tests, and screen privacy pass |
| 7 | Stabilize doctor clinic and visit lifecycle | One visit/draft invoice constraints, transition and repeated-click tests | Full appointment-to-checkout flow creates no duplicate records |
| 8 | Complete lab workflow | Required statuses, alerts/escalation, receipt gate, remakes, pricing/payable tests | Lab order follows patient/visit, cannot deliver before receipt, delays are actionable |
| 9 | Complete multi-currency finance | Original/account/base values, exchange-rate snapshots, cashier/treasury/refund/report/PDF tests | YER/SAR/USD remain reconcilable; unlike currencies are never silently added |
| 10 | Align inventory, administration, and reports | Canonical links, permissions, alerts, shared print contract | Operational reports reconcile to canonical records and print identity |
| 11 | Stabilize basic orthodontics | Patient-owned workspace, records, diagnosis, plans/stages/visits/retention tests | Core orthodontics is usable without advanced AI/VTO dependencies |
| 12 | Evaluate cephalometry return gate | All prior gates, green main CI, authenticated journey, no open Critical findings | Owner explicitly authorizes resumption and paused PRs are revalidated against main |

## Phase 1 Slices

### Merged Phase 1 Work

- `CORE-P1-S1` merged in PR #701. Reception now has a tested navigation and
  route-guard contract for the canonical appointment workflow. Authenticated
  Reception runtime verification remains part of the Phase 1 exit evidence.
- `CORE-P1-S2` merged in PR #702. Canonical capability owners and legacy aliases
  now have a checked registry, redirect tests, and role-parity tests.

### CORE-P1-S1 - Reception Appointment Route Alignment

Problem: Reception is included in backend `AppointmentAccess` and the target workflow,
but excluded from the `/appointments` sidebar item and route guard.

Scope:

- Add Reception to the existing canonical `/appointments` sidebar and route contract.
- Add tests proving route precedence for `/appointments/recall` and access for the
  appointment index/create/detail/edit routes.
- Verify that no second appointment page or API is introduced.

Out of scope: changing backend roles, appointment capacity, queue behavior, or visual
redesign. Those belong to later slices.

Exit: role navigation/guard tests pass, type/lint/frontend tests/build pass, and an
authenticated Reception smoke test is recorded or labeled `Needs runtime verification`.

### Proposed Remaining Phase 1 Order

- `CORE-P1-S2`: Create a checked canonical route/owner inventory and redirect tests.
- `CORE-P1-S3`: Remove policy drift by deriving sidebar and route guards from one
  frontend route manifest while retaining server authorization as authority.
- `CORE-P1-S4`: Document and test backend policy ownership for each canonical route.
- `CORE-P1-S5`: Add a CI signal that distinguishes executed authenticated E2E from
  skipped E2E; a skipped flow must not be presented as journey verification.

## Metrics

The checklist records these after every PR:

- Open Critical and High findings.
- Canonical routes with tested role contracts.
- Authenticated patient-journey steps passing.
- Backend line/branch coverage and whether thresholds are blocking.
- Frontend warning count.
- Fresh/legacy migration parity status.
- Multi-currency reconciliation fixture status.
- Cephalometry return-gate completion count.
