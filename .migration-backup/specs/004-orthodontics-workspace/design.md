# 004 Orthodontics Design

- Frontend owner: `frontend/src/app/(dashboard)/ortho/`.
- Shared components: `frontend/src/components/ortho/`.
- Backend owners: `OrthoCasesController.cs`, `OrthoCaseAiController.cs`, `OrthoModelAnalysesController.cs`.
- Services: `OrthoService`, `OrthoCaseQueryService`, `OrthoCaseDraftService`.
- Entities: `OrthoCase`, `OrthoDiagnosis`, `OrthoClinicalExam`, `TreatmentPlan`, `OrthoVisit`, `TreatmentStage`, `RetentionRecord`, `ModelAnalysis`, `OrthodonticAiLog`.
- Permissions: `OrthoAccess`, selected `OrthoSurgicalAccess`.
- Tests: `backend/tests/AqlanDentalPro.UnitTests/Ortho/`.

Allowed files: existing ortho pages/components/services/tests/specs.

Forbidden files: new ortho root, migrations without approved data model spec, clinical claims without doctor review language.
