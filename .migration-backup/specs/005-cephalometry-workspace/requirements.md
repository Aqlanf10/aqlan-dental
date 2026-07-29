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
- `CEPH-REQ-017`: Every AI-generated or externally imported landmark SHALL retain its placement source and source identifier; AI proposals SHALL retain model and original coordinates. Such points SHALL remain unreviewed until explicit doctor review and SHALL block analysis approval and final PDF export while unreviewed.
- `CEPH-REQ-018`: Account-owned WebCeph Landmark Table import SHALL parse the exported table structurally, convert millimetre coordinates relative to S using saved calibration, reject invalid/out-of-bounds or PA imports, preserve unmapped rows under stable keys, revoke prior approval, and never place patient exports in source control or logs.
- `CEPH-REQ-019`: AI landmark accuracy SHALL be reported by model from saved doctor corrections in millimetres (mean, median, P95, within 1 mm, and within 2 mm). A model version's sample SHALL NOT be labelled sufficient below 30 reviewed points from three independent analyses, samples from different model versions SHALL NOT be pooled to cross that threshold, and the product SHALL NOT claim WebCeph-equivalent accuracy without an orthodontist-labelled reference benchmark.
- `CEPH-REQ-020`: WebCeph account synchronization SHALL use only the official partner API with a server-held secret after the required agreement and plan are confirmed. Browser sessions, passwords, and undocumented endpoints SHALL NOT be used. Patients, records, and images may use the official API contract; landmark points, analysis results, and clinical/diagnostic data SHALL use explicit account exports because the public API excludes them.
- `CEPH-REQ-021`: Clinical landmark-accuracy claims SHALL use the preregistered, reproducible protocol in `docs/ceph-ai/CEPH_AI_VALIDATION_PROTOCOL.md`, an orthodontist-adjudicated gold standard, patient-level splits, a locked independent test set, per-landmark and derived-measurement metrics, repeatability, subgroup results, and confidence intervals. Build success, unit tests, correction telemetry, visual similarity, or one case SHALL NOT be presented as accuracy evidence.
- `CEPH-REQ-022`: Cephalometric images and doctor corrections SHALL NOT become training data automatically. Dataset intake, de-identification, consent/legal basis, licensing, patient-level partitioning, annotation, adjudication, versioning, retention, withdrawal, and release SHALL follow `docs/ceph-ai/CEPH_AI_DATA_GOVERNANCE.md`.
- `CEPH-REQ-023`: Every future specialized inference result SHALL be reproducible from immutable model, artifact, dataset, preprocessing, landmark-definition, inference, calibration, and image-integrity versions; original predictions and doctor corrections SHALL remain separately auditable, and historic analyses SHALL remain pinned to their originating versions.
- `CEPH-REQ-024`: WebCeph SHALL remain a functional and, when lawfully exportable, paired accuracy comparator rather than a gold standard or default training-label source. Aqlan's native tracing workflow SHALL be the primary product path; WebCeph import/sync SHALL remain secondary migration/interoperability functionality.
- `CEPH-REQ-025`: Lateral landmark semantics SHALL be pinned to a ratified version of `docs/ceph-ai/CEPH_AI_LANDMARK_DEFINITIONS.md`, including core/optional status and bilateral/double-contour rules. A definition change SHALL create a new version and SHALL NOT silently reinterpret historic analyses or benchmark labels.
- `CEPH-REQ-026`: Model promotion SHALL proceed through offline validation, shadow evaluation, controlled canary, clinical sign-off, monitoring, and tested rollback. Initial numerical targets in the validation protocol are release gates to be measured, not statements of current performance.
- `CEPH-REQ-027`: Gold-standard benchmark manifests SHALL be de-identified, reject undeclared properties, use salted patient-group hashes for patient-cluster split isolation, require two independent reviews for all 24 core landmarks, and require an explicit third-reviewer or consensus-panel decision for disagreement above 1.5 mm or any visibility/double-contour conflict. Validation SHALL be Admin-only and stateless, SHALL NOT accept image locations or direct clinical identifiers, and SHALL NOT auto-average reviewer coordinates.
- `CEPH-REQ-028`: Offline landmark evaluation SHALL pin protocol, dataset, model, preprocessing, landmark-definition, and one non-training evaluation split; validation/internal/external splits SHALL NOT be pooled. It SHALL evaluate all 24 core landmarks; count omitted visible points as failures; and report observed-only plus failure-penalized SDR at 1.0/1.5/2.0/2.5/3.0/4.0 mm. It SHALL report radial-error distribution, completeness, not-visible prediction errors, per-image/per-landmark/prespecified subgroup results, and deterministic 95% intervals bootstrapped by patient cluster without returning patient linkage hashes. The Admin-only endpoint SHALL be stateless and reject loose JSON contracts.
- `CEPH-REQ-029`: Derived-measurement, repeatability, confidence, and paired-comparator evaluation SHALL use one frozen versioned geometry engine shared with the clinical analysis path. It SHALL report prespecified measurement bias/absolute error/Bland-Altman/category disagreement, repeated-run displacement/SD/ICC/missing consistency, paired deltas, and confidence/error-coverage behavior before any non-inferiority or production claim.

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
- WHEN longitudinal soft-tissue comparison runs THEN both records SHALL belong to the same case and view type, the before date SHALL precede the after date, and deltas SHALL use saved finite values only.
- WHEN final parity is closed THEN a deployed authenticated test SHALL cover the canonical list, cohort privacy surface, case review, Timelapse, responsive layout, and any eligible PDF/CSV downloads without embedding credentials or patient fixtures in source.
- WHEN an accuracy result is reported THEN the report SHALL identify the exact protocol, dataset/split, model, preprocessing, landmark-definition, sample count, failures, confidence interval, and clinical sign-off status.
- WHEN a patient correction is saved THEN it SHALL remain clinical data only unless a separately approved de-identification, annotation, and dataset-release workflow includes it in an immutable dataset version.
- WHEN WebCeph and Aqlan are compared THEN both SHALL use the same eligible locked images and adjudicated gold standard, or the result SHALL be labelled functional/observational rather than an accuracy comparison.
- WHEN a model or landmark definition changes THEN old analyses SHALL retain their original lineage and SHALL NOT be recomputed or relabelled silently.
- WHEN a benchmark manifest is validated THEN every case SHALL pass the de-identification evidence gate, every patient group SHALL remain in one dataset split, every core point SHALL have two independent reviews and explicit gold truth, and unresolved disagreements SHALL block dataset release.
- WHEN landmark predictions are evaluated THEN every omitted visible core point SHALL remain in the primary denominator as a failure, exactly one non-training split SHALL define the report, and every bootstrap draw SHALL resample complete patient clusters rather than individual images or points.
