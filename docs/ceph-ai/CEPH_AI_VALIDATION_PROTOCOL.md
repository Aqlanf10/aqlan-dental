# Cephalometric AI Clinical Validation Protocol

Protocol ID: `ADP-CEPH-VAL-v1`
Landmark definition set: `ADP-LM-LAT-v1`
Status: prospective protocol; no accuracy result is implied

## Objective

Measure whether a pinned Aqlan model produces clinically acceptable, repeatable lateral-cephalometric landmarks and derived measurements on independent data. WebCeph may be evaluated as a comparator on the same images when licensing and exports permit, but orthodontist-adjudicated coordinates remain the reference.

## Preregistration

Before opening the locked test labels, freeze and hash:

- model artifact, runtime/container, inference code, and configuration;
- preprocessing and postprocessing versions;
- landmark-definition version;
- train/validation/internal-test/external-test patient IDs as one-way salted hashes;
- primary/secondary metrics, subgroup definitions, exclusion rules, failure handling, and clinical margins;
- statistical analysis script and random seed.

Any change after unblinding creates a new experiment/model version.

## Dataset unit and schema

One row per radiograph plus one row per landmark annotation. Required case fields:

| Field | Requirement |
|---|---|
| `imageId` | Non-identifying stable UUID |
| `patientGroupId` | Salted linkage key used only to prevent leakage |
| `imageSha256` | Integrity/deduplication hash |
| dimensions | Original width and height |
| calibration | Pixel spacing or validated ruler scale, unit, source, and verifier |
| source | De-identified site/device/vendor/model category |
| orientation | Projection and facing direction, plus confidence/reviewer |
| quality flags | Normal, low contrast, blur, double contour, metal, missing anatomy, severe crop, low resolution |
| clinical strata | Pediatric/adult, skeletal Class I/II/III, high/average/low angle; labels require clinician approval |
| definitionVersion | `ADP-LM-LAT-v1` or successor |
| preprocessingVersion | Exact transform contract or `none` |

Per-landmark fields: image ID, key, reviewer ID alias, X/Y in original pixels, visibility, double-contour decision, annotation time, tool version, adjudication status, and final gold X/Y.

## Gold standard

1. At least two independent orthodontists annotate each image without seeing model or comparator output.
2. Reviewers use the same frozen definitions and original-resolution image with verified calibration.
3. Compute inter-reviewer radial distance per point in millimetres.
4. Any disagreement above 1.5 mm, visibility conflict, or semantic/double-contour conflict is reviewed by a third orthodontist or consensus panel.
5. The final coordinate is an explicit adjudicated decision. Do not automatically average a genuine anatomical disagreement.
6. Store every original annotation, adjudication reason, reviewer alias, date, and definition version.
7. Estimate inter-observer and intra-observer error on a prespecified repeated subset before model comparison.

## Patient-level partitioning

- `training`: model fitting only.
- `validation`: tuning, threshold calibration, and candidate selection.
- `internal test`: locked patients from development sites; one evaluation per released candidate.
- `external clinical test`: locked patients from at least one unseen site/device workflow.

All images/visits from one patient stay in one partition. Near-duplicate detection and patient linkage checks run before splitting. A site held out for external testing is never used for normalization tuning.

Indicative acquisition stages, not guarantees of statistical adequacy:

- pipeline pilot: 100-200 adjudicated images;
- initial multi-device study: 500-1,000 images;
- clinical validation: 1,500-3,000+ diverse images, with sample size finalized from the primary endpoint, margin, prevalence, clustering, and desired confidence interval.

## Inference procedure

1. Verify checksum, projection, orientation, anatomy coverage, and calibration.
2. Preserve the original image; generate a separately hashed inference derivative.
3. Run the pinned container without access to labels or reviewer corrections.
4. Persist raw heatmaps/distributions when supported, original predictions, postprocessed predictions, confidence, reason/status per key, latency, device, and failures.
5. Missing landmarks are failures, not silently excluded from the primary analysis. Report both complete-case and failure-penalized sensitivity analyses.
6. Repeat a prespecified subset three times with identical settings; if the model is stochastic, also evaluate fixed and varied seeds.

## Landmark metrics

For predicted point `p`, gold point `g`, and millimetres-per-pixel scale `s`:

`radialErrorMm = sqrt((px-gx)^2 + (py-gy)^2) * s`

Report overall, per landmark, and per case:

- mean radial error (MRE), median, standard deviation, P90, P95, and maximum;
- SDR at 1.0, 1.5, 2.0, 2.5, 3.0, and 4.0 mm;
- missing/failure rate and all-landmarks-complete rate;
- bootstrap 95% confidence intervals clustered by patient;
- paired delta and confidence interval against each comparator on the same cases.

Do not pool model versions. Do not use the current product telemetry threshold of 30 points/3 analyses as clinical evidence; it is only a minimum display guard.

## Derived-measurement metrics

Calculate model and gold measurements using the same frozen geometry engine and calibration. At minimum evaluate SNA, SNB, ANB, Wits, FMA, SN-MP, U1-SN, U1-NA angle/distance, L1-NB angle/distance, IMPA, interincisal angle, facial heights/ratios, and soft-tissue lines.

Report mean signed error (bias), mean/median absolute error, SD, P95 absolute error, Bland-Altman limits of agreement, and category disagreement where a measurement drives a class/pattern label. Clinical tolerances must be approved per measurement before unblinding.

## Stratified analysis

Prespecify and report, with sample counts and wide-CI warnings:

- site, device/vendor/model family, image dimensions, and calibration source;
- normal vs low contrast, blur, double contour, metal, crop, missing anatomy, and low resolution;
- pediatric vs adult and narrower age bands where powered;
- skeletal Class I/II/III and high/average/low-angle pattern;
- sex only when lawfully available and clinically justified;
- confidence band, OOD status, and complete vs partial output.

No subgroup is declared equivalent from a non-significant p-value alone.

## Confidence and review utility

Evaluate confidence as an uncertainty signal, not a cosmetic number:

- reliability diagram and expected calibration error;
- error/coverage curve when low-confidence points are referred;
- sensitivity for detecting points above clinically defined error thresholds;
- percentage of low-confidence points sent to review (target: 100%);
- manual-adjustment rate, correction magnitude/time, and false-clear rate.

Thresholds are selected on validation data and frozen before testing.

## Repeatability and runtime

For three runs on the same input, compute pairwise per-landmark displacement, within-case SD, intraclass correlation for derived measurements, missing-key consistency, and maximum drift. Report median/P95 upload-to-draft latency, model-only latency, timeouts, retries, and resource profile.

## Initial gates for clinical-test eligibility

These are target gates from the owner brief, not achieved results:

- overall MRE `<= 2.0 mm`;
- median error `< 1.5 mm`;
- SDR@2 mm `>= 80%` and SDR@4 mm `>= 95%`;
- no critical landmark with MRE `> 3 mm` without a validated warning/referral mechanism;
- 100% of low-confidence cases referred to manual review;
- no unexplained clinically important internal-to-external degradation;
- all prespecified measurement tolerances and safety failure gates pass.

Production eligibility additionally requires orthodontist sign-off, approved data governance, security/privacy review, reproducible build, rollback, shadow evaluation, and canary monitoring.

## Comparator and non-inferiority rule

“Equivalent to WebCeph” may be written only if both systems are run on the same sufficiently sized locked images, scored against the same adjudicated labels, and assessed using a preregistered paired non-inferiority margin with confidence intervals. A single case, visual similarity, build success, or lack of statistical significance is insufficient.

## Research anchors

- The [MICCAI 2023 CL-Detection challenge](https://cl-detection2023.grand-challenge.org/) defines MRE and SDR@2 mm on a 600-image, 38-landmark, multi-center dataset.
- The associated [2024 benchmark paper](https://arxiv.org/abs/2409.15834) reports that the best challenge methods still had failure scenarios; its aggregate results are context, not acceptance evidence for Aqlan.
- Candidate code or weights must pass license, reproducibility, security, and local benchmark review before integration. Published or self-reported numbers are never copied into the Aqlan baseline result.
