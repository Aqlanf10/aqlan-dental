# 010 — Prescriptions in English + External Radiology Referrals

## Current State (Evidence)

- `backend/src/AqlanDentalPro.Domain/Entities/Prescription.cs` — prescriptions exist
  (Drugs as JSONB); printing is frontend-only via
  `frontend/src/components/prescriptions/PrescriptionPrint.tsx` (`printScreen()`),
  fully Arabic/RTL.
- No concept of an outgoing imaging order: `Radiograph` stores uploaded results
  only; `InternalReferral`/`HospitalReferral` are doctor→doctor / surgery-specific.
- Owner directive (2026-07-12): when the doctor orders a panoramic (OPG) or 3D
  (CBCT) study, a printed referral must go with the patient to the external
  radiology center; **all printed prescription-family forms must be in English**
  (external centers/pharmacies read English).

## Requirements

- `RX-REQ-001` A staff user SHALL be able to create a radiology order for a
  patient with: study type (panoramic / cbct_3d / lateral_ceph / pa_ceph /
  bitewing / periapical / other), optional region/details, optional clinical
  notes/reason, ordering doctor.
- `RX-REQ-002` The radiology order SHALL have a printable **English** referral
  form (LTR) carrying: clinic identity in English (from Settings — no
  hardcoding), lead-doctor line per the owner's report-identity decision,
  patient name/age/gender, requested study, region, clinical notes, date,
  doctor signature area, and clinic contact footer.
- `RX-REQ-003` The prescription print form (`PrescriptionPrint`) SHALL be in
  English (labels/layout LTR); user-entered values print as entered.
- `RX-REQ-004` English clinic identity SHALL come from Settings keys
  (`website.clinicNameEn`, `website.addressEn`, `website.leadDoctorEn`,
  `website.leadDoctorCredentialsEn`) served by `/api/public/website-settings`
  with English fallback defaults — configurable, never hardcoded in components.
- `RX-REQ-005` Radiology orders SHALL be listed per patient (patient file) and
  creatable from the patient quick actions and the prescriptions area.
- `RX-REQ-006` API errors SHALL carry Arabic `message` fields (constitution).
  Schema for existing production DBs SHALL be created via the idempotent
  startup-DDL pattern (no migration-chain changes).

## Out of Scope

- Tracking the result upload (existing `Radiograph` flow covers results).
- Status workflow beyond created/soft-deleted (no accepted/completed lifecycle).
- Backend PDF rendering (browser print like prescriptions; QuestPDF later if needed).

## Acceptance Criteria

- WHEN a doctor creates a radiology order for OPG THEN a detail page SHALL
  offer Print and the printed page SHALL be fully English with settings-driven
  clinic identity.
- WHEN `website.clinicNameEn` is changed in Settings THEN the referral and
  prescription printouts SHALL reflect it without redeploy.
- WHEN a prescription is printed THEN all form labels SHALL be English.
