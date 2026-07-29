# 002 Daily Operations Design

- Frontend owner: `frontend/src/app/(dashboard)/daily-operations/`.
- Shared journey components: `frontend/src/components/shared/journey/`.
- Backend owners: `DailyOperationsController.cs`, `PatientJourneyController.cs`, `ClinicQueueController.cs`.
- Services: `PatientJourneyService`, `CheckoutService`.
- Entities: `Appointment`, `BookingRequest`, `ClinicQueueItem`, `Visit`, `Invoice`, `Payment`, `LabOrder`.
- Permissions: daily permission keys in `usePermissions.ts`, backend `StaffOnly`, `AdminOrReception`, `DoctorAccess`, `FinanceAccess`.

Allowed files: daily operations folder, journey components, relevant controllers/services/tests.

Forbidden files: migrations unless approved, standalone duplicate queue/dashboard screens, FinanceV3 calculations unless finance spec is updated.

Rollback: revert module UI/API changes together and keep specs aligned.
