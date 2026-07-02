# 007 Lab Inventory Design

- Lab frontend: `frontend/src/app/(dashboard)/lab/`, `frontend/src/components/lab/`, `frontend/src/types/lab.ts`.
- Inventory frontend: `frontend/src/app/(dashboard)/inventory/`, `frontend/src/components/inventory/`, `frontend/src/types/inventory.ts`.
- Backend owners: `LabOrdersController.cs`, `LabPayablesController.cs`, `LabReportsController.cs`, `LabsController.cs`, `LabWorkTypesController.cs`, `LabWorkPricesController.cs`, `InventoryController.cs`, `PurchaseOrdersController.cs`, `SuppliersController.cs`.
- Services: `LabOrderQueryService`.
- Entities: lab and inventory entity sets in `AppDbContext.cs`.
- Permissions: lab permission keys, `StaffOnly`, `AdminOnly`, finance/report policies.

Allowed files: lab/inventory owners and tests.

Forbidden files: creating parallel supplier/lab/inventory modules or unreviewed finance logic.
