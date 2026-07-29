---
Task ID: 1
Agent: Main Agent
Task: Fix Finance V3 PDF printing issues — comprehensive audit and implementation

Work Log:
- Performed thorough audit of backend PDF infrastructure: 4 controllers, 3 QuestPDF document classes, PdfService, LabOrderPdfGenerator
- Performed thorough audit of frontend PDF infrastructure: Finance V3 tabs (InvoicesTab, CollectionsTab), daily-operations (4 inline downloads), patient-journey, lab page
- Identified 7 root causes: missing generic catch in 4 PDF endpoints, no font registration logging, no font copy in API.csproj, 4 duplicate inline PDF download implementations in daily-operations
- Created branch fix/finance-v3-pdf-printing from main
- Implemented backend fixes: PdfService font logging, 4 controllers with Arabic 500 catch blocks, API.csproj font copy
- Implemented frontend fixes: Replaced 4 inline PDF downloads in daily-operations/page.tsx with downloadPdfFromApi()
- Created PdfEndpointErrorHandlingTests.cs with 11 test cases
- Frontend build passed (npm run build)
- Pushed branch and created PR #345

Stage Summary:
- PR #345: https://github.com/Aqlanf10/aqlan-dental/pull/345
- Branch: fix/finance-v3-pdf-printing
- 7 files changed, 291 insertions, 41 deletions
- Backend: PdfService.cs, PaymentsController.cs, InvoicesController.cs, ReportsController.cs, API.csproj
- Frontend: daily-operations/page.tsx
- Tests: PdfEndpointErrorHandlingTests.cs (new)
- No schema changes, no migrations, no financial logic changes
- dotnet build/test cannot run locally (no SDK), pending CI verification

---

## YOLO Sprint 2 — Service Catalog Enhancements (2026-06-26)

Task ID: YOLO-S2
Agent: Main Agent
Branch: `yolo-s2/services` (1 commit ahead of `main`)
PR: https://github.com/Aqlanf10/aqlan-dental/pull/553

### Scope
Enhance the service catalog with treatment-package composition, per-service
consumable materials, optional color tagging, and an optional Contract →
TreatmentPackage link. All changes additive & backward-compatible — every new
column nullable, the migration fully idempotent.

### Work Log
- Read CLAUDE.md + worklog tail; checked out main, created branch `yolo-s2/services`
  (the branch already existed locally from a prior partial attempt — leftover
  untracked files were preserved and built upon).
- **Backend Domain**:
  - `ClinicService.Color` (hex varchar(20), nullable) + `Consumables` + `PackageLinks` navs
  - `TreatmentPackage.Services` collection
  - `Contract.PackageId` (nullable FK → TreatmentPackages, ON DELETE SET NULL) + `Package` nav
  - New entities: `TreatmentPackageService` (PackageId, ClinicServiceId, Quantity=1, OverridePrice?) + `ServiceConsumable` (ClinicServiceId, InventoryItemId, Quantity=1, Notes?)
- **Backend EF Configurations**:
  - `ClinicServiceConfiguration.Color` (HasMaxLength(20))
  - `ContractConfiguration.PackageId` index + `HasOne(Package).WithMany().OnDelete(SetNull)`
  - `TreatmentPackageServiceConfiguration` — cascade on package, restrict on service, unique composite index
  - `ServiceConsumableConfiguration` — cascade on service, restrict on inventory, unique composite index
- **Backend Migration** `20260713000000_AddServicePackagesConsumables`:
  - Idempotent raw SQL (ADD COLUMN IF NOT EXISTS / CREATE TABLE IF NOT EXISTS / CREATE INDEX IF NOT EXISTS)
  - FK creation guarded with `DO $$ BEGIN IF NOT EXISTS … END $$`
  - Down path: `DROP TABLE IF EXISTS` / `DROP INDEX IF EXISTS` / `DROP COLUMN IF EXISTS`
  - Mirrors the 20260712000000_AddAppointmentEnhancements pattern (raw SQL because
    the EF migration chain is historically broken per CLAUDE.md pitfall)
- **Backend Controllers**:
  - `ServicesSettingsController` accepts Color in Create/Update (validated hex via
    NormalizeColor helper; rejects malformed values with Arabic message)
  - `TreatmentPackagesController.GetById` now loads Services list + computed total
  - New endpoints on `TreatmentPackagesController`:
    - `POST /api/treatment-packages/{id}/services` (add service or bump qty if exists)
    - `PUT /api/treatment-packages/{id}/services/{serviceId}` (update qty/override)
    - `DELETE /api/treatment-packages/{id}/services/{serviceId}` (remove link)
  - New `ServiceConsumablesController` (GET/POST/DELETE) — admin-gated writes,
    Arabic error messages, duplicate-(service,item) guard with 409 Conflict
- **Backend Contract / Finance**:
  - `ContractListDto` + `ContractDetailDto`: added PackageId/PackageName/PackageColor
  - `CreateContractRequest` + `UpdateContractRequest`: added PackageId
  - `FinanceService.CreateContractAsync`: validates package exists + active; resolves
    Guid.Empty → null (clear)
  - `FinanceService.UpdateContractAsync`: PATCH semantics (null = leave unchanged,
    Guid.Empty = clear)
  - `FinanceService.MapContractList` + `GetContractByIdAsync`: include(c => c.Package)
    and map package fields
- **Backend Tests**:
  - Existing `ServiceConsumableTests` (9 tests) verified
  - New `TreatmentPackageServiceTests` (9 tests): link CRUD, navigation, cascade
    delete, multi-service package, Contract.PackageId nullable set/read/clear
  - Full suite: **2030/2030 pass** (was 2021 before; +9 from new tests)
- **Frontend Types**:
  - `types/appointment.ts`: `TreatmentPackageService`, `ServiceConsumable`,
    `UpsertPackageServiceRequest`, `CreateServiceConsumableRequest`;
    extended `TreatmentPackage` with `services` + `computedTotal`
  - `types/finance.ts`: extended `Contract` with `packageId/packageName/packageColor`
  - `finance-v3/components/types.ts`: extended `ContractListItem` with same fields
- **Frontend Pages (Arabic RTL)**:
  - `settings/services/page.tsx`: color picker (with clear button) in the form +
    اللون column in the table + color swatch in the Name cell; expandable per-row
    panel that lists the service's ServiceConsumables and lets the admin add/remove
    materials from the inventory catalog (with quantity + notes)
  - `settings/packages/page.tsx`: expandable per-row panel showing the package's
    services (quantity, override price, effective unit price, line total,
    computed catalog total) + add/remove service UI (with override-price field
    per package-service link)
  - `finance-v3/components/ContractsTab.tsx`: package selector in Create Contract
    modal (auto-fills TotalAmount from package price when total is 0) + new
    الباقة column in the contracts list showing the package name with color swatch
- **Verification**:
  - `dotnet build -c Release` — 0 errors (102 pre-existing warnings, none new)
  - `dotnet test` — 2030/2030 pass
  - `npx tsc --noEmit` — clean
  - `npm run lint` — only pre-existing warnings, no errors (removed one unused import)
  - `npx vitest run` — 166/166 pass
  - `npm run build` — succeeded
  - `scripts/check-mojibake.sh` — clean
- **Commit**: 1 commit, 28 files changed, +2067 / -97
- **PR**: https://github.com/Aqlanf10/aqlan-dental/pull/553 (created via GitHub REST API — `gh` CLI not installed in sandbox)

### Stage Summary
- Branch: `yolo-s2/services`
- PR #553: https://github.com/Aqlanf10/aqlan-dental/pull/553
- 28 files changed, +2067 / -97
- 9 new files (entities, configs, controller, DTO, migration, 2 test files, plus the existing untracked Sprint 2 leftovers that were completed)
- 19 modified files (3 entities, 3 DTOs, 2 controllers, AppDbContext, 2 EF configs, FinanceService, 4 frontend pages/components, 3 type files, 1 modal)
- No migrations deleted. No framework swap. No features removed.
- All new columns nullable + idempotent migration (re-runnable on hot-fixed DBs).
- All new error messages in Arabic.
- No exception details leaked in HTTP responses.
- ServiceConsumables are catalog metadata only — no auto-decrement of Inventory (later sprint).
- Contract.PackageId is catalog metadata only — pricing still driven by Contract.TotalAmount.
- **CI verified green on commit 67cf1b5** (2026-06-26):
  - Backend — Build & Test: ✅ success
  - Frontend — Lint, Type-check & Build: ✅ success
  - E2E — Playwright: ✅ success
  - Arabic Mojibake Guard: ✅ success
  - Vercel Preview Comments: ✅ success
- **Local re-verification (2026-06-26, sandbox)**:
  - `dotnet build -c Release` — 0 errors (102 pre-existing warnings, none new)
  - `dotnet test` — **2030/2030 pass** (incl. 19 YOLO-S2 tests: 9 TreatmentPackageService + 10 ServiceConsumable)
  - `npx tsc --noEmit` — clean (exit 0)
  - `npx vitest run` — **166/166 pass**
  - `scripts/check-mojibake.sh` — clean

---

## YOLO Sprint 4 + 5 — Inventory Enhancements + Patient Segments (2026-06-26)

### Sprint 4 — Inventory enhancements
- `Inventory` entity gains 5 new nullable fields (parallel to the existing `ExpiryDate`):
  - `MinStockLevel` (numeric 12,2) — parallel decimal threshold to the legacy int `MinQuantity`; kept distinct so existing low-stock logic (`Quantity <= MinQuantity`) is untouched
  - `PurchaseUnit` (varchar 30) — e.g. كرتون، عبوة، كيلو
  - `ConsumptionUnit` (varchar 30) — e.g. قطعة، مل، جرام
  - `ImageUrl` (varchar 500) — thumbnail URL for visual identification
  - `WarehouseLocation` (varchar 100) — bin/shelf label, indexed for fast lookups
- **Migration `20260714000000_AddInventoryEnhancements`**: idempotent raw SQL (`ADD COLUMN IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS`) — safe on databases where the C-08 startup hotfix already added the columns. Mirrors the existing `20260712000000_AddAppointmentEnhancements` pattern (raw SQL because the EF migration chain is historically broken per CLAUDE.md pitfall).
- **StartupDatabaseMaintenance**: `EnsureInventoryEnhancementsSchemaAsync` hotfix mirrors the migration, runs unconditionally on every boot so the app stays healthy even if `ENABLE_STARTUP_DB_MAINTENANCE=false`.
- `InventoryConfiguration`: register the new properties + `WarehouseLocation` index.
- `InventoryController` + DTO: new fields in `CreateInventoryItemRequest` (validator with Arabic messages) + `GetAll` response (incl. computed `IsBelowMinStockLevel`) + Create/Update mappings.
- **Frontend** (Arabic RTL):
  - `types/inventory.ts`: extend `InventoryItem` + `CreateInventoryItemRequest` with the new fields
  - `InventoryFormModal`: add fields for `ExpiryDate`, `BatchNumber`, `MinStockLevel`, `PurchaseUnit`, `ConsumptionUnit`, `ImageUrl`, `WarehouseLocation` with Arabic labels and helper text
  - `inventory/page.tsx` table: image thumbnail (with graceful fallback to `ImageIcon`), warehouse-location column with `MapPin` icon, below-min-stock badge under item name, purchase/consumption unit subtext

### Sprint 5 — Patient segments
- **Entities** (`PatientSegment` + `PatientSegmentMember`, both `BaseEntity` → soft-delete + global query filter):
  - `PatientSegment`: Name, Description, Color, IsDynamic, QueryJson (reserved for custom dynamic segments later)
  - `PatientSegmentMember`: SegmentId, PatientId, AddedAt — `UNIQUE (SegmentId, PatientId)` so a patient appears at most once per custom segment
  - `PatientSegmentBuiltInKeys` static class with 4 stable keys: `builtin:ortho-overdue`, `builtin:outstanding-balance`, `builtin:no-recent-visit`, `builtin:lab-ready`
- **Migration `20260715000000_AddPatientSegments`**: idempotent raw SQL (`CREATE TABLE IF NOT EXISTS` + `DO $$ ... END $$` FK guards checking `pg_constraint`) — safe on databases where the C-08 startup hotfix already created the tables.
- **StartupDatabaseMaintenance**: `EnsurePatientSegmentsSchemaAsync` hotfix mirrors the migration.
- `PatientSegmentConfiguration` + `PatientSegmentMemberConfiguration`: unique index, cascade delete on both FKs (Segment → Members, Patient → Memberships).
- `AppDbContext`: register `PatientSegments` + `PatientSegmentMembers` DbSets.
- **`PatientSegmentsController`** (`[Authorize(Policy = "AdminOnly")]`):
  - `GET /api/patient-segments` — returns 4 built-in dynamic segments (computed at read time, not stored) + custom segments from DB
  - `GET /api/patient-segments/{key}/members` — built-in by Key, custom by Guid; built-in memberships include a `Reason` field (overdue amount, days since visit, appliance type, etc.)
  - `POST /api/patient-segments` — create custom segment
  - `POST /api/patient-segments/{id}/members` — add member (duplicate guard with Arabic 400)
  - `DELETE /api/patient-segments/{id}/members/{patientId}` — soft-delete member
  - `DELETE /api/patient-segments/{id}` — soft-delete segment (members cascade)
  - All error messages in Arabic per CLAUDE.md; no exception details leaked.
- **Pre-built dynamic segments**:
  - **مرضى تقويم متأخرون** — active OrthoCase patients whose latest OrthoVisit's `NextAppointmentDate` is in the past
  - **مرضى عليهم مبالغ** — patients with outstanding balance > 0 (contracts + non-draft invoices − payments)
  - **مرضى لم يحضروا** — patients with no Visit in the last 90 days (must have at least one visit ever — excludes brand-new walk-ins)
  - **مرضى المختبر الجاهز** — patients with at least one LabOrder in `Ready` status
- **Frontend `/patient-segments` page** (Arabic RTL):
  - Summary strip (total segments, built-in count, total members)
  - Card grid for built-in dynamic segments (4 cards with color stripe + icon + member count)
  - Card grid for custom segments with delete button
  - Members modal with debounced search + CSV export (UTF-8 BOM for Excel Arabic)
  - Create segment modal with 8 color presets
  - Add member modal with debounced patient search (300ms) using `/api/patients?search=`
  - Floating add-member button when a custom segment is open
  - Low-data warning when all segments are empty (helpful onboarding cue)
- **Sidebar**: add 'مجموعات المرضى' entry under the main section (Admin only, `Layers` icon)
- **`routePermissions.ts`**: add `/patient-segments` (Admin only — matches backend `[Authorize(Policy = "AdminOnly")]`)

### Tests
- `InventoryEnhancementsMigrationIdempotencyTests` (11 tests) — verifies Up/Down SQL uses `IF NOT EXISTS` / `IF EXISTS` guards, all 5 new columns are nullable, `ExpiryDate` is not touched (already existed), no destructive ops in Up, round-trip safety.
- `PatientSegmentsMigrationIdempotencyTests` (13 tests) — verifies both tables use `CREATE TABLE IF NOT EXISTS`, indexes use `CREATE INDEX IF NOT EXISTS`, FKs are guarded by `pg_constraint` check, `ON DELETE CASCADE` on both FKs, `UNIQUE (SegmentId, PatientId)`, Members dropped before Segments in Down.
- `PatientSegmentsControllerTests` (18 tests) — GetList always returns 4 built-ins with stable keys + Arabic names + IsDynamic/IsBuiltIn flags; empty DB → zero member counts; custom CRUD (create → appears in GetList → delete hides it); AddMember duplicate/patient-not-found/segment-not-found guards with Arabic messages; RemoveMember soft-deletes; GetMembers for built-in + custom; OrthoOverdue + LabReady computation smoke tests with seeded data.

### Verification
- `dotnet build -c Release` — 0 errors (55 pre-existing warnings, none new)
- `dotnet test` (UnitTests) — **2082/2082 pass** (was 2040; +42 from new tests: 11 Inventory + 13 PatientSegments migration + 18 PatientSegments controller)
- `npx tsc --noEmit` — clean (exit 0)
- `npm run lint` — only pre-existing warnings, no errors
- `npx vitest run` — **166/166 pass**
- `npm run build` — succeeded (`/patient-segments` route at 11 kB / 155 kB First Load JS)
- `scripts/check-mojibake.sh` — clean
- Integration tests (Testcontainers) require Docker and are not run in the sandbox; CI will exercise them.

### Stage Summary
- Branch: `yolo-s4-s5/inventory-segments`
- PR #555: https://github.com/Aqlanf10/aqlan-dental/pull/555
- 21 files changed, +2933 / -29
- 10 new files (2 entities, 2 configurations, 2 migrations, 1 controller, 3 test files, 1 frontend type, 1 frontend page)
- 11 modified files (1 entity, 1 DbContext, 1 EF config, 1 model snapshot, 1 startup maintenance, 1 controller, 1 frontend type, 1 form modal, 1 inventory page, 1 sidebar, 1 routePermissions)
- No migrations deleted. No framework swap. No features removed.
- All new columns nullable + idempotent migrations (re-runnable on hot-fixed DBs).
- All new error messages in Arabic.
- No exception details leaked in HTTP responses.
- Pre-built dynamic segments are computed in code (not stored) — they always reflect current DB state with zero stale-cache risk.
- Custom segments use soft-delete (BaseEntity) — reversible, global query filter excludes tombstoned rows.
- CSV export uses UTF-8 BOM so Excel renders Arabic correctly (mirrors the existing `PatientTable.exportCsv` pattern).


---

## TD-021 PR A1 — Extract InvoiceLedgerService from FinanceService (2026-07-03)

Task ID: TD-021-PR-A1
Agent: Main Agent
Branch: `refactor/td-021-pr-a1-extract-invoice-ledger-service` (1 commit ahead of `main`)
Linked plan: `docs/technical-debt/TD-021-god-service-extraction-plan.md` — Part A, PR A1 (lowest-risk, most isolated slice)

### Scope
First slice of the FinanceService god-service decomposition. Move the two
invoice-ledger posting methods (`PostInvoiceIssuedEntryAsync` +
`ReverseInvoiceIssuedEntryAsync`) out of the 2256-line `FinanceService` into a
focused `InvoiceLedgerService` behind its own `IInvoiceLedgerService` interface.
Pure code move — no business-logic change, no migration, no schema change.

The TD-021 plan classifies this slice as the safest first move because the two
methods are self-contained: their only dependencies are `db`,
`journalEntryService`, `currentUser`, and `logger` — they share no private
helpers with the rest of FinanceService. (The Payments + Supplier/Refund
clusters share `DualWrite*` + `ResolveTreasuryNoSaveAsync` and must move
together in a later PR A4.)

### Work Log
- Read CLAUDE.md + AGENT_START_HERE.md + ROADMAP.md + REMAINING_TASKS_PLAN.md +
  TD-021 plan + technical-debt-register.md to understand project state.
- Verified the other five "remaining tasks" in REMAINING_TASKS_PLAN.md are
  already done: PatientJourneyController shrank from 2242→165 lines (CLIN-22 done),
  ortho/[id]/page.tsx shrank from 3469→312 lines with full _components/ split
  (FE-20 done). The active remaining work is TD-021 (god-service extraction).
- Created branch `refactor/td-021-pr-a1-extract-invoice-ledger-service` from `main`.
- **New file** `Application/Interfaces/Services/IInvoiceLedgerService.cs`:
  - Two methods: `PostInvoiceIssuedEntryAsync(Guid invoiceId)`,
    `ReverseInvoiceIssuedEntryAsync(Guid invoiceId)`.
  - XML doc explains the move, the slicing rationale, and points to TD-021.
- **New file** `Infrastructure/Services/InvoiceLedgerService.cs`:
  - Primary constructor: `AppDbContext db, ICurrentUserService currentUser,
    IJournalEntryService journalEntryService, ILogger<InvoiceLedgerService> logger`.
  - Bodies are byte-for-byte identical to the previous FinanceService methods.
- **Modified** `Application/Interfaces/Services/IFinanceService.cs`:
  - Removed the two methods from the interface.
  - Added an inline NOTE comment pointing callers to `IInvoiceLedgerService`.
- **Modified** `Infrastructure/Services/FinanceService.cs`:
  - Deleted the two method bodies (~78 lines).
  - File shrank from 2256 → 2178 lines.
- **Modified** `API/Controllers/InvoicesController.cs`:
  - Issue endpoint: replaced `GetRequiredService<IFinanceService>()` with
    `GetRequiredService<IInvoiceLedgerService>()`.
  - Cancel endpoint: same replacement. Both with TD-021 PR A1 traceability comments.
- **Modified** `API/Controllers/FinanceV3Controller.cs`:
  - Added `IInvoiceLedgerService invoiceLedgerService` to the primary constructor.
  - Cancel endpoint: switched from `financeService.ReverseInvoiceIssuedEntryAsync`
    to `invoiceLedgerService.ReverseInvoiceIssuedEntryAsync`.
- **Modified** `API/Configuration/ServiceRegistrationConfiguration.cs`:
  - Added `services.AddScoped<IInvoiceLedgerService, InvoiceLedgerService>()`
    with comment explaining the extraction.
- **Modified 7 existing test files** to use the new service:
  - `Finance/FinanceV3JournalPostingTests.cs` — 1 site
  - `Finance/FinanceV3AccountingSafetyTests.cs` — 5 sites
  - `Finance/FinanceV3FinalBlockingTests.cs` — 5 sites (3 mock setups + 2 real calls)
  - `Finance/FinanceV3ControllerTests.cs` — 1 ctor call
  - `Finance/FinanceV3IntegrationFixTests.cs` — 1 ctor call
  - `Finance/DoctorCommissionsTests.cs` — 1 ctor call
  - `TechnicalDebtCleanupTests.cs` — 2 ctor calls
- **New file** `Finance/InvoiceLedgerServiceTests.cs` — 5 characterization tests:
  1. `PostInvoiceIssuedEntry_CreatesBalancedPostedJournalEntry_DebitReceivable_CreditRevenue`
  2. `PostInvoiceIssuedEntry_WhenInvoiceNotInIssuedStatus_ThrowsArgumentException`
  3. `PostInvoiceIssuedEntry_WhenInvoiceMissing_ThrowsArgumentException`
  4. `ReverseInvoiceIssuedEntry_CreatesPostedReversal_ThatNetsOriginalToZero`
  5. `ReverseInvoiceIssuedEntry_WhenNoOriginalExists_DoesNotThrow_AndPersistsNoReversal`
  - Per the TD-021 plan: "add characterization tests *before* moving if a slice
    is thin on coverage." Coverage already existed via FinanceV3* tests, but
    this file names the contract directly against the new interface so future
    PRs (A2–A4) cannot silently regress it.

### Stage Summary
- Branch: `refactor/td-021-pr-a1-extract-invoice-ledger-service`
- Commit: `f30946b0`
- 15 files changed, +470 / -169
- 3 new files (interface, implementation, characterization tests)
- 12 modified files (2 controllers, 1 DI config, 1 interface, 1 service, 7 test files)
- `FinanceService.cs` shrank from 2256 → 2178 lines (first slice of the god-service decomposition)
- `dotnet build -c Release` — 0 errors (61 pre-existing warnings, none new)
- `dotnet test tests/AqlanDentalPro.UnitTests` — **2250/2250 pass** (was 2245; +5 new characterization tests)
- `npx tsc --noEmit` (frontend) — clean (frontend untouched, but verified as a sanity check)
- `npx vitest run` (frontend) — 179/179 pass
- `scripts/check-mojibake.sh` — clean (Arabic strings preserved correctly)
- No migrations, no schema changes, no business logic changes — pure code move
- All Arabic error messages preserved verbatim
- No exception details leaked in HTTP responses
- Next slices (per TD-021 plan, in order): PR A2 (FinanceReadService),
  PR A3 (ContractService), PR A4 (PaymentService + SupplierRefundService — must
  move together because they share DualWrite* + ResolveTreasuryNoSaveAsync helpers)
