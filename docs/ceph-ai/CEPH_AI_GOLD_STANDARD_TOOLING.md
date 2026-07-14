# Cephalometric Gold-Standard Tooling

Status: implemented contract and stateless validation endpoint. This tooling does not contain a trained model and does not establish clinical accuracy.

## Purpose

This contract creates the locked, de-identified reference data required to measure Aqlan's landmark accuracy. It is intentionally independent of WebCeph exports and patient records. WebCeph remains a lawful functional comparator only; its outputs are not labels, training data, or proof of equivalent accuracy.

## Contract

- Schema: `schemas/ceph-benchmark-manifest-v1.schema.json`
- Runtime contract: `GET /api/ceph-benchmark/contract`
- Validation: `POST /api/ceph-benchmark/validate`
- Authorization: `AdminOnly`
- Request limit: 5 MB
- Storage: none; validation is stateless and never writes a manifest or image
- Landmark definition: `ADP-LM-LAT-v1`
- Core set: 24 lateral landmarks; `SPog`, `U6`, and `L6` are optional

The public JSON uses camel-case property names and string enum values. A schema-valid document must still pass the semantic validator because JSON Schema cannot prove reviewer independence, uniqueness, patient-cluster split isolation, image bounds, or the adjudication decision.

## De-identification Gate

Each case carries only:

- a random image UUID;
- a lowercase SHA-256 image-content digest;
- a separately salted SHA-256 patient-group token used only for split isolation;
- opaque site and device codes;
- broad clinical strata needed for subgroup analysis;
- pseudonymous reviewer, steward, and approval references.

The contract has no patient name, clinical patient identifier, date of birth, file path, image URL, or image bytes. `additionalProperties: false` rejects undeclared fields at every object level. The semantic validator blocks release unless metadata is sanitized, private DICOM tags are removed, pixel inspection passed, and no burned-in identifier was detected.

Salts and the lookup that links a source record to `patientGroupId` stay in the approved data-steward environment. They must never be committed, logged, returned by this API, or shared with model-development staff.

## Annotation And Adjudication

1. Normalize an eligible lateral image to right-facing orientation and record physical calibration without changing aspect ratio.
2. Two independent reviewers annotate every core landmark using the ratified definition document. A reviewer cannot see the other reviewer's coordinates while annotating.
3. The validator calculates the maximum pairwise radial disagreement in millimetres.
4. Agreement at or below `1.5 mm` may use `ConsensusWithinThreshold` with an explicit gold coordinate.
5. Disagreement above `1.5 mm`, visibility disagreement, or a double-contour disagreement requires `ThirdReviewer` or `ConsensusPanel`.
6. A third-reviewer decision requires that reviewer's own annotation and approval alias. Missing anatomy is represented explicitly as `NotVisible` with null coordinates and `AnatomyNotVisible`.
7. Gold coordinates are never auto-averaged. The validator preserves the submitted adjudicated coordinate.

`isStructurallyValid` means there are no malformed-contract errors. `isReleaseReady` additionally means no privacy, annotation, or adjudication blocker remains. Only release-ready manifests may receive an immutable dataset version and enter offline model evaluation.

## Leakage Prevention

Every image UUID and image hash must be unique. All images sharing a `patientGroupId` must remain in one split. A manifest is rejected if a patient's images cross training, validation, internal-test, or external-clinical-test boundaries. External evaluation data must remain frozen and inaccessible to training and threshold tuning.

## Example Request

Use synthetic or already de-identified content only:

```http
POST /api/ceph-benchmark/validate HTTP/1.1
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "schemaVersion": "1.0",
  "datasetVersion": "pilot-001",
  "landmarkDefinitionVersion": "ADP-LM-LAT-v1",
  "createdAt": "2026-07-14T00:00:00Z",
  "cases": ["...schema-conformant de-identified cases..."]
}
```

Do not paste patient data into API debugging tools, issue trackers, pull requests, or test fixtures. Unit tests construct only deterministic synthetic cases.

## Evidence Boundary

Passing this validator proves dataset-contract readiness, not model accuracy. Accuracy claims require the locked evaluation engine, adjudicated cohort, confidence intervals, subgroup results, repeatability analysis, and clinical sign-off defined in `CEPH_AI_VALIDATION_PROTOCOL.md`.
