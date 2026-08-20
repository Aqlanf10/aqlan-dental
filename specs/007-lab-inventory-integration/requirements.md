# 007 Lab Inventory Integration Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/lab/`, `frontend/src/app/(dashboard)/inventory/`, `LabOrdersController.cs`, `LabPayablesController.cs`, `LabReportsController.cs`, `LabsController.cs`, `InventoryController.cs`, `PurchaseOrdersController.cs`, lab/inventory entities and tests.

- `LABINV-REQ-001`: Lab orders SHALL use existing lab controllers and UI.
- `LABINV-REQ-002`: Lab payables SHALL remain integrated with finance permissions.
- `LABINV-REQ-003`: Inventory SHALL remain admin-owned unless a spec changes it.
- `LABINV-REQ-004`: Service consumables and lab costs SHALL not create hidden finance side effects.
- `LABINV-REQ-005`: Lab pricing/settings SHALL use existing settings routes.

## Target State

Lab, payables, and inventory cooperate without duplicate supplier or cost modules.

## Risks

Duplicated suppliers, unpaid lab work, wrong commission deduction, inventory drift.

## Allowed Future Work

Improve overdue lab visibility, lab reports, pricing UI, inventory adjustments, purchase orders.

## Forbidden Future Work

Second lab order module, inventory writes outside owner API, finance bypass.

## Acceptance Criteria

- WHEN lab costs affect finance THEN finance spec SHALL be checked.
- WHEN inventory is consumed THEN inventory owner APIs/entities SHALL be used.
- Needs runtime verification for full lab-to-finance workflow.

---

## Chairside Parity Extension (CORE-LAB-CHAIR) — 2026-08-20

Owner directive: bring the field/chairside capabilities of the `aqlanf10/aqlan-lab`
Android app into the existing web lab module. Gap analysis with per-item code
evidence: `docs/audits/LAB_CHAIRSIDE_PARITY_ANALYSIS_2026-08-20.md`.

Scope is **six capabilities the web module lacks**, not a re-implementation of the
app. The web module is already ahead on order lifecycle, remakes, attachments,
payables, aging, finance/commission linkage, statements, reporting, permissions,
and branch isolation — none of that is touched.

- `LABINV-REQ-006`: Lab order items SHALL offer an FDI tooth selector that writes the
  same `toothNumber` field. Free text SHALL remain accepted so existing orders and
  non-standard notations keep working; the selector is an input aid, not a new
  storage format. No schema change.
- `LABINV-REQ-007`: Lab order items SHALL offer a shade picker whose options come from
  `Settings` (`lab.shade_guide`), defaulting to VITA Classical. Free text SHALL remain
  accepted. Values SHALL be normalised for reporting without rewriting stored history.
- `LABINV-REQ-008`: A lab order SHALL carry a scannable code derived from its existing
  `OrderNumber` — no new identifier and no new column. The order PDF SHALL render it,
  and an authenticated staff screen SHALL resolve a scanned code to the order the user
  is already permitted to see. Scanning SHALL NOT bypass any existing permission or
  branch check.
- `LABINV-REQ-009`: The order screen SHALL offer sending order details to the lab over
  WhatsApp using the lab's stored number. Message content SHALL come from `Settings`
  identity keys (`clinic.name`, `clinic.lead_doctor`, …) — no hardcoded clinic text.
  The action SHALL NOT transmit patient data beyond what the lab already receives on
  the printed order.
- `LABINV-REQ-010`: Exchange rates used on lab orders SHALL be resolvable from a single
  source of truth instead of manual per-order entry: named market presets held in
  `Settings` (`finance.fx.preset.*`) and an optional refresh from a configured provider.
  A manual override SHALL remain possible and SHALL be recorded as manual. A failed
  refresh SHALL surface as a failure and SHALL NOT silently substitute a stale or
  default rate.
- `LABINV-REQ-011`: Consumables attributable to a lab order SHALL be recordable against
  that order through the existing inventory owner APIs. No inventory write may bypass
  `InventoryController`, and no second consumption ledger may be created.

### Acceptance Criteria (Chairside Parity)

- WHEN a tooth or shade is chosen from a selector THEN the stored value SHALL be
  indistinguishable from the same value typed by hand.
- WHEN a scanned code resolves to an order outside the user's branch or permission
  scope THEN the system SHALL refuse with the standard Arabic authorization message and
  SHALL NOT reveal that the order exists.
- WHEN an exchange-rate refresh fails THEN the UI SHALL say so in Arabic and the
  previous rate SHALL remain visibly marked as stale — no silent fallback.
- WHEN a lab order is sent to WhatsApp THEN clinic identity SHALL be read from
  `Settings`, and the absence of a lab phone number SHALL produce an Arabic error, not
  a half-formed link.
- WHEN consumables are recorded against a lab order THEN inventory balances and the
  order cost SHALL both reflect it, through existing owner APIs only.

### Forbidden (Chairside Parity)

- Any second supplier, payables, inventory, or lab-order module.
- Any new patient-data path to an external host (the app's Firestore/Storage backup
  path is explicitly out of scope — see audit §4).
- Any local-PIN authentication path parallel to the system's existing auth.
- Any change to `LabOrder` status semantics, finance sync, or commission calculation.
