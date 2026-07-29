# WebCeph Functional Benchmark

Status: observational baseline, not an accuracy certification
Date: 2026-07-14
Scope: one owner-authorized lateral cephalometric record in an authenticated WebCeph session, the supplied WebCeph user manual, and public WebCeph documentation.

## Safety and evidence boundary

- No patient name, identifier, image, screenshot, coordinate export, or clinical conclusion is stored here.
- The inspection records visible behavior only. It does not infer WebCeph's model, training data, source code, or undocumented API.
- WebCeph is a functional comparator, not a gold standard. Accuracy can be compared only when both systems are evaluated against the same independently adjudicated orthodontist labels.
- The observed record already contained a saved AI tracing. A controlled three-run repeatability result is not yet available, so no repeatability number is claimed.

## Observed workflow

1. Open a patient record and select the lateral cephalometric image.
2. Enter `Digitization`; the record shows whether landmark detection is complete.
3. Run or review AI landmark detection, then use `Modify` to inspect/correct points.
4. Use calibration controls (`Apply Preset Calibration` or `Image Size Calibration`) and image brightness/contrast controls.
5. Review the landmark list, use `Extra Landmarks` when needed, inspect `Landmark Table`, and save.
6. Move to `Analysis` after digitization is saved. Additional visible work areas include PA, Soft-Tissue, Occlusogram, Assessment, Treatment, Superimposition, Viewer, Case, and Timelapse.
7. Review analysis/tracing outputs and generate an eligible report from the saved record.

The supplied manual also documents Standard and Simplified views, AI-driven PA and soft-tissue analysis, multiple superimposition, landmark/movement tables, timelapse, and image crop. The public product page confirms extra landmarks, landmark-table export, multiple superimposition, clinic-logo reports, VTO/STO movement tables, comparison charts, and crop features.

## Landmark observations

The visible lateral digitization sidebar exposed 30 labels:

`A`, `ANS`, `Ar`, `B`, `Ba`, `Co`, `DC`, `G`, `Go`, `LL`, `Me`, `Na`, `Or`, `PNS`, `Po`, `Pog`, `Pn`, `Pt`, `R1`, `R3`, `S`, `A'`, `B'`, `Me'`, `Na'`, `Pog'`, `Sn`, `UL`, `Xi`, `SOr`.

The accessibility layer also named dental drawing objects such as `Upper_1`, `Lower_1`, `Upper_6`, `Lower_6`, incisal/root tips, and molar mesial/distal objects. These are recorded as visible UI objects only; they do not establish an internal WebCeph label count or model output contract.

No numeric per-landmark confidence was visible in the inspected digitization state. The absence of a visible value in this state does not prove that WebCeph does not calculate confidence internally.

## Strengths observed

- A compact digitization-to-analysis workflow with clear saved/completed state.
- A dense landmark review surface with direct modification, reset, extra-landmark, and landmark-table controls.
- Calibration and viewing adjustments are kept close to tracing.
- Analysis, treatment, superimposition, case review, and timelapse are integrated around the same record.
- Automated case tags are visibly qualified as non-definitive rather than presented as final diagnosis.

## Weaknesses or unknowns

- Numeric point confidence and uncertainty reasons were not visible in the inspected state.
- The UI did not expose model version, preprocessing version, training dataset version, or landmark-definition version.
- A saved AI result does not expose its independent clinical error against an adjudicated gold standard.
- One authorized case cannot establish accuracy, robustness, device generalization, fairness, or repeatability.
- Reset/re-run can alter the saved clinical record, so repeatability testing must use a disposable authorized copy or a controlled benchmark harness.

## Aqlan capabilities already present

- Canonical `/ceph` workspace, image upload, calibration, brightness/contrast/invert, zoom/pan, and non-destructive viewer tools.
- 24 core lateral landmarks plus optional `SPog`, `U6`, and `L6`.
- AI draft, per-point refinement, confidence display, doctor correction, provenance, original AI coordinates, and review gates.
- Deterministic measurements for Steiner, Tweed, McNamara, Ricketts, Downs, Jarabak, and Wits.
- Saved measurements, doctor approval, PDF/CSV, VTO scenarios, structural superimposition, PA analysis, case review, cohort privacy, and timelapse.
- Account-owned WebCeph landmark-table import exists, but it is a migration aid and must remain secondary to Aqlan's native workflow.

## Functional gaps to close in Aqlan

1. Replace the general-purpose vision prompt path with a versioned, specialized cephalometric landmark model after benchmark selection.
2. Add deterministic orientation/quality checks and versioned preprocessing while retaining the immutable original image.
3. Add anatomical plausibility checks that warn and lower trust without silently moving accepted points.
4. Add a guided review queue by anatomical group, automatic local zoom/patch, keyboard nudge, full undo/redo, and AI-vs-doctor overlay.
5. Add model-registry and immutable inference-run records with full version lineage.
6. Add a reproducible clinical evaluation engine and accuracy dashboard with per-landmark, subgroup, measurement, runtime, correction, and drift metrics.
7. Make native AI tracing the primary action; keep WebCeph import under migration/interoperability.

## Repeatability protocol for the controlled rerun

For the same de-identified image and fixed calibration/preprocessing configuration:

1. Run each system three times with the same model version and settings.
2. Export or capture normalized coordinates without patient identifiers.
3. Compute pairwise radial displacement in millimetres for every landmark.
4. Report per-landmark mean/max displacement, overall median/P95, and exact-match rate.
5. Record upload-to-draft latency and any missing/failed landmarks.
6. Never overwrite the gold-standard annotation or doctor-approved clinical record.

Current result: **not measured**. No numeric claim is permitted until the controlled rerun is completed.

## Official references

- [WebCeph user manual](https://public-assets.webceph.com/static/pdf/%5BWEBCEPH%5DUser_manual_US.pdf)
- [WebCeph product capabilities](https://doc.webceph.com/en/pricing/)
- [WebCeph partner API documentation](https://doc.webceph.com/en/api/)

The official API is limited to partner integrations, requires an agreed partner key and Premium-or-higher account, and documents patient/record/image operations. It does not provide a licensed shortcut to WebCeph landmark predictions or clinical analysis data.
