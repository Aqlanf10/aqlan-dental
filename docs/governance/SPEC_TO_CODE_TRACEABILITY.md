# Spec To Code Traceability

Initial table based on static repo inspection. Incomplete rows are marked `Needs completion in future sprint`.

| Spec ID | Module | Requirement | Files implementing it | Tests | Status | Last updated | Risk |
|---|---|---|---|---|---|---|---|
| `MS-REQ-001` | Clinic identity | Settings-backed clinic identity | `FinanceClinicIdentity.cs`, `SettingsController.cs`, PDF generators | Finance PDF tests | partial | 2026-07-02 | Medium |
| `MS-REQ-002` | Arabic RTL | Arabic RTL UI | `frontend/src/app/layout.tsx`, `globals.css`, Arabic UI files | Encoding guard | active | 2026-07-02 | Medium |
| `MS-REQ-003` | Roles | User roles and policies | `UserRole.cs`, `AuthorizationPolicyConfiguration.cs`, `usePermissions.ts` | Authorization/Permissions tests | active | 2026-07-02 | High |
| `MS-REQ-004` | Patients | Patient access/privacy | `PatientAccessFilter.cs`, `PatientAccessService`, `PatientsController.cs` | Authorization, portal, patient tests | active | 2026-07-02 | High |
| `MS-REQ-005` | Daily operations | Reception workflow | `daily-operations/`, `DailyOperationsController.cs`, `PatientJourneyController.cs`, `ClinicQueueController.cs` | DailyOperations, ClinicQueue tests | active | 2026-07-02 | High |
| `MS-REQ-006` | Appointments | Appointment scheduling | `AppointmentsController.cs`, `AppointmentService.cs`, appointment UI/hooks | Appointment tests | active | 2026-07-02 | Medium |
| `MS-REQ-007` | Queue | Waiting queue | `ClinicQueueController.cs`, `ClinicQueueItem`, daily operations modules | ClinicQueue tests | active | 2026-07-02 | Medium |
| `MS-REQ-008` | Doctor clinic | Doctor workspace | `doctor-clinic/`, patient journey/visit APIs | Needs completion in future sprint | needs review | 2026-07-02 | High |
| `MS-REQ-009` | Orthodontics | Ortho workspace | `ortho/`, `components/ortho/`, `OrthoCasesController.cs`, `OrthoService` | Ortho tests | active | 2026-07-02 | High |
| `MS-REQ-010` | Cephalometry | Ceph and draft AI | `ceph/`, `components/ceph/`, `CephController.cs`, `CephAiDraftService` | Ceph tests | active, runtime review needed | 2026-07-02 | High |
| `MS-REQ-011` | Surgery | Surgery and ortho-surgical | `surgery/`, `ortho-surgical/`, `SurgeryController.cs`, `OrthoSurgicalCasesController.cs` | Surgery and OrthoSurgical tests | active | 2026-07-02 | High |
| `MS-REQ-012` | Finance | Finance V3 | `finance-v3/`, `FinanceV3Controller*.cs`, `FinanceService.cs`, `TreasuryResolutionService.cs` | Finance tests | active | 2026-07-02 | Critical |
| `MS-REQ-013` | Lab | Lab orders/payables/reports | `lab/`, `LabOrdersController.cs`, `LabPayablesController.cs`, `LabReportsController.cs` | Lab tests | active | 2026-07-02 | High |
| `MS-REQ-014` | Inventory | Inventory/purchases/suppliers | `inventory/`, `InventoryController.cs`, `PurchaseOrdersController.cs` | Inventory tests | active | 2026-07-02 | Medium |
| `MS-REQ-015` | Reports | Reports/PDF | `ReportsController.cs`, `PdfService.cs`, PDF generators, print pages | PDF tests | active | 2026-07-02 | High |
| `MS-REQ-016` | Settings | Settings-backed rules | `SettingsController.cs`, settings pages, `FinanceSettingsKeys.cs` | Settings/finance tests | partial | 2026-07-02 | High |
| `MS-REQ-017` | Navigation | Sidebar/routes | `Sidebar.tsx`, `routePermissions.ts`, dashboard layout | Needs completion in future sprint | active | 2026-07-02 | Medium |
| `MS-REQ-018` | Production | Deployment/CI safety | `Program.cs`, `.github/workflows/ci.yml`, `encoding-guard.yml` | CI | active | 2026-07-02 | High |
| `001` | Navigation | No duplicate routes | `Sidebar.tsx`, `routePermissions.ts` | Needs completion in future sprint | active | 2026-07-02 | Medium |
| `NAV-REQ-001` | Navigation | One canonical staff sidebar | `frontend/src/components/layout/Sidebar.tsx` | Needs completion in future sprint | documented | 2026-07-02 | Medium |
| `NAV-REQ-002` | Navigation | Dashboard and command center ownership | `frontend/src/app/(dashboard)/page.tsx`, `frontend/src/app/(dashboard)/clinic-command-center/page.tsx` | Needs runtime verification | needs owner decision | 2026-07-02 | Medium |
| `NAV-REQ-003` | Navigation | Explicit root dashboard guard behavior | `frontend/src/lib/routePermissions.ts`, `frontend/src/app/(dashboard)/layout.tsx` | Needs route guard tests | needs review | 2026-07-02 | Medium |
| `NAV-REQ-004` | Daily operations | Queue and patient journey redirects stay compatibility-only | `frontend/src/app/(dashboard)/clinic-queue/page.tsx`, `frontend/src/app/(dashboard)/patient-journey/page.tsx` | Needs runtime verification | documented | 2026-07-02 | Medium |
| `NAV-REQ-006` | Lab | Accountant lab subroute access matches visible links | `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts` | Needs role smoke/tests | mismatch found | 2026-07-02 | High |
| `NAV-REQ-007` | Lab/settings | BranchManager lab settings access matches visible links | `frontend/src/components/layout/Sidebar.tsx`, `frontend/src/lib/routePermissions.ts` | Needs role smoke/tests | mismatch found | 2026-07-02 | High |
| `NAV-REQ-010` | Ortho/surgery | Ortho-surgical route remains contextual or owner-approved visible | `frontend/src/app/(dashboard)/ortho-surgical/page.tsx`, `frontend/src/lib/routePermissions.ts` | Needs runtime verification | needs owner decision | 2026-07-02 | Medium |
| `NAV-REQ-013` | Finance | Finance remains a single sidebar entry | `frontend/src/app/(dashboard)/finance-v3/page.tsx`, `frontend/src/components/layout/Sidebar.tsx` | Needs completion in future sprint | documented | 2026-07-02 | Low |
| `NAV-REQ-014` | Daily operations | Daily operations owns reception tabs | `frontend/src/app/(dashboard)/daily-operations/` | Needs runtime verification | documented | 2026-07-02 | Medium |
| `002` | Daily operations | One reception workflow | daily operations UI/API | DailyOperations/ClinicQueue tests | active | 2026-07-02 | High |
| `003` | Doctor clinic | Doctor workflow | doctor-clinic UI, patient journey APIs | Needs completion in future sprint | needs review | 2026-07-02 | High |
| `004` | Orthodontics | Ortho workspace | ortho UI/API/services | Ortho tests | active | 2026-07-02 | High |
| `005` | Cephalometry | Draft AI and reports | ceph UI/API/services | Ceph tests | active | 2026-07-02 | High |
| `006` | Finance | Finance stability | finance UI/API/services/settings | Finance tests | active | 2026-07-02 | Critical |
| `007` | Lab/inventory | Lab-inventory integration | lab/inventory UI/API | Lab/Inventory tests | partial | 2026-07-02 | High |
| `008` | Users/roles | Auth and permissions | auth/users/policies/route guard | Auth/Authorization tests | active | 2026-07-02 | Critical |
| `009` | Reports | Reports/PDF | reports/print/PDF services | PDF tests | active | 2026-07-02 | High |

## Production Owner QA — Round 1 Continuation (2026-07-02)

Live production QA (admin) beyond the initial round. After the `ClinicServices.Color`
fix deployed, `/api/dashboard/stats` still 500'd, exposing two further schema-drift
500s on the two most-used operational screens, both fixed as idempotent startup
hotfixes (C-08 pattern, no migration). Two additional 500s (enum-type drift) were
investigated and documented for a follow-up round (owner decision needed).

| ID | Module | Finding | Code | Status | Fixed here | Runtime verify | Owner decision |
|----|--------|---------|------|--------|-----------|----------------|----------------|
| `QA1C-01` | Finance/Schema | `Contracts."Currency"` missing → `/api/dashboard/stats` + `/api/contracts` 500 (batched DDL rollback on the Treasury unique-index step) | `StartupDatabaseMaintenance.EnsureMultiCurrencyColumnsAsync` | fixed | yes | yes (post-deploy 200) | no |
| `QA1C-02` | Ortho/Journey | `Visits."WireUpper"/"WireLower"/"CurrentStage"`/`OrthoCaseId` missing (no hotfix) → `/api/patient-journey/today` 500 when a day has an appointment+visit | `StartupDatabaseMaintenance.EnsureVisitOrthoFieldsSchemaAsync` (new) | fixed | yes | yes (post-deploy 200) | no |
| `QA1C-03` | Inventory | `/api/suppliers` 500 — likely enum-type drift on `Suppliers."Type"` (not a missing column) | `SuppliersController.GetAll` | documented | no | n/a | yes |
| `QA1C-04` | Finance | `/api/finance-v3/expenses` 500 — likely enum-type drift on `OperationalExpenses."Category"/"ApprovalStatus"` | `FinanceV3Controller.GetExpenses` | documented | no | n/a | yes |
| `QA1C-05` | Navigation | Full admin sidebar crawl: no blank pages, no unexpected 404/redirect; only 500s were QA1C-01/02 screens | `Sidebar.tsx`, all dashboard routes | verified | n/a | n/a | no |
