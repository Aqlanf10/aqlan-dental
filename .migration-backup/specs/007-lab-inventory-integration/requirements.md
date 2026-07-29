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
