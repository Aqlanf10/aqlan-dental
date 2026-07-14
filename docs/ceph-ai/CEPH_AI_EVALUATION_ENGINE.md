# Cephalometric Landmark Evaluation Engine

Status: implemented offline landmark evaluator. It calculates evidence; it does not create a trained model or establish clinical accuracy.

## Runtime Contract

- Schema: `schemas/ceph-landmark-evaluation-v1.schema.json`
- Contract discovery: `GET /api/ceph-benchmark/contract`
- Evaluation: `POST /api/ceph-benchmark/evaluate-landmarks`
- Authorization: `AdminOnly`
- Request limit: 5 MB
- Storage: none; the request and result are not persisted
- Protocol: `ADP-CEPH-VAL-v1`
- Landmark definitions: `ADP-LM-LAT-v1`

The endpoint uses a route-local strict JSON contract. Undeclared properties, numeric enum values, composite enum strings, duplicate image or landmark entries, unknown image IDs, invalid coordinates, and invalid confidence values are rejected. Omitted prediction cases and omitted visible points are valid model outcomes and are counted as failures rather than contract errors.

## Evaluation Scope

The primary report evaluates the 24 core lateral landmarks. Optional `SPog`, `U6`, and `L6` predictions are accepted by the input contract but do not alter the core aggregate. Every request selects exactly one of `Validation`, `InternalTest`, or `ExternalClinicalTest`; training is forbidden and splits are never pooled into one overall metric. Run separate pinned requests to compare internal with external performance.

Gold points marked `Visible` form the primary denominator. `NotVisible` gold points are excluded from radial error, counted separately, and any coordinate prediction on them is reported as a predicted-on-not-visible error. A model cannot improve its primary SDR by omitting difficult points:

- observed-only SDR uses predictions with a radial error;
- failure-penalized SDR uses all visible gold points as the denominator;
- missing cases, missing points, `NotFound`, and `Rejected` are failures in the latter;
- thresholds are inclusive (`error <= threshold`).

## Reported Metrics

Overall, per landmark, per de-identified image UUID, and by subgroup, the engine reports:

- eligible, observed, failed, not-visible, and predicted-on-not-visible counts and rates;
- patient-cluster and case counts;
- MRE, median, sample SD, P90, P95, and maximum radial error in millimetres;
- SDR at 1.0, 1.5, 2.0, 2.5, 3.0, and 4.0 mm;
- all-landmarks-complete count and rate;
- patient-clustered percentile-bootstrap 95% CI for MRE and failure-penalized SDR;
- split, site, device, age band, skeletal class, angle pattern, and image-quality subgroups.

Bootstrap sampling is by `patientGroupId`, so multiple visits from one patient are resampled together. Results are deterministic for the pinned random seed. CIs are omitted when fewer than two patient clusters are available, and subgroup output raises a small-cluster warning below 30 patients. The patient linkage hash is used only inside the calculation and is never returned.

## Statistical Definitions

Radial error is computed in original image pixels and converted with the case calibration:

`sqrt((predictionX - goldX)^2 + (predictionY - goldY)^2) * millimetresPerPixel`

Quantiles use linear interpolation at `(n - 1) * probability`. Standard deviation is the sample SD with denominator `n - 1`; it is null for fewer than two observed errors. Bootstrap limits are the interpolated 2.5th and 97.5th percentiles. The engine does not round stored results.

A rate is null, rather than zero, when its denominator is zero. This applies, for example, when a landmark is gold-labelled not visible in every case of a subgroup.

## Evidence Boundary

This stage does not yet calculate derived-measurement bias, Bland-Altman limits, repeatability/ICC, calibrated uncertainty, paired comparator deltas, or non-inferiority. Those require a shared frozen geometry engine and repeated or paired inference contracts. Until those stages and the locked adjudicated study are complete, the baseline remains `not measured` and WebCeph-equivalent accuracy must not be claimed.
