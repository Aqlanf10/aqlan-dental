# 005 Cephalometry Design

- Frontend owners: `frontend/src/app/(dashboard)/ceph/`, `frontend/src/components/ceph/`, `frontend/src/lib/ceph*`.
- Backend owners: `CephController.cs`, `CephNormsController.cs`, `PhotoAnalysisController.cs`.
- Services: `CephService`, `PhotoAnalysisService`, `CephAiDraftService`, `CephAiLandmarkDraftService`.
- DTOs: `backend/src/AqlanDentalPro.Application/DTOs/Ceph/`.
- Entities: `CephAnalysis`, `CephLandmark`, `CephMeasurement`, `CephDiagnosis`, `CephNorm`, `CephAnalysisVersion`, `PhotoAnalysis`.
- Permissions: `OrthoAccess`, `AdminOnly` for AI/norm settings, `StaffOnly` where used.
- Tests: `backend/tests/AqlanDentalPro.UnitTests/Ceph/`.
- Measurement-table export: pure CSV serialization and browser download ownership live in `frontend/src/lib/cephMeasurementCsv.ts`; the existing `/ceph/[id]` toolbar supplies the saved `CephAnalysis.measurements` snapshot and applies the same approval/clean-state gate as final PDF export. No export API or persisted entity is added.
- CSV safety: UTF-8 BOM supports Arabic in Excel, every field is quoted and escaped, and text beginning with spreadsheet formula markers is prefixed as literal text. Numeric measurement values remain numeric cells.
- Frontend tests: `frontend/src/__tests__/lib/cephMeasurementCsv.test.ts` and `frontend/src/__tests__/components/ceph/CephMeasurementExportButton.test.tsx` cover serialization, download, and the toolbar gate.

Allowed files: existing ceph owners, `frontend/src/lib/cephMeasurementCsv.ts`, frontend ceph tests, and ceph specification/governance files.

Forbidden files: fake providers, automatic clinical acceptance, migration edits without approved spec.
