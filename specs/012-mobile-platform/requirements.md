# Mobile platform requirements

**Spec:** MOBILE-PLATFORM-001  
**Owner directive:** `docs/governance/MOBILE_OWNER_DIRECTIVE_2026-08-21.md`

## Goal

Deliver two independent Android applications backed by the existing Aqlan Dental Pro data:

1. Staff app: the existing `mobile/` Expo client.
2. Patient app: a distinct `patient-mobile/` Expo client limited to patient-portal capabilities.

## Requirements

- **MOBILE-REQ-001:** Staff and patient apps must have different Android application IDs, release pipelines, token namespaces and navigation trees.
- **MOBILE-REQ-002:** Staff native authentication may use only `/api/auth/mobile/*`; patient native authentication may use only `/api/portal/mobile/auth/*`.
- **MOBILE-REQ-003:** Patient JWTs must continue to satisfy `PatientAccess` and must never authorize `StaffOnly` endpoints.
- **MOBILE-REQ-004:** Browser authentication remains cookie-based. Native refresh tokens are returned only by explicit mobile aliases and stored only in the OS secure credential store.
- **MOBILE-REQ-005:** A patient client must never call staff credential-management endpoints under `/api/portal/credentials/*`.
- **MOBILE-REQ-006:** The patient app exposes only patient-safe profile, appointments, treatments, visits, prescriptions, finance summary and messaging DTOs.
- **MOBILE-REQ-007:** Patient changes are constrained to existing portal commands and server-side validation; no direct database access exists in either app.
- **MOBILE-REQ-008:** Failure of either mobile bundle must not change the web application. Patient routes remain a separately authorized API surface and a separately deployable client.
- **MOBILE-REQ-009:** Arabic RTL is the initial UI language; English localization is added without changing API contracts.
- **MOBILE-REQ-010:** Every mobile pull request must pass backend authorization tests and its app-specific typecheck/export workflow.
- **MOBILE-REQ-011:** The patient network client must validate the normalized URL, reject absolute/protocol-relative/traversal paths, and prove that every request remains under `/api/portal/`.
- **MOBILE-REQ-012:** Production release must not claim independent concurrent patient sessions until the server either stores refresh sessions per device or the owner explicitly accepts the current one-refresh-token-per-account limitation.

## Non-goals for the first slice

- No database migration.
- No replacement of the web portal.
- No duplication of finance, appointment or messaging business rules in a mobile client.
- No publishing or production rollout before a signed release and acceptance testing.
