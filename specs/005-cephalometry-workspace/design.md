# 005 Cephalometry Design

- Frontend owners: `frontend/src/app/(dashboard)/ceph/`, `frontend/src/components/ceph/`, `frontend/src/lib/ceph*`.
- Backend owners: `CephController.cs`, `CephNormsController.cs`, `PhotoAnalysisController.cs`.
- Services: `CephService`, `PhotoAnalysisService`, `CephAiDraftService`, `CephAiLandmarkDraftService`.
- DTOs: `backend/src/AqlanDentalPro.Application/DTOs/Ceph/`.
- Entities: `CephAnalysis`, `CephLandmark`, `CephMeasurement`, `CephDiagnosis`, `CephNorm`, `CephAnalysisVersion`, `PhotoAnalysis`.
- Permissions: `OrthoAccess`, `AdminOnly` for AI/norm settings, `StaffOnly` where used.
- Tests: `backend/tests/AqlanDentalPro.UnitTests/Ceph/`.

Allowed files: existing ceph owners and ceph tests.

Forbidden files: fake providers, automatic clinical acceptance, migration edits without approved spec.
