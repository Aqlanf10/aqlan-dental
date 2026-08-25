# Staff and patient mobile boundary

This record implements **MOBILE-PLATFORM-001**.

- `mobile/` remains the staff application. It authenticates only through staff-native aliases and relies on server permissions for every operation.
- `patient-mobile/` is a separate patient application and Android package. It authenticates only through patient-native aliases and calls only `/api/portal/*`.
- The web app and browser portal keep their current cookie contracts.
- Both apps use the same authoritative backend data, but neither accesses PostgreSQL directly.
- Patient write operations remain limited to existing portal profile, appointment and messaging commands.
- No database migration or production deployment setting is changed in the first slice.

Security review evidence:
`PatientPortalController.cs`, `PatientPortalMessagesController.cs`,
`AuthorizationPolicyConfiguration.cs`, `PatientAccessFilter.cs`,
`mobile/src/lib/api.ts`, `mobile/src/auth/tokenStore.ts`,
`frontend/src/stores/patientAuthStore.ts`, and portal authorization tests.
