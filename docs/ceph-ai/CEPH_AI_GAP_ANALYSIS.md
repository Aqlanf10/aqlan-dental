# Cephalometric AI Gap Analysis

Status: code and workflow audit, 2026-07-14
Decision: Aqlan must provide a native, independently validated workflow. WebCeph import is optional interoperability, not the product strategy.

## Executive finding

Aqlan already has a broad cephalometric workspace and strong doctor-review gates. Its main clinical-accuracy limitation is upstream: `CephAiLandmarkDraftService` sends the radiograph to a general multimodal provider using anatomical prompt text. This is explicitly described in the product as a non-certified draft. It is not a specialized heatmap/keypoint model and has no independently reproduced cephalometric benchmark.

The current correction dashboard is useful product telemetry, but it is not a clinical validation system. It groups reviewed point corrections by model and reports mean, median, P95, within 1 mm, and within 2 mm after only 30 points/3 analyses. It lacks a locked test set, adjudicated gold standard, per-landmark and subgroup analysis, confidence intervals, measurement error, repeatability, and immutable dataset/preprocessing lineage.

## Capability matrix

| Capability | WebCeph Observed | Aqlan Current | Gap | Clinical Risk | Priority | Proposed Fix |
|---|---|---|---|---|---|---|
| Native AI tracing | Saved AI tracing and re-detection controls | General Gemini/Anthropic vision prompt; draft only | No specialized ceph model or reproduced benchmark | Systematic point error may look plausible | P0 | Benchmark specialized heatmap/keypoint candidates, then integrate one versioned model behind a stable inference contract |
| Orientation check | Manual says right-facing lateral is expected and warns on unfamiliar images | Prompt assumes patient faces right | No deterministic flip/laterality detector | Mirrored predictions and invalid measurements | P0 | Add explicit orientation classifier/rules and a blocking manual confirmation when uncertain |
| Image quality gate | WebCeph documents warnings for unfamiliar/non-lateral inputs | Viewer adjustments only | No scored quality/OOD/missing-anatomy gate | AI runs on unsuitable images | P0 | Versioned quality assessment for projection, crop, blur, contrast, resolution, artifacts, and anatomy coverage |
| Preprocessing | Internal details not exposed | Raw JPG/PNG/WebP bytes sent to provider | No immutable, testable preprocessing version | Device/domain variation and unreproducible results | P0 | Preserve original; create versioned letterboxed inference derivative and log transform metadata |
| Calibration | Preset and image-size calibration visible | Saved `PixelsPerMm` and image dimensions | DICOM pixel spacing and calibration provenance are limited | Millimetre errors and linear measures can be wrong | P0 | Add calibration source, unit, ruler endpoints/DICOM tags, verifier, and versioned audit record |
| Landmark set | 30 sidebar labels observed plus dental drawing objects | 24 core + optional `SPog/U6/L6`; provider allows 27 | Different naming/coverage; prompt service advertises requested 24 while parser accepts 27 | Missing or semantically mismatched points | P0 | Freeze `ADP-LM-LAT-v1`, map aliases explicitly, and reject unknown semantic substitutions |
| Double contours | Product offers modify/review workflow | Prompt has partial side rules for Or/Po/Co | No shared adjudication rule per landmark | Inconsistent labels and training noise | P0 | Put double-contour rules in versioned definitions and reviewer tooling |
| Confidence | No numeric confidence visible in inspected state | Provider self-reported confidence; low threshold 0.7 | Confidence is not calibrated; high mode omits below 0.5 while UI warns below 0.7 | False reassurance or missing points | P0 | Learn/calibrate uncertainty on validation data; unify thresholds and expose reason codes |
| Partial result handling | Completion state is prominent | Provider accepts as few as 8 points; core readiness blocks later | AI success can be shown despite major omissions | Reviewer may miss absent critical points | P0 | Return explicit per-key status and fail/partial state; never call partial output complete |
| Anatomical validation | Manual modification and visible tracing | No post-inference anatomical constraint engine | Implausible relationships are not automatically flagged | Gross errors propagate to measurements | P0 | Add non-destructive plausibility rules and region/neighbor checks |
| Doctor review | Modify, reset, save, landmark table | Drag correction, per-point review, approval gates | No dedicated sequential review queue or batch sign-off | Slow review and skipped points | P1 | Grouped review queue, unresolved counter, keyboard nudge, and explicit reviewed-all action |
| Local point inspection | Dense landmark list | Point selection and canvas controls | No automatic regional zoom/anatomical patch/definition panel | Fine errors are harder to see | P1 | Add click-to-zoom patch with definition, neighbors, confidence, and source |
| Undo/redo | Not established in observation | Limited editing state; no complete audited stack | Corrections are harder to reverse safely | Accidental point movement | P1 | Add bounded undo/redo for point edits, calibration, and viewer transforms |
| AI vs doctor overlay | Not established in observation | Original AI proposal is persisted | No first-class overlay/toggle and correction vector summary | Lost learning/review context | P1 | Overlay proposal/correction vectors and preserve both in version snapshots |
| Tracing contours | Tracing visible in analysis workflow | Polylines connecting landmark keys | Not anatomical segmentation/contour prediction | Visually crude or misleading contours | P1 | Add reviewed contour model or spline/segmentation layer, clearly distinguished from landmark lines |
| Measurement engine | Analysis follows saved digitization | Broad deterministic engine, including true molar-based Wits when possible | Needs independent formula fixtures and measurement-error validation | Correct points can still yield wrong values | P0 | Golden geometry fixtures plus gold-standard measurement comparison and tolerance review |
| Readiness consistency | Saved/completed states visible | Canonical key-based readiness exists | List/legacy quality surfaces still use `>=24` count in places | Optional keys may mask missing core keys | P0 | Remove count-only gates and centralize canonical core-key readiness |
| Report provenance | Report/export workflow available | PDF requires approval and includes tracing/measurements | Missing full model/dataset/preprocessing/definition lineage statement | Report cannot be reproduced exactly | P0 | Add immutable inference/version provenance and required AI-assisted approval wording |
| Accuracy metrics | Internal accuracy is not exposed | Correction telemetry by model | No clinical benchmark or CI | Unsupported “same as WebCeph” claim | P0 | Implement the locked validation protocol before any accuracy claim |
| Repeatability | Three-run test not yet measured | No inference-run comparison tool | Stochastic instability is invisible | Same image may produce different care inputs | P0 | Persist run IDs/seeds/settings and add repeatability evaluator |
| Dataset governance | WebCeph data rights are external | Patient data protected operationally | No dedicated training-dataset approval/version workflow | Consent, leakage, and licensing risk | P0 | Implement the governance controls in `CEPH_AI_DATA_GOVERNANCE.md` |
| Model registry | WebCeph internal details not exposed | Provider/model string only | No immutable model registry, promotion, rollback, or pinning | Silent production model changes | P0 | Registry with artifact hash, contract, license, metrics, stage, and rollback |
| WebCeph interoperability | Landmark table/API documented | Import and migration page are prominent | Import can be mistaken for core product strategy | Dependency and licensing confusion | P2 | Move under Migration/Interoperability; make native Aqlan tracing the primary path |

## Current data contract findings

Persisted point provenance includes placement source, source key, source model ID, confidence, reasoning, original AI proposal coordinates, review state, and correction error in millimetres. Analysis approval stores approver and time. This is a useful base.

Missing immutable fields include:

- inference run ID and timestamp per result;
- model name, semantic version, artifact digest, runtime, and configuration;
- dataset version, preprocessing version, and landmark-definition version;
- original prediction set independent from the mutable active points;
- reviewer identity/time per point and adjudication outcome;
- image quality, source/device, orientation, calibration provenance, and OOD status;
- deployment stage, shadow/canary cohort, and rollback lineage.

## P0 acceptance boundary

No model may be described as clinically accurate, WebCeph-equivalent, or production-ready until:

1. the same locked, patient-separated test cases are run through both candidates;
2. the same adjudicated gold standard and landmark definitions are used;
3. confidence intervals and clinically defined non-inferiority margins are reported;
4. per-landmark, subgroup, measurement, repeatability, failure, and correction metrics pass;
5. an orthodontist signs the validation report and residual risks;
6. the model is pinned in the registry and released through shadow/canary controls.

## Evidence inspected

- Frontend ceph pages/components, `cephMath.ts`, `cephTracing.ts`, `cephReadiness.ts`, and related tests.
- `CephController`, `CephService`, `CephAiLandmarkDraftService`, provider prompts/parsers, entities, DTOs, migrations, PDF generator, and ceph unit tests.
- `specs/005-cephalometry-workspace/` and binding governance documents.
- Authenticated WebCeph workflow observation plus the official manual/product/API documentation.
