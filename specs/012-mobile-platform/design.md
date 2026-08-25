# Mobile platform design

**Spec:** MOBILE-PLATFORM-001

## Boundary model

| Surface | Client | Authentication | Allowed API ownership | Forbidden |
|---|---|---|---|---|
| Staff web | Next.js `frontend/` | HttpOnly staff cookies | Existing staff controllers | Patient-token elevation |
| Staff Android | Expo `mobile/` | `/api/auth/mobile/*` + secure store | Existing staff controllers, server permissions and patient-access filter | Portal credentials as patient identity |
| Patient web | Next.js `/portal/*` | `portal_refresh` HttpOnly cookie | `PatientPortalController`, `PatientPortalMessagesController` | Staff controllers |
| Patient Android | Expo `patient-mobile/` | `/api/portal/mobile/auth/*` + separate secure-store keys | Patient portal read models and commands only | `/api/auth/*`, staff routes, portal credential administration |

## Isolation decisions

- The apps are separate build artifacts with distinct Android package IDs.
- Tokens are audience-separated by server claims/policies and locally separated by key names.
- The patient client has a narrow API module whose normalized URL must remain under `/api/portal/`; traversal and absolute/protocol-relative paths fail closed.
- Browser cookie behavior is unchanged. Explicit native aliases carry refresh tokens through a patient-specific header.
- Business logic stays in existing backend services. Mobile clients render DTOs and submit validated commands; they do not write PostgreSQL directly.
- The first slice adds no migration and no production deployment configuration change.

## Failure containment

A UI crash is confined to its application process. A patient request is confined by `PatientAccess`, patientId claims and portal services. CI contract tests protect route aliases, policy ownership and non-serialization of browser refresh tokens.

The backend remains a modular monolith in this slice, so process-level isolation is not claimed. If production telemetry later shows portal load threatening staff operations, the next boundary is a separately deployed patient gateway using the same portal contracts, rate limits and database least-privilege role.

## Token flow

1. Patient signs in through `/api/portal/mobile/auth/login`.
2. The native alias returns access and rotated refresh tokens; the web alias still sets an HttpOnly cookie and omits the refresh token from JSON.
3. The app stores tokens under patient-only secure-store keys.
4. Normal requests carry the access token.
5. On 401, the app calls the explicit native refresh alias with the expired access token plus `X-Aqlan-Portal-Refresh-Token`.
6. Logout revokes the stored server hash and deletes the device copy.

## Known session limitation and release gate

`PatientAccount` currently stores one `RefreshTokenHash` for the whole account. A login or refresh from the patient app can therefore replace the browser portal refresh token, and browser refresh can replace the phone token. Logout also revokes that shared hash.

This does not widen data access, but it prevents a truthful claim of independent simultaneous web/mobile sessions. Before production rollout, choose and verify one of:

1. add append-oriented per-device patient refresh sessions with rotation/revocation tests; or
2. explicitly accept a single active patient session and communicate it to patients.

No production-ready status is allowed while this choice remains implicit.

## Rollback

Revert the feature commits. No schema rollback is needed for the first slice. Existing staff mobile and both web surfaces remain unchanged.
