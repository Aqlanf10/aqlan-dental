# WebCeph Functional Parity Baseline

Date: 2026-07-13

## Scope And Method

This baseline compares the WebCeph patient/record workflow and the 89-page
WebCeph user manual supplied by the owner against the current Aqlan Dental Pro
repository. The target is functional and clinical-workflow parity implemented
with Aqlan's own Arabic RTL interface, data model, permissions, audit rules, and
doctor-approval gates. WebCeph branding, artwork, text, proprietary layouts, and
undocumented algorithms are not copied.

Evidence inspected:

- Live WebCeph patient list and record workspace in Microsoft Edge, read-only.
- `[WEBCEPH] User manual.pdf`, chapters covering records, digitization,
  analysis, PA, soft tissue, occlusogram, assessment, treatment,
  superimposition, viewer, case, and timelapse.
- `docs/ortho-module/CEPH-EPIC.md` and `specs/005-cephalometry-workspace/`.
- Existing `/ceph`, `/ortho`, photo-analysis, model-analysis, problem-list,
  case-presentation, PDF, comparison, and VTO owners.

## Functional Matrix

| WebCeph area | Aqlan owner/evidence | State | Remaining parity work |
|---|---|---:|---|
| Patient/record progress | `/ceph`, `cephWorkflow.ts`, ortho records checklist | Complete | Keep approval-aware workflow wording under regression tests. |
| Lateral digitization | `CephCanvas.tsx`, `CephService.cs`, 27 landmarks, calibration | Complete | Preserve manual editing and AI-draft review gates. |
| Image processing | brightness/contrast/invert; zoom/pan/fit; transient distance/angle rulers and reversible rotate/flip preview | Complete | Crop remains intentionally unavailable rather than silently invalidating source-pixel landmarks. |
| Analysis methods | Steiner, Tweed, McNamara, Ricketts, Downs, Jarabak, Wits | Complete | Add method visibility presets only if they reuse existing measurements/norms. |
| Norm customization | `CephNormsController`, DB overrides | Complete | Admin runtime QA remains. |
| Measurement/report export | Arabic PDF, print, UTF-8 CSV | Complete | Preserve approval and saved-snapshot gates. |
| Clinical assessment | structured ceph assessment + approval-gated explicit handoff to canonical ortho problem list | Complete | Preserve doctor selection, approval gates, duplicate suppression, and patient access controls. |
| Lateral soft tissue | profile photo analysis + S/E lines; `/ceph/photo/compare?viewType=profile` saved-record image/measurement comparison | Complete | Preserve the named soft-tissue definitions, real saved dates, and no-growth-prediction statement. |
| Frontal soft tissue | `/ceph/photo/frontal` ratios/asymmetry; `/ceph/photo/compare?viewType=frontal` saved-record image/measurement comparison | Complete | Preserve scale-independent ratios, patient-side conventions, chronological record order, and no interpolation. |
| PA cephalometric x-ray | `analysisType=pa` in the existing ceph owner; 15-point `CephPaCanvas`; calibrated transverse/asymmetry/cant engine mirrored in backend/frontend; snapshots, approval, PDF/CSV | Complete | Preserve ZR→ZL patient-left sign convention, descriptive treatment of unconfigured age/sex norms, and the prohibition on deriving a lateral skeletal class from PA-only data. |
| Occlusogram | canonical `/ortho/[id]/model-analysis` workflow; `OrthoModelAnalysesController`; `DentalModelAnalysisCalculator` | Complete | Preserve the three-mode tooth-size, arch-width/length, and irregularity presentation; case-photo reuse, version navigation, approval, and PDF must continue using the canonical model-analysis owner. |
| Treatment simulation | `/ceph/vto` doctor-authored movements plus persisted named scenarios, immutable versions, before/after snapshots, notes, and Arabic PDF inclusion | Complete | Preserve approval/access gates, saved calibration/landmark inputs, version history, and the explicit no biological or soft-tissue response prediction statement. |
| Superimposition | same-case analysis/version selection, multi-layer SN registration, explicit reference, stable colors, opacity controls, visible legend, and metadata-bearing SVG export | Complete | Preserve same-case and patient-access guards, required registration landmarks, record/date identity, and exported reference context. |
| Case review | `/ceph/case/[caseId]` reuses `CasePresentationPanel`, report/photo selections, readiness, PDF/PPTX, superimposition, Occlusogram, and the canonical ortho case | Complete | Preserve the ortho case as the sole data owner; selected/prepared report photos remain the saved composition metadata. |
| Timelapse | `/ceph/timelapse/[caseId]`, canonical ceph analyses and ortho clinical photos, `buildCephTimelapseFrames` | Complete | Preserve real date ordering, explicit record selection, and the prohibition on fabricated/interpolated frames. |
| Case tags/search | approved `CephDiagnosis` fields projected through `CephClinicalTagCatalog`; `/ceph` approved-tag filter | Complete | Unknown/free-text diagnosis values must never become tags, and both analysis and diagnosis approval remain required. |
| Cohort analysis | `GET /api/ceph/cohort`, `/ceph/cohort`, latest approved record per accessible patient | Complete | Preserve the five-patient threshold for the whole cohort and each measurement, aggregate-only DTO, allowlisted filters, and patient-access scope. |

## Delivery Sequence

1. `SEQ-43`: baseline and binding acceptance map.
2. `SEQ-44`: non-destructive viewer tools.
3. `SEQ-45`: structured doctor-reviewed assessment and problem-list handoff.
4. `SEQ-46`: persisted treatment/VTO scenarios.
5. `SEQ-47`: multi-record superimposition.
6. `SEQ-48`: PA cephalometric analysis.
7. `SEQ-49`: canonical occlusogram/model-analysis workflow.
8. `SEQ-50`: timelapse plus case/cohort workflows.
9. `SEQ-51`: final parity audit, authenticated runtime QA, accessibility,
   responsive checks, PDF/export checks, and closure report.

## Cross-Cutting Acceptance Rules

- AI output remains a draft until explicit orthodontist review.
- A saved measurement is not the same as doctor approval.
- Reports and exports use saved snapshots and configured clinic identity.
- Existing patient-access filters remain authoritative on every list/detail/export.
- No new route/controller/entity is introduced when an existing owner can be extended.
- Clinical calculations require named definitions, deterministic tests, and backend/frontend parity where both compute them.
- Image transformations must not move landmarks relative to source pixels unless a deliberate migration and audit trail exist.
- Runtime-only claims remain marked as needing runtime verification until tested in an authenticated deployed session.

## Completion Definition

Parity is complete only when every matrix row is either `Complete` or explicitly
accepted by the owner as out of scope, all linked requirements and tests pass,
no open review/CI issue remains, production deployment is healthy, and the final
authenticated workflow QA is recorded under `SEQ-51`.

The implementation matrix has no remaining `Partial` row. Final runtime,
accessibility, export, CI, and deployment evidence is recorded in
`docs/audits/WEBCEPH_CEPH_PARITY_CLOSURE.md`.
