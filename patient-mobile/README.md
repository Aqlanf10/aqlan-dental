# Aqlan Dental Patient Mobile

Independent Android patient portal for Aqlan Dental Pro.

- Distinct Android package: `com.aqlandental.patient`
- Patient-only API namespace: `/api/portal/*`
- Native auth namespace: `/api/portal/mobile/auth/*`
- Access and refresh tokens stored in `expo-secure-store`
- No staff endpoints and no direct database access

## Verification

```bash
npm install --no-audit --no-fund
npm run typecheck
npm run doctor
EXPO_PUBLIC_API_URL=https://patient-mobile-ci.invalid npm run export:android
```
