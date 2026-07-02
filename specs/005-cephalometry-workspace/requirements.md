# 005 Cephalometry Workspace Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/ceph/`, `frontend/src/components/ceph/`, `frontend/src/lib/cephMath.ts`, `cephTracing.ts`, `cephReadiness.ts`, `CephController.cs`, `CephNormsController.cs`, `PhotoAnalysisController.cs`, `CephService`, `CephAiDraftService`, `CephAiLandmarkDraftService`, ceph DTOs/tests.

- `CEPH-REQ-001`: `/ceph` SHALL be the canonical cephalometry workspace.
- `CEPH-REQ-002`: AI-generated ceph diagnosis or landmarks SHALL be draft-only until doctor review.
- `CEPH-REQ-003`: Ceph norms/settings SHALL use existing `CephNormsController` and settings where applicable.
- `CEPH-REQ-004`: Reports SHALL use Arabic PDF identity rules.
- `CEPH-REQ-005`: No fake AI provider, fake measurement, or fake clinical claim is allowed.

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
- Needs runtime verification for doctor review flow.
