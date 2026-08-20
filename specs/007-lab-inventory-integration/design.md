# 007 Lab Inventory Design

- Lab frontend: `frontend/src/app/(dashboard)/lab/`, `frontend/src/components/lab/`, `frontend/src/types/lab.ts`.
- Inventory frontend: `frontend/src/app/(dashboard)/inventory/`, `frontend/src/components/inventory/`, `frontend/src/types/inventory.ts`.
- Backend owners: `LabOrdersController.cs`, `LabPayablesController.cs`, `LabReportsController.cs`, `LabsController.cs`, `LabWorkTypesController.cs`, `LabWorkPricesController.cs`, `InventoryController.cs`, `PurchaseOrdersController.cs`, `SuppliersController.cs`.
- Services: `LabOrderQueryService`.
- Entities: lab and inventory entity sets in `AppDbContext.cs`.
- Permissions: lab permission keys, `StaffOnly`, `AdminOnly`, finance/report policies.

Allowed files: lab/inventory owners and tests.

Forbidden files: creating parallel supplier/lab/inventory modules or unreviewed finance logic.

---

## Chairside Parity Design (CORE-LAB-CHAIR)

Evidence for every gap: `docs/audits/LAB_CHAIRSIDE_PARITY_ANALYSIS_2026-08-20.md`.

### Placement

| Requirement | Touches | Deliberately NOT touched |
|---|---|---|
| `LABINV-REQ-006` tooth selector | `components/lab/NewLabOrderModal.tsx`, `EditLabOrderModal.tsx`, new `components/lab/ToothPicker.tsx` | `LabOrderItem` schema — writes existing `toothNumber` |
| `LABINV-REQ-007` shade picker | same modals, new `components/lab/ShadePicker.tsx`, `Settings` key `lab.shade_guide` | `LabOrderItem.Shade` schema |
| `LABINV-REQ-008` scannable code | `LabOrderPdfGenerator`, new lookup endpoint on `LabOrdersController`, new scan screen under `app/(dashboard)/lab/` | no new identifier column — derives from `OrderNumber` |
| `LABINV-REQ-009` WhatsApp dispatch | `OrdersPanel.tsx` + order modals; reuses the `wa.me` pattern already in `settings/labs/page.tsx` | `Lab` entity |
| `LABINV-REQ-010` exchange rates | new `IExchangeRateService` in Infrastructure, `Settings` keys, order modals read it | `LabOrder.ExchangeRateToYer` column and finance sync |
| `LABINV-REQ-011` consumables | `InventoryController` (owner API) + a link record; lab order cost read path | any direct inventory write from lab code |

### Key design constraints

1. **No schema change for 006/007/008.** Tooth and shade selectors write the existing
   string columns; the scannable code is derived from `OrderNumber`, which already has
   a unique index (`20260527000000_AddLabOrderNumberUniqueIndex`).
2. **Scan lookup is a permission-checked read, not a shortcut.** It resolves through the
   same `LabOrderQueryService` path used by the orders list, so branch isolation and
   `lab` permission keys apply unchanged. A code for an out-of-scope order returns the
   standard not-found/forbidden shape.
3. **Exchange rate is read-through, never write-through.** The service resolves a rate;
   the order still stores `ExchangeRateToYer` exactly as today, so
   `LabOrderFinanceSyncService` and commission logic are untouched. The rate's origin
   (preset / live / manual) is recorded for audit.
4. **The known trap applies.** `LabOrders.DoctorId` references `Doctors.Id`, not
   `Users.Id` — any new query joining doctors converts through `Doctors.UserId`
   (`CLAUDE.md`).
5. **Settings, not constants.** Shade guide, market presets, provider URL, and refresh
   interval all live in `Settings`.

### Allowed files

Lab frontend components and panels, `LabOrdersController`, `LabOrderPdfGenerator`,
a new exchange-rate service and its registration, `Settings` seed keys, and their tests.

### Forbidden files

`LabOrderFinanceSyncService`, `CommissionService`, `TreasuryResolutionService`,
supplier/payables controllers, and any historic migration.
