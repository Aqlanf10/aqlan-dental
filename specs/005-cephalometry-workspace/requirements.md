# 005 Cephalometry Workspace Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/ceph/`, `frontend/src/components/ceph/`, `frontend/src/lib/cephMath.ts`, `cephTracing.ts`, `cephReadiness.ts`, `CephController.cs`, `CephNormsController.cs`, `PhotoAnalysisController.cs`, `CephService`, `CephAiDraftService`, `CephAiLandmarkDraftService`, ceph DTOs/tests.

- `CEPH-REQ-001`: `/ceph` SHALL be the canonical cephalometry workspace.
- `CEPH-REQ-002`: AI-generated ceph diagnosis or landmarks SHALL be draft-only until doctor review.
- `CEPH-REQ-003`: Ceph norms/settings SHALL use existing `CephNormsController` and settings where applicable.
- `CEPH-REQ-004`: Reports SHALL use Arabic PDF identity rules.
- `CEPH-REQ-005`: No fake AI provider, fake measurement, or fake clinical claim is allowed.
- `CEPH-REQ-006`: Measurement-table export SHALL use the saved measurement snapshot only, SHALL be blocked while edits are unsaved or the analysis is not doctor-approved, and SHALL preserve Arabic text safely in spreadsheet software without executable formula cells.
- `CEPH-REQ-007`: The cephalometry list SHALL distinguish saved measurements from doctor approval and SHALL NOT describe an unapproved analysis as final-report ready.
- `CEPH-REQ-008`: WebCeph parity work SHALL follow the binding functional matrix in `docs/audits/WEBCEPH_CEPH_PARITY.md`; parity means equivalent clinical workflow in Aqlan's design, not copied proprietary UI or undocumented algorithms.
- `CEPH-REQ-009`: Viewer tools SHALL support non-destructive distance/angle inspection and preview transforms without silently changing source-pixel landmark coordinates.
- `CEPH-REQ-010`: Structured ceph assessment SHALL remain doctor-reviewed and SHALL require explicit selection before adding any item to the orthodontic problem list.
- `CEPH-REQ-011`: Treatment/VTO scenarios SHALL persist only doctor-authored movements and SHALL NOT claim predicted biological or soft-tissue response.
- `CEPH-REQ-012`: Multi-record superimposition SHALL preserve a named reference, record/date identity, distinct colors, opacity, and auditable export context.
- `CEPH-REQ-013`: PA cephalometry SHALL use documented frontal-radiograph landmarks and deterministic symmetry/transverse measurements with saved calibration and doctor approval.
- `CEPH-REQ-014`: Occlusogram SHALL reuse the canonical orthodontic model-analysis engine rather than duplicate arch calculations.
- `CEPH-REQ-015`: Timelapse/case/cohort workflows SHALL use real ordered records, approved clinical labels, patient-access filtering, and aggregate privacy safeguards; no fabricated interpolation or diagnosis tags.
- `CEPH-REQ-016`: Final parity SHALL require authenticated runtime verification, responsive/accessibility QA, exports, patient access, CI, deployment, and a closed parity matrix.

## Target State

WebCeph-inspired, doctor-reviewed, auditable ceph workspace.

## Risks

Clinical harm, unreviewed diagnosis, fake AI, PDF identity drift.

## Allowed Future Work

Improve tracing, quality/readiness, VTO, comparison, photo analysis, report UX, tests.

## Forbidden Future Work

Automatic diagnosis acceptance, hidden AI assumptions, unaudited provider changes, new ceph module.

## Acceptance Criteria

- WHEN AI drafts are created THEN the UI/API SHALL mark them as drafts pending doctor review.
- WHEN a report is generated THEN it SHALL use existing PDF identity rules.
- WHEN a doctor exports the measurement table THEN the CSV SHALL contain the saved values, norms, deviations, analysis groups, and interpretations with UTF-8 Arabic support.
- WHEN the analysis is unapproved, has unsaved edits, or has no saved measurements THEN measurement-table export SHALL remain unavailable.
- WHEN list data is loaded THEN each analysis SHALL show the first unfinished workflow stage in this order: landmarks, saved measurements, doctor approval.
- WHEN measurements are saved but approval is pending THEN the list SHALL prompt review and approval rather than claim completion.
- WHEN a parity feature is implemented THEN its matrix row, requirement, tests, and deployment evidence SHALL be updated together.
- WHEN a ceph clinical tag is shown THEN its value SHALL be allowlisted and both its source analysis and structured diagnosis SHALL be doctor-approved.
- WHEN cohort analytics are requested THEN only the latest approved record per accessible patient SHALL be counted, no patient identifier SHALL be returned, and the complete result plus each measurement SHALL be suppressed below five independent patients.
- WHEN Timelapse runs THEN every frame SHALL correspond to a persisted dated image or analysis; no interpolated treatment frame SHALL be generated.
- Needs runtime verification for doctor review flow.
