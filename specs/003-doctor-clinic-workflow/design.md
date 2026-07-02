# 003 Doctor Clinic Design

- Frontend owner: `frontend/src/app/(dashboard)/doctor-clinic/`.
- Related frontend: patient tabs in `frontend/src/components/patient/tabs/`.
- Backend owners: `PatientJourneyController.cs`, `VisitsController.cs`, `TreatmentPlanController.cs`, doctor schedule APIs.
- Entities: `Doctor`, `DoctorSchedule`, `Visit`, `Patient`, treatment entities.
- Permissions: `DoctorAccess`, `StaffOnly`, `PatientAccessFilter`, route roles in `routePermissions.ts`.

Allowed files: doctor-clinic folder, patient journey hooks/components, related tests.

Forbidden files: patient access weakening, new patient entity, finance logic without finance spec.

Rollback: revert workflow UI/API changes and keep patient access unchanged.
