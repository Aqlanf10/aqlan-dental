## Ceph UI ↔ Backend DTO Map (CEPH-TASK-002, updated 2026-07-11)

All rows verified against `CephController.cs`, `CephNormsController.cs`, `PhotoAnalysisController.cs`, `CephDto.cs`, `CephAiDraftResultDto.cs`, `types/ceph.ts`, `orthoService.ts`, and the `ceph/` pages. camelCase↔PascalCase is the default JSON casing and is not counted as drift.

### 1. Endpoint → DTO → consumer

| Endpoint (method + path) | Backend DTO (C#) | Frontend type (ts) | Consumed by |
|---|---|---|---|
| GET `/api/ceph` | `CephAnalysisListDto[]` | `CephAnalysisList[]` | `ceph/page.tsx` (list) |
| GET `/api/ceph?orthoCaseId=` | `CephAnalysisListDto[]` | `CephAnalysisList[]` | `orthoService.getCephAnalyses` (ortho case panels) |
| GET `/api/ceph/{id}` | `CephAnalysisDetailDto` | `CephAnalysis` | `ceph/[id]`, `[id]/quality`, `vto`, `compare` pages |
| POST `/api/ceph` | req `CreateCephAnalysisRequest` → `CephAnalysisDetailDto` | inline `{ id: string }` (only reads id) | `ceph/new/page.tsx` |
| POST `/api/ceph/{id}/landmarks` | req `SaveLandmarksRequest` → `CephAnalysisDetailDto` | req inline → `CephAnalysis` | `ceph/[id]/page.tsx` |
| GET `/api/ceph/compare?baseId=&targetId=` | `CephCompareResultDto` | `CephCompareResult` | `ceph/compare/page.tsx` |
| POST `/api/ceph/{id}/versions` | req `CreateCephVersionRequest` → `CephVersionListDto` | req inline `{ label }` | `ceph/[id]/page.tsx` |
| GET `/api/ceph/{id}/versions` | `CephVersionListDto[]` | `CephVersionListItem[]` | `ceph/[id]/page.tsx` |
| GET `/api/ceph/{id}/versions/{versionId}` | `CephVersionDetailDto` | `CephVersionDetail` | `ceph/[id]`, `compare` pages |
| POST `/api/ceph/{id}/simulate` | req `AiSimulateRequest` → `CephSimulationResultDto` | inline `{ isSimulation, simulationNotice, landmarks }` | `ceph/[id]/page.tsx` |
| POST `/api/ceph/{id}/ai/auto-trace` | req `CephAiTraceRequest` → `CephAiTraceResultDto` | inline `{ landmarks, modelId, disclaimer, generatedAt }` | `ceph/[id]/page.tsx` |
| POST `/api/ceph/{id}/ai/refine-landmark` | req `CephAiRefineLandmarkRequest` → `CephAiRefineResultDto` | inline `{ landmark, modelId, disclaimer, generatedAt }` | `ceph/[id]/page.tsx`, `CephCanvas.tsx` |
| POST `/api/ceph/{id}/ai/draft-diagnosis` | **no request body**; anon `{ draft, modelId, disclaimer, generatedAt }` | `CephAiDraftResponse` | `components/ceph/AnalysisReport.tsx` |
| POST `/api/ceph/{id}/approve` | req `ApproveCephAnalysisRequest?` → anon `{ message, analysis: CephAnalysisDetailDto }` | untyped (`api.post` result ignored) | `ceph/[id]/page.tsx` |
| GET `/api/ceph/{id}/report/pdf` | PDF bytes (`File`) | binary download | `ceph/[id]/page.tsx`, `CasePresentationPanel.tsx` |
| PUT `/api/ceph/{id}/diagnosis` | req `SaveDiagnosisRequest` → anon `{ message }` | req = `CephDiagnosis` partial | `ceph/[id]/page.tsx` |
| GET `/api/ceph-norms` | `CephNormDto[]` | `ApiNorm[]` | `ceph/[id]/page.tsx` |
| GET `/api/photo-analysis?orthoCaseId=` | `PhotoAnalysisListItemDto[]` | `PhotoAnalysisListItem[]` / `SavedPhotoAnalysis[]` | `FacialPhotoPanel.tsx`, `usePhotoAnalysisCase.ts` |
| GET `/api/photo-analysis/{id}` | `PhotoAnalysisDetailDto` | inline (shell) | `PhotoAnalysisShell.tsx` |
| POST `/api/photo-analysis` | req `SavePhotoAnalysisRequest` → `PhotoAnalysisDetailDto` | inline `{ id }` | `usePhotoAnalysisCase.ts` |
| GET `/api/photo-analysis/{id}/report/pdf` | PDF bytes | binary download | `FacialPhotoPanel.tsx`, `PhotoAnalysisShell.tsx` |

### 2. Drift status

Field-by-field checked (`CephAnalysisListDto`, `CephAnalysisDetailDto`, `CephLandmarkDto`, `CephMeasurementDto`, AI draft/trace/refine result DTOs); rest spot-checked.

- **SEQ-14 / PR #650 — fixed:** `IsAutoTraced`, `DoctorId`, and `Notes` are now represented in frontend `CephAnalysis`. The detail view surfaces the clinical user note and labels auto-traced analyses as requiring doctor review.
- **Notes storage caveat — handled:** after landmarks/calibration are saved, the backend stores calibration metadata and `UserNotes` together as JSON in the `Notes` column. The frontend now extracts only `UserNotes` and never renders raw calibration JSON.
- **`CephAnalysisListDto.Notes`:** still absent from `CephAnalysisList` intentionally; the list page does not render notes, so carrying this payload into list state provides no UI value.
- **`CephLandmark` client-only fields:** `nameAr` and `group` remain derived from `LANDMARK_DEFS`; intentional. Nullable backend `Name` is backfilled by the UI.
- **`CephMeasurement.apiInterpretation`:** client-only overlay; expected.
- **`CephDiagnosis.aiRecommendation`:** read-only response field; intentionally absent from `SaveDiagnosisRequest`.
- AI trace/refine/simulate result shapes match their frontend inline types.
- `CephCompareRowDto` / `CephCompareRow` match apart from expected C# decimal→TS number and frontend string-union narrowing.

### 3. Endpoints with no frontend consumer in the scanned scope

- `DELETE /api/ceph/{id}`
- `DELETE /api/photo-analysis/{id}`
- Ceph norms write/lookup endpoints other than `GET /api/ceph-norms`

These remain documented observations, not asserted defects. No frontend calls to non-existent ceph/photo-analysis endpoints were found.
