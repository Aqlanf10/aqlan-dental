# Sprint 2 — Daily Operations Production Completion: Backend Changes

**Task ID:** 1  
**Branch:** sprint/2-daily-operations-production-completion  
**Date:** 2026-06-02

## Summary

Implemented all 10 backend changes for Sprint 2. Build succeeds with 0 errors. EF Core migration created successfully.

## Changes Made

### 1. LabOrder Entity — Extended Fields
**File:** `backend/src/AqlanDentalPro.Domain/Entities/LabOrder.cs`

Added 6 new fields:
- `Shade` (string?) — dental shade/color code
- `RestorationType` (string?) — type of restoration (e.g., Zirconia Crown)
- `VisitId` (Guid?) — nullable link to Visit
- `DeliveredDate` (DateOnly?) — when delivered to patient
- `CancellationReason` (string?) — reason if cancelled
- `BranchId` (Guid?) — for multi-branch support

Added 1 navigation property:
- `Visit? Visit` — link to Visit entity

### 2. LabOrderConfiguration — New Indexes & FK
**File:** `backend/src/AqlanDentalPro.Infrastructure/Data/Configurations/LabOrderConfiguration.cs`

Added:
- Index on `BranchId` for multi-branch queries
- Index on `VisitId` for visit linkage
- FK relationship: `LabOrder.Visit → Visit` with `SetNull` on delete

### 3. PaymentMethodSetting Entity — NEW
**File:** `backend/src/AqlanDentalPro.Domain/Entities/PaymentMethodSetting.cs`

Created entity with fields: Name, Code, RequiresReferenceNumber, AccountName, AccountNumber, Notes, SortOrder, BranchId. Inherits IsActive from BaseEntity.

### 4. PaymentMethodSettingConfiguration — NEW
**File:** `backend/src/AqlanDentalPro.Infrastructure/Data/Configurations/PaymentMethodSettingConfiguration.cs`

Created configuration with:
- Unique index on `Code`
- Index on `BranchId`
- `HasQueryFilter(p => p.IsActive)` for global soft-delete
- Property constraints (MaxLength for Name, Code, AccountName, AccountNumber, Notes)

### 5. LabOrdersController — New Endpoints
**File:** `backend/src/AqlanDentalPro.API/Controllers/LabOrdersController.cs`

Added 5 new endpoints:
- `GET /api/lab-orders/today` — returns today's lab orders (SentDate, ExpectedDate, ReceivedDate, or DeliveredDate matches today)
- `GET /api/lab-orders/ready` — returns lab orders with status "ready" or "received"
- `POST /api/lab-orders/{id}/mark-received` — marks as received with notification
- `POST /api/lab-orders/{id}/mark-delivered` — marks as delivered to patient
- `POST /api/lab-orders/{id}/cancel` — cancels with reason

Updated existing endpoints:
- `CreateLabOrderRequest` — added Shade, RestorationType, VisitId
- `Create` method — populates new fields (Shade, RestorationType, VisitId)
- `GetAll` response — includes Shade, RestorationType, VisitId, DeliveredDate, CancellationReason
- `GetById` response — includes Shade, RestorationType, VisitId, DeliveredDate, CancellationReason

Added DTOs:
- `MarkReceivedRequest` — optional ReceivedDate
- `CancelLabOrderRequest` — required Reason

### 6. SettingsController — Payment Method Settings API
**File:** `backend/src/AqlanDentalPro.API/Controllers/SettingsController.cs`

Added 4 endpoints:
- `GET /api/settings/payment-methods` — list active payment methods (StaffOnly)
- `POST /api/settings/payment-methods` — create payment method (AdminOnly)
- `PUT /api/settings/payment-methods/{id}` — update payment method (AdminOnly)
- `POST /api/settings/payment-methods/{id}/toggle` — toggle active/inactive (AdminOnly)

Added DTOs:
- `CreatePaymentMethodRequest`
- `UpdatePaymentMethodRequest`

Updated controller constructor to inject `ICurrentUserService` for admin checks.

### 7. Receipt PDF Endpoint — Already Existed
**File:** `backend/src/AqlanDentalPro.API/Controllers/PaymentsController.cs`

The `GET /api/payments/{id}/pdf` endpoint already existed with the exact implementation requested. No changes needed.

### 8. DbContext — PaymentMethodSettings DbSet
**File:** `backend/src/AqlanDentalPro.Infrastructure/Data/AppDbContext.cs`

Added: `public DbSet<PaymentMethodSetting> PaymentMethodSettings => Set<PaymentMethodSetting>();`

### 9. DbSeeder — Default Payment Methods
**File:** `backend/src/AqlanDentalPro.Infrastructure/Data/Seed/DbSeeder.cs`

Added `SeedPaymentMethodSettingsAsync` method with upsert-by-Code logic for 7 default payment methods:
| Name | Code | RequiresReferenceNumber |
|---|---|---|
| نقداً | cash | false |
| بطاقة/شبكة | card | true |
| تحويل بنكي | bank_transfer | true |
| كريمي | karimey | true |
| جوالي | jawaly | true |
| حوالة | transfer | false |
| أخرى | other | false |

### 10. Daily Operations Report Endpoint — NEW Controller
**File:** `backend/src/AqlanDentalPro.API/Controllers/DailyOperationsController.cs`

Created new controller with:
- `GET /api/daily-operations/report?date=YYYY-MM-DD` — returns aggregated daily report
  - PatientCounts: Total, Waiting, InRoom, ReadyForCheckout, Completed, NoShow, LeftWithoutCompletion, Emergency
  - Financial: TotalCollected, ByPaymentMethod, NewDebts, PartialPayments, DraftInvoices, Discounts
  - LabOrders: Sent, Received, Delivered
  - ManagerOverrides count
  - TomorrowAppointments count

### 11. Migration
**File:** `backend/src/AqlanDentalPro.Infrastructure/Data/Migrations/20260602230911_Sprint2_LabOrderAndPaymentMethodSettings.cs`

EF Core migration created successfully, adding:
- 6 new columns to `LabOrders` table (Shade, RestorationType, VisitId, DeliveredDate, CancellationReason, BranchId)
- `PaymentMethodSettings` table with indexes
- `FK_LabOrders_Visits_VisitId` foreign key
- `IX_LabOrders_BranchId` and `IX_LabOrders_VisitId` indexes

## Build Result
- **Build succeeded** with 0 errors
- 58 pre-existing warnings (none from Sprint 2 changes)
- Fixed CS0108 warning by removing duplicate `IsActive` property from `PaymentMethodSetting` (inherited from `BaseEntity`)

---

**Task ID:** 3  
**Branch:** sprint/2-daily-operations-production-completion  
**Date:** 2026-06-03

## Summary

Implemented Commission Audit + Financial Closure Logic. Added collection-based commission endpoint, financial closure validation endpoint, and comprehensive test coverage for commission calculations, financial closure, and lab order lifecycle. Build succeeds with 0 errors.

## Changes Made

### 1. Collection-Based Commission Endpoint
**File:** `backend/src/AqlanDentalPro.API/Controllers/FinanceV3Controller.DoctorCommissions.cs`

Added new endpoint `GET /api/finance-v3/doctor-commissions/earned-from-collections` that calculates commission based on ACTUAL payment collections (not just invoice amounts).

Formula: `Commission = (Collected Amount - Lab Cost - Material Cost - Other Direct Costs) * Doctor Percentage`

Key features:
- Only payments that have been actually collected are counted
- Collection ratio is calculated per invoice: `collectionRatio = invoicePaid / invoiceTotal`
- Proportional costs are deducted based on collection ratio
- Supports branch isolation for non-admin users
- Supports date range filtering
- Returns: DoctorId, DoctorName, CasesCount, TotalServiceValue, TotalCollected, TotalLabCost, TotalMaterialCost, TotalOtherDirectCosts, NetCommissionableAmount, DoctorPercentage, CommissionDue, CommissionPaid, CommissionRemaining

### 2. Financial Closure Validation Endpoint
**File:** `backend/src/AqlanDentalPro.API/Controllers/PatientJourneyController.cs`

Added new endpoint `POST /api/patient-journey/{patientId}/validate-financial-closure` that validates whether a visit can be financially closed.

Business rules:
- No outstanding balance → `canClose: true`
- Outstanding balance with active ortho case or general treatment plan → `canClose: true, reasonCode: "MULTI_SESSION_PLAN"`
- Outstanding balance with manager override → `canClose: true, reasonCode: "MANAGER_OVERRIDE"`, audit log created
- Outstanding balance without plan or override → `canClose: false, reasonCode: "OUTSTANDING_BALANCE"`

Added `using System.Text.Json;` for `JsonSerializer.SerializeToDocument`.

Added DTO:
- `ValidateFinancialClosureRequest` with `ManagerOverride`, `ClosureReason`, `VisitId` properties

### 3. Commission Tests
**File:** `backend/tests/AqlanDentalPro.UnitTests/Services/CommissionCalculatorTests.cs`

Added 3 proportional commission tests for earned-from-collections scenarios:
- `ProportionalCommission_50PercentCollection_Returns50PercentOfFullCommission` — 50% paid → 50% commission
- `ProportionalCommission_ZeroCollection_ReturnsZero` — no payment → 0 commission
- `ProportionalCommission_FullCollection_ReturnsFullAmount` — full payment → full commission

**File:** `backend/tests/AqlanDentalPro.UnitTests/Finance/DoctorCommissionsTests.cs`

Added 5 earned-from-collections integration tests:
- `EarnedFromCollections_PartialPayment_ReturnsProportionalCommission` — Invoice 100k, paid 50k, 50% doctor → commission 25k
- `EarnedFromCollections_FullPayment_WithLabCost_ReturnsCorrectCommission` — Invoice 100k, paid 100k, lab 20k, 50% doctor → commission 40k
- `EarnedFromCollections_UnpaidInvoice_ReturnsZeroCommission` — Invoice 100k, paid 0 → commission 0
- `EarnedFromCollections_CancelledPayment_NotCounted` — Invoice with inactive payment → commission 0
- `EarnedFromCollections_BranchIsolation_ReturnsOnlyBranchData` — Non-admin user sees only their branch

### 4. Financial Closure Tests
**File:** `backend/tests/AqlanDentalPro.UnitTests/Journey/PatientJourneyTests.cs`

Added 4 financial closure validation tests:
- `ValidateFinancialClosure_NoOutstanding_ReturnsCanClose` — fully paid invoice → outstanding = 0
- `ValidateFinancialClosure_WithOutstanding_NoPlan_RequiresManagerOverride` — outstanding balance, no plan → cannot close
- `ValidateFinancialClosure_WithOutstanding_ActivePlan_AllowsClosure` — outstanding balance with active ortho case → allows closure
- `ValidateFinancialClosure_ManagerOverride_RecordsAuditLog` — manager override creates AuditLog entry with proper fields

Added `using System.Text.Json;` for `JsonSerializer.SerializeToDocument`.

### 5. Lab Order Tests
**File:** `backend/tests/AqlanDentalPro.UnitTests/LabOrders/LabOrderNumberGenerationTests.cs`

Added 5 lab order lifecycle tests using InMemory database:
- `MarkReceived_SetsStatusAndDate` — "sent" → "received" with ReceivedDate
- `MarkDelivered_RequiresReadyStatus` — cannot deliver from "sent", can deliver from "ready"
- `Cancel_WithReason_SetsCancellationReason` — cancel with reason, verify status and reason
- `Today_ReturnsOnlyTodayOrders` — filter by today's date (SentDate, ExpectedDate, ReceivedDate, DeliveredDate)
- `Ready_ReturnsOnlyReadyAndReceived` — filter by status "ready" or "received"

Added imports: `AqlanDentalPro.Domain.Entities`, `AqlanDentalPro.Infrastructure.Data`, `Microsoft.EntityFrameworkCore`

## Build Result
- **Build succeeded** with 0 errors
- 58 pre-existing warnings (none from new changes)

---

# Sprint 2 — Daily Operations Production Completion: Frontend Changes

**Task ID:** 2  
**Branch:** sprint/2-daily-operations-production-completion  
**Date:** 2026-03-05

## Summary

Implemented all 7 frontend features for Sprint 2. TypeScript compiles with 0 errors (only pre-existing globals.css warning).

## Changes Made

### 1. Top Operation Buttons in Daily Operations Header
**File:** `frontend/src/app/(dashboard)/daily-operations/page.tsx`

Added 8 compact operation buttons in the command bar between the date filters and the walk-in button area:

1. **تسجيل وصول** (CheckIn) — ClipboardCheck icon, green — calls `handleIntake(selectedItem)`
2. **مريض جديد** (New Patient) — UserPlus icon, navy — navigates to `/patients/new`
3. **دخول مباشر** (Walk-In) — LogIn icon, orange — opens walkInModal
4. **تحصيل** (Collect Payment) — CreditCard icon, emerald — opens QuickPaymentModal
5. **نداء** (Call Patient) — Bell icon, amber — calls `handleCallPatient(selectedItem)`
6. **إعادة النداء** (Recall) — BellRing icon, yellow — re-calls already called patients
7. **موعد قادم** (Next Appointment) — CalendarPlus icon, blue — opens bookAppointmentModal
8. **طباعة سند** (Print Receipt) — Printer icon, purple — downloads PDF receipt for latest payment

Each button that requires a patient selection checks `getActiveItem()` (returns `sidePanelItem ?? selectedItem ?? null`) and shows a toast if none selected. Buttons are gated by permission hooks.

Added imports: `ClipboardCheck, LogIn, BellRing, CalendarPlus` from lucide-react, and `useHasPermission, PERMISSION_KEYS` from hooks.

### 2. Payment Methods Settings Page
**File:** `frontend/src/app/(dashboard)/settings/payment-methods/page.tsx` (NEW)

Created a complete settings page following the rooms settings page pattern:
- Fetches from `GET /api/settings/payment-methods`
- Displays each method in a table row with: Name, Code, Active toggle, RequiresReferenceNumber badge, AccountName, AccountNumber
- Allows toggling active/inactive via `POST /api/settings/payment-methods/{id}/toggle`
- Allows editing via inline form with `PUT /api/settings/payment-methods/{id}`
- Allows creating new methods via `POST /api/settings/payment-methods`
- Shows "الرقم المرجعي إلزامي" badge if RequiresReferenceNumber is true
- Only accessible by Admin role (checked via `useHasPermission` with `SETTINGS_PAYMENT_METHODS_MANAGE`)

### 3. Updated usePermissions Hook
**File:** `frontend/src/hooks/usePermissions.ts`

Added 17 new permission keys to `PERMISSION_KEYS`:
- `DAILY_OPS_VIEW`, `DAILY_OPS_CHECK_IN`, `DAILY_OPS_CREATE_WALK_IN`, `DAILY_OPS_CALL_PATIENT`, `DAILY_OPS_RECALL_PATIENT`, `DAILY_OPS_ENTER_ROOM`, `DAILY_OPS_CHANGE_ROOM`, `DAILY_OPS_COLLECT_PAYMENT`, `DAILY_OPS_CREATE_DRAFT_INVOICE`, `DAILY_OPS_CLOSE_VISIT`, `DAILY_OPS_MANAGER_OVERRIDE`, `DAILY_OPS_LAB_VIEW`, `DAILY_OPS_LAB_MANAGE`, `REPORTS_DAILY_VIEW`, `COMMISSIONS_VIEW`, `SETTINGS_PAYMENT_METHODS_MANAGE`

Added `ROLE_FALLBACK` map with role-based permission inference for:
- Reception: full daily ops access (checkIn, walkIn, callPatient, recall, enterRoom, changeRoom, collectPayment, draftInvoice, closeVisit, labView)
- Accountant: dailyOps view, collectPayment, finance, reports, commissions
- Assistant: callPatient, recall, enterRoom, changeRoom, labView
- Doctor roles: dailyOps view, lab view
- BranchManager: full daily ops + manager override + lab manage + reports + commissions + payment methods

Updated `useHasPermission` hook to fall back to role-based permissions when no explicit permissions are loaded.

### 4. Permission-Based Button Visibility
**File:** `frontend/src/app/(dashboard)/daily-operations/page.tsx`

Added permission checks using `useHasPermission`:
- `canCheckIn` — gates CheckIn button
- `canCreateWalkIn` — gates Walk-In button
- `canCallPatient` — gates Call Patient button
- `canRecallPatient` — gates Recall button
- `canCollectPayment` — gates Collect Payment button AND Direct Payment button (replaced `!isDoctor` check)
- `canCreateDraftInvoice` — available for future use
- `canCloseVisit` — available for future use

### 5. Dynamic Payment Methods in Modals
**Files:** 
- `frontend/src/app/(dashboard)/daily-operations/_components/Modals.tsx`
- `frontend/src/app/(dashboard)/daily-operations/_lib/hooks.ts`

**QuickPaymentModal changes:**
- Added `usePaymentMethodSettings` hook to fetch dynamic payment methods from API
- Replaced hardcoded `PAYMENT_METHODS` dropdown with dynamic methods from `activePaymentMethods`
- Falls back to `PAYMENT_METHODS` constant if API returns empty
- Added `referenceNumber` state and `referenceError` state
- When selected method has `requiresReferenceNumber: true`, shows a required reference number input field
- Validates reference number on submit: shows error "الرقم المرجعي مطلوب لطريقة الدفع هذه" if missing
- Updated `onConfirm` signature to accept `referenceNumber?: string`

**DirectPaymentModal changes:**
- Same changes as QuickPaymentModal
- Updated `onConfirm` data type to include `referenceNumber?: string`

**useCreatePayment hook:**
- Added `referenceNumber?: string` to the mutation body type

**page.tsx handlers:**
- Updated `handlePaymentConfirm` to accept and pass `referenceNumber`
- Updated `handleDirectPaymentConfirm` to accept and pass `referenceNumber`

### 6. Enhanced Receipt Download
**File:** `frontend/src/app/(dashboard)/daily-operations/page.tsx`

Added `handlePrintReceipt` callback that:
- Gets the active item via `getActiveItem()`
- Shows toast "يرجى اختيار مريض أولاً" if no patient selected
- Checks for `latestPayment?.id` in selectedSummary
- Shows toast "لا توجد دفعة حديثة لطباعة سند" if no payment found
- Downloads PDF receipt from `/api/payments/{paymentId}/pdf`

Added "طباعة سند" button in the side panel's Financial Summary section:
- Shows only when `finance.latestPayment?.id` exists
- Downloads PDF receipt directly with proper filename

### 7. Payment Methods Link in Settings Page
**File:** `frontend/src/app/(dashboard)/settings/page.tsx`

Added `CreditCard` icon import and a new Link card for payment methods:
- Links to `/settings/payment-methods`
- Purple theme with CreditCard icon
- Label: "طرق الدفع"
- Description: "إدارة طرق الدفع المتاحة والرقم المرجعي"

## Build Result
- **TypeScript check passed** with 0 relevant errors
- Only pre-existing `globals.css` module declaration warning
