# 009 Reports Printing Design

- Backend owners: `ReportsController.cs`, `PdfService.cs`, PDF generator classes in `backend/src/AqlanDentalPro.API/Services/`.
- Identity helper: `FinanceClinicIdentity.cs`.
- Frontend report owners: `frontend/src/app/(dashboard)/reports/`, print pages under patient routes, `frontend/src/lib/pdfDownload.ts`, `frontend/src/lib/printUtils.ts`.
- Entities: reporting depends on module entities.
- Permissions: `ReportsAccess`, finance/lab/ortho/ceph module policies.
- Tests: finance PDF tests, ceph PDF tests, ortho PDF tests, lab order PDF tests, frontend `pdfDownload` test.

Allowed files: report/PDF owners, settings helpers, report tests.

Forbidden files: hardcoded identity, auth weakening, migrations unless report requires approved schema.
