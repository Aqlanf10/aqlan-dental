# Cephalometric AI Model and Inference Lineage

Status: implementation contract for SEQ-56. This contract records what ran; it does not establish clinical accuracy.

## Immutable model identity

Every candidate identity pins provider, model id, model version, preprocessing version, dataset version, landmark-definition version, and artifact SHA-256. The canonical identity SHA-256 is computed from all seven values. Provider-managed models whose artifact digest and training data are unavailable are recorded as `observed`; they cannot be approved or pinned.

Candidate promotion requires three non-empty evidence references:

- locked validation report
- independent statistics approval
- orthodontist clinical approval

Approval is an administrative evidence attestation. The registry does not infer that a report is valid and does not convert CI success into a clinical-equivalence claim.

## Deployment and rollback

The `ceph-landmark-draft` slot stores the active and immediately previous approved model versions. Pinning changes only future inference. Rollback swaps those pointers in one operation. Existing analyses, inference runs, original predictions, and doctor corrections retain their original model ids.

Pin, rollback, and inference also require the model's preprocessing and landmark-definition versions to match the contracts implemented by the running application. An evidence record cannot claim a different pipeline from the code that actually executes it.

When no approved version is pinned, the configured external provider/model is recorded as an unapproved `observed` version. This preserves the existing draft workflow without silently promoting a general vision provider.

## Inference run

Each draft or single-landmark refinement persists:

- analysis and immutable model-version ids
- operation and precision mode
- image dimensions and SHA-256, but not image bytes or file path
- start/completion timestamps and triggering user id
- bounded success/failure state
- original normalized coordinates and confidence only
- SHA-256 of the canonical original-prediction JSON

Provider reasoning text is intentionally excluded from the persisted original prediction. It remains available in the transient doctor-review response, but cannot introduce unnecessary free text into the lineage record.

A completed run cannot be completed or failed again through the service. Clinical landmark saves may reference only a successful run from the same analysis. Each saved AI landmark retains the proposal, final doctor-reviewed coordinate, correction error in millimetres, model id, and inference-run id.

## API boundary

- `GET /api/ceph-ai-models`: Admin-only registry and deployment state.
- `POST /api/ceph-ai-models`: register a versioned candidate.
- `POST /api/ceph-ai-models/{id}/approve`: attach the three evidence approvals.
- `POST /api/ceph-ai-models/{id}/pin`: pin an approved candidate.
- `POST /api/ceph-ai-models/rollback`: restore the previous approved version.
- `GET /api/ceph/{id}/ai/inference-runs`: authorized case-scoped lineage summary; original predictions are not returned.

The existing AI endpoints still return unsaved drafts. They now include `inferenceRunId`, registry key, preprocessing version, and landmark-definition version so the reviewed save can preserve exact lineage.

## Evidence boundary

Successful migrations, hashes, API tests, and rollback tests prove traceability mechanics only. Model quality remains `not measured` until the frozen benchmark and independent clinical study are executed. WebCeph-equivalent accuracy remains prohibited language until the preregistered paired non-inferiority gate passes.
