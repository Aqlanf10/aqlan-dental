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
| Lateral soft tissue | profile photo analysis + S/E lines | Partial | Unify navigation and longitudinal comparison. |
| Frontal soft tissue | `/ceph/photo/frontal` ratios/asymmetry | Partial | Unify navigation and longitudinal comparison. |
| PA cephalometric x-ray | no PA landmark/measurement owner | Missing | Add frontal radiograph workspace, calibration, landmarks, symmetry/transverse measurements, saved snapshots, report, and tests. |
| Occlusogram | rich `/ortho/[id]/model-analysis` engine | Partial | Present arch-width/length/crowding analysis as the canonical occlusogram workflow; avoid a duplicate calculator. |
| Treatment simulation | `/ceph/vto` doctor-authored movements plus persisted named scenarios, immutable versions, before/after snapshots, notes, and Arabic PDF inclusion | Complete | Preserve approval/access gates, saved calibration/landmark inputs, version history, and the explicit no biological or soft-tissue response prediction statement. |
| Superimposition | analysis/version comparison and SN similarity transform | Partial | Add multi-record selection, color legend, stable reference choice, opacity controls, and export. |
| Case review | ortho case presentation, records checklist, photos, PDFs | Partial | Add a unified ceph case-review entry and saved composition metadata without duplicating the ortho case. |
| Timelapse | patient timeline and comparison primitives | Missing | Add ordered image/analysis playback with date/phase labels and no fabricated interpolation. |
| Case tags/search | ortho problem list/diagnosis exists; ceph list has no clinical filters | Missing | Derive doctor-reviewed filters from approved structured records; never treat generated tags as definitive diagnosis. |
| Cohort analysis | general reports exist; no ceph cohort analytics | Missing | Add permission-filtered aggregate distributions with minimum cohort size and no patient leakage. |

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
