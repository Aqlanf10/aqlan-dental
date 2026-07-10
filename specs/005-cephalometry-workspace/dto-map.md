## Ceph UI ↔ Backend DTO Map (CEPH-TASK-002, 2026-07-10)

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
| POST `/api/ceph/{id}/ai/draft-diagnosis` | **no request body**; anon `{ draft, modelId, disclaimer, generatedAt }` (from `CephAiDraftResultDto`) | `CephAiDraftResponse` | `components/ceph/AnalysisReport.tsx` |
| POST `/api/ceph/{id}/approve` | req `ApproveCephAnalysisRequest?` → anon `{ message, analysis: CephAnalysisDetailDto }` | untyped (`api.post` result ignored) | `ceph/[id]/page.tsx` |
| GET `/api/ceph/{id}/report/pdf` | PDF bytes (`File`) | binary download | `ceph/[id]/page.tsx`, `CasePresentationPanel.tsx` |
| PUT `/api/ceph/{id}/diagnosis` | req `SaveDiagnosisRequest` → anon `{ message }` | req = `CephDiagnosis` partial | `ceph/[id]/page.tsx` |
| GET `/api/ceph-norms` | `CephNormDto[]` | `ApiNorm[]` | `ceph/[id]/page.tsx` |
| GET `/api/photo-analysis?orthoCaseId=` | `PhotoAnalysisListDto[]` (see `PhotoAnalysisDto.cs`) | `PhotoAnalysisListItem[]` / `SavedPhotoAnalysis[]` | `FacialPhotoPanel.tsx`, `usePhotoAnalysisCase.ts` |
| GET `/api/photo-analysis/{id}` | `PhotoAnalysisDto` | inline (shell) | `PhotoAnalysisShell.tsx` |
| POST `/api/photo-analysis` | req `SavePhotoAnalysisRequest` → `PhotoAnalysisDto` | inline `{ id }` | `usePhotoAnalysisCase.ts` |
| GET `/api/photo-analysis/{id}/report/pdf` | PDF bytes | binary download | `FacialPhotoPanel.tsx`, `PhotoAnalysisShell.tsx` |

### 2. Mismatches / drift found

Field-by-field checked (`CephAnalysisListDto`, `CephAnalysisDetailDto`, `CephLandmarkDto`, `CephMeasurementDto`, AI draft/trace/refine result DTOs); rest spot-checked.

- **`CephAnalysisListDto.Notes`** — present in C# list DTO, **absent** from ts `CephAnalysisList`. The list page never renders notes, so this is dead payload rather than a bug.
- **`CephAnalysisDetailDto` → `CephAnalysis` drops three fields**: `IsAutoTraced`, `DoctorId`, and `Notes` exist in the C# DTO but are **missing** from the ts `CephAnalysis` interface. `Notes` in particular is round-tripped on create (`CreateCephAnalysisRequest.Notes`) but the detail type cannot surface it — likely real drift worth flagging to the UI team.
- **`CephLandmark` (ts) adds `nameAr` and `group`** not present in `CephLandmarkDto`. These are client-derived from `LANDMARK_DEFS` (see auto-trace/refine handlers), not backend fields — intentional, not a bug. Also `CephLandmarkDto.Name` is nullable while ts `name` is required (the UI backfills it), so a null `Name` from the API relies on that fallback.
- **`CephMeasurement` (ts) adds `apiInterpretation`**; backend `CephMeasurementDto` has `InterpretationAr` (mapped to ts `interpretationAr`) but no `apiInterpretation`. Client-only overlay field — expected.
- **`CephDiagnosis.aiRecommendation`** is read-only: it exists on `CephDiagnosisDto` (response) and ts `CephDiagnosis`, but is intentionally absent from the write DTO `SaveDiagnosisRequest`. Not drift, but note the PUT silently ignores it if sent.
- **AI trace / refine / simulate result DTOs match field-for-field** with their inline ts types (`landmarks/landmark, modelId, disclaimer, generatedAt`; `isSimulation, simulationNotice, landmarks`). `CephAiDraftResultDto`/anon draft response matches `CephAiDraftResponse` exactly.
- **`CephCompareRowDto` / `CephCompareRow`** match, but backend uses `decimal?` for numeric columns vs ts `number`; ts narrows `analysisGroup`/classifications to string-literal unions where C# uses plain `string?` — type-narrowing only, no missing fields.

**Endpoints with no frontend consumer (in scanned scope):**

- `DELETE /api/ceph/{id}` — implemented, no caller found anywhere in the frontend.
- `DELETE /api/photo-analysis/{id}` — implemented, no caller found in the ceph/photo components scanned.
- `CephNormsController` write/lookup endpoints — `GET /api/ceph-norms/best`, `POST /api/ceph-norms`, `GET /api/ceph-norms/{id}`, `PUT /api/ceph-norms/{id}`, `POST /api/ceph-norms/reset-defaults` — none are called from the ceph pages; only `GET /api/ceph-norms` (list) is consumed. These are presumably driven by an admin/settings screen outside this module (not verified here — flagged as unclear rather than asserted).

**Frontend calls to non-existent endpoints:** none found. Every `/api/ceph*` and `/api/photo-analysis*` call resolves to a real controller action. (`ceph/new` also calls `DELETE /api/uploads/{fileName}`, which belongs to a separate uploads controller, out of scope for this map.)
