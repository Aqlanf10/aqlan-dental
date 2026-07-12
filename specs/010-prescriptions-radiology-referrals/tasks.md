# 010 — Tasks

- `RX-TASK-001` Backend: `RadiologyOrder` entity + configuration + DbSet +
  idempotent `EnsureRadiologyOrdersSchemaAsync` startup DDL +
  `api/radiology-orders` controller (POST / GET by patient / GET id / DELETE,
  FluentValidation with Arabic messages). (strong model)
- `RX-TASK-002` Backend: seed + serve EN identity keys
  (`website.clinicNameEn`, `website.addressEn`, `website.leadDoctorEn`,
  `website.leadDoctorCredentialsEn`) in `PublicController` website-settings +
  `StartupDatabaseMaintenance.EnsureWebsiteSettingsSeedAsync`. (cheap model OK)
- `RX-TASK-003` Frontend: radiology order create page
  (`/radiology-orders/new?patientId=`), detail page with English
  `RadiologyReferralPrint`, launch buttons (patient quick actions,
  prescriptions list header). (strong model)
- `RX-TASK-004` Frontend: convert `PrescriptionPrint` to English LTR; extend
  `useClinicBranding` with EN fields. (cheap model OK)
- `RX-TASK-005` Tests: component tests for both print forms (English labels,
  settings-driven identity), backend validator tests. (cheap model OK)
