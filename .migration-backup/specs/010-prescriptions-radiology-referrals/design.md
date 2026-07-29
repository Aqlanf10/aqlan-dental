# 010 — Design Notes

## RadiologyOrder entity

Mirrors the small-clinical-record pattern (`Prescription`/`InternalReferral`):
BaseEntity + `PatientId`, `DoctorId?` (FK → Doctors.Id, resolved from the
current user's Doctor row when omitted — same as prescriptions), `VisitId?`,
`StudyType` (string enum: `panoramic|cbct_3d|lateral_ceph|pa_ceph|bitewing|
periapical|other` — aligned with `Radiograph.XrayType` values so a later
"attach result" feature maps 1:1), `Region?` (max 200), `ClinicalNotes?`
(max 1000). Soft delete via `IsActive`.

Schema for existing production DBs: `EnsureRadiologyOrdersSchemaAsync` with
`CREATE TABLE IF NOT EXISTS` + patient index, appended to the Ensure chain
(non-fatal catch) — the established pattern; the migration chain is not touched.

## API

`api/radiology-orders` (StaffOnly + PatientAccessFilter, mirroring
`PrescriptionsController`):
- `POST` — create (validates StudyType against the allowed set; Arabic messages)
- `GET ?patientId=&page=&pageSize=` — paginated list
- `GET {id}` — detail incl. patient name/DOB/gender + doctor name for the printout
- `DELETE {id}` — soft delete

## English clinic identity

`/api/public/website-settings` gains `clinicNameEn`, `addressEn`,
`leadDoctorEn`, `leadDoctorCredentialsEn` (Settings keys
`website.*En`, seeded English defaults matching the owner's report-identity
decision: lead doctor "Dr. Aqlan Alkamel — Orthodontic Specialist",
credentials "Central University of Manila — Philippines").
`useClinicBranding` exposes them; both print components consume them.
No hardcoded identity in components (constitution).

## Print forms (frontend-only, `printScreen()`)

- `RadiologyReferralPrint` — new, English LTR: EN clinic header + lead-doctor
  line, To: Radiology Center, patient block (name/age/sex/date), requested
  study (highlighted), region, clinical notes, doctor signature, contact footer.
- `PrescriptionPrint` — converted to English LTR (labels only; entered values
  print as entered). Same props; callers unchanged except passing EN branding.
