# Cephalometric AI Implementation Roadmap

Status: proposed sequence after the documentation/validation-foundation PR
Constraint: each PR is independently reviewable, updates specs/governance, includes focused tests, and does not claim clinical accuracy from build success.

## Product direction

Aqlan's own workflow is primary: upload, quality/orientation check, calibration, native AI draft, grouped doctor review, deterministic analysis, approval, and report. WebCeph import remains under migration/interoperability and is not the path to Aqlan accuracy.

## PR sequence

| PR | Scope | Main deliverables | Exit evidence |
|---|---|---|---|
| 1 | Validation specification | Benchmark, gap, protocol, data governance, landmark definitions, baseline template, roadmap | Docs/schema checks; clinical/engineering review; no runtime/model change |
| 2 | Gold-standard tooling | De-identified manifest schema, annotation export/import, two-reviewer disagreement and adjudication workflow | Schema/unit tests; sample synthetic fixture; privacy review |
| 3 | Evaluation engine | Patient-clustered MRE/median/SD/P95, SDR 1-4 mm, per-landmark/subgroup/measurement/repeatability metrics and CIs | Deterministic golden fixtures; independent stats review |
| 4 | Inference contract and model registry | Immutable model/preprocessing/dataset/definition versions, inference runs, original predictions, corrections, hashes, pin/rollback fields | Migrations/API tests; old analyses remain readable; no provider promotion |
| 5 | Specialized baseline model | License-approved ceph model adapter, versioned letterbox preprocessing, local/container inference, partial/failure contract | Reproduced benchmark on validation set; security/license review; draft-only UI |
| 6 | Quality, confidence, anatomy | Orientation/OOD/image-quality gate, calibrated uncertainty, anatomical plausibility warnings | Sensitivity/error-coverage and subgroup validation; all low-confidence points referred |
| 7 | Doctor correction workflow | Grouped queue, click-to-zoom patch, definitions, keyboard nudge, full undo/redo, AI/doctor overlay, explicit all-reviewed approval | Frontend/E2E/accessibility tests and clinician usability review |
| 8 | Accuracy dashboard | Locked evaluation reports, per-model/landmark/device/quality metrics, sample counts/CIs, correction rate, drift | Authorization/privacy tests; no patient rows; honest insufficient-sample states |
| 9 | Training pipeline | Approved dataset manifests, reproducible experiments, artifact signing, experiment matrix, no automatic correction ingestion | Reproducibility run, leakage test, license/data approvals |
| 10 | External clinical validation | Frozen candidate on independent site/device cohort, comparator study when lawful, measurement and workflow outcomes | Signed report with limitations and non-inferiority analysis |
| 11 | Controlled production rollout | Shadow evaluation, canary, monitoring, alert thresholds, rollback, version pinning | Release board approval; rollback drill; post-market monitoring plan |

## Immediate P0 work after this PR

1. Implement the evaluation schema/engine before choosing a model.
2. Centralize the 24-key readiness gate and remove count-only checks.
3. Add immutable inference/model lineage without changing historic record semantics.
4. Define the approved dataset acquisition and adjudication study with orthodontists.
5. Evaluate specialized candidates under the same split/preprocessing, including license and deployability.

## Candidate-model experiment matrix

Candidates are selected by evidence, not name or recency. Each row must use the same approved dataset version, patient splits, preprocessing variants, landmark mapping, metrics, and hardware profile.

| Dimension | Required comparison |
|---|---|
| Architecture | Heatmap HRNet/U-Net style, coordinate regression only as a comparator, and any clinically justified hybrid/coarse-to-fine candidate |
| Initialization | From scratch vs permitted pretrained weights, with license/provenance recorded |
| Resolution | Prespecified letterboxed input sizes; no aspect-ratio distortion |
| Preprocessing | Raw normalization, CLAHE experiment, conservative denoise experiment; each versioned |
| Loss/postprocess | Heatmap distribution/soft-argmax choices, missing-point policy, uncertainty calibration |
| Robustness | Site/device/quality/age/skeletal strata and external holdout |
| Operations | Latency, memory, CPU/GPU availability, deterministic mode, failure/retry behavior |

Public challenge results are screening context only. For example, the [CL-Detection benchmark](https://arxiv.org/abs/2409.15834) used 600 multi-center images and 38 landmarks, while individual repositories may use different labels, splits, licenses, or unavailable weights. Aqlan accepts no published number until reproduced on its frozen protocol.

## Release stages

- **Offline:** no clinical UI; benchmark-only.
- **Shadow:** runs beside current workflow; output hidden from care and scored later.
- **Assisted canary:** small authorized cohort; draft visible, mandatory full review, enhanced monitoring.
- **Controlled production:** only after external validation and sign-off; version pinned per analysis.
- **Rollback:** one action returns new inference to the last approved registry version; existing analyses retain their original model lineage.

## Definition of “WebCeph-equivalent accuracy”

The phrase remains forbidden until a paired, preregistered study on the same locked images and adjudicated labels demonstrates that Aqlan satisfies the agreed non-inferiority margin with confidence intervals and no clinically important subgroup/measurement regression. Workflow resemblance and a three-run demo do not satisfy this definition.
