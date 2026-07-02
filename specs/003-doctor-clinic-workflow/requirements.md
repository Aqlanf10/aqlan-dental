# 003 Doctor Clinic Workflow Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/doctor-clinic/`, `frontend/src/app/(dashboard)/schedule/`, patient tabs, `PatientJourneyController.cs`, `VisitsController.cs`, `TreatmentPlanController.cs`, `DoctorSchedulesController.cs`.

- `DC-REQ-001`: Doctor clinic SHALL use existing doctor-clinic and patient journey owners.
- `DC-REQ-002`: Doctors SHALL see only allowed patient information.
- `DC-REQ-003`: Treatment, visit, prescription, referral, surgery, and ortho links SHALL use existing modules.
- `DC-REQ-004`: Doctor-specific schedule labels SHALL remain aligned with route roles.

## Target State

Doctors can work from one clinic workspace without duplicated patient modules.

## Risks

Patient access widening, duplicated doctor dashboard, conflicting workflow with daily operations.

## Allowed Future Work

Improve doctor room assignment, visit summary, next actions, treatment-plan ergonomics.

## Forbidden Future Work

Creating a new patient record surface or bypassing `PatientAccessFilter`.

## Acceptance Criteria

- WHEN a doctor opens clinic workflow THEN existing patient/visit APIs SHALL be used.
- WHEN a doctor lacks patient access THEN backend SHALL deny access.
- Needs runtime verification for complete doctor workflow.
