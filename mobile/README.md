# Aqlan Dental Pro Mobile

Native Android/iOS client for the existing **Aqlan Dental Pro** system.

## Architecture

- Expo SDK 57 / React Native
- Expo Router
- Existing ASP.NET Core API and PostgreSQL database
- Same staff accounts, role authorization, branch scoping and patient-access rules as the web app
- Access and refresh tokens are stored only in `expo-secure-store`
- Native refresh uses `/api/auth/mobile/*`; web auth remains cookie-based and unchanged
- Temporary-password users are forced through the existing server-side change-password policy before app access

## Local setup

1. Copy `.env.example` to `.env`.
2. Set `EXPO_PUBLIC_API_URL`.
3. Run `npm install`.
4. Run `npm run typecheck`.
5. Run `npm start`.

For a physical phone, the API URL must be reachable from the phone. Production builds require HTTPS.

## Mobile workspaces

- Staff sign-in
- Mandatory temporary-password change
- Dashboard stats and attention alerts
- Patient search, create, edit and profile workspaces
- Daily and recall appointments
- Daily Operations / Patient Journey
- Clinical visits and handoff to reception
- Patient finance, statements and safe payment entry
- Orthodontics, general dentistry and FDI charting
- Oral surgery and shared orthognathic planning
- Clinical photos, radiographs, documents, prescriptions and referrals
- Lab orders, inventory operations and reports
- Permission-aware settings, account and security screens
- On-device release, API health, session and permission diagnostics
- Official clinic identity with branded Android icon/splash and a unified RTL design system
- Root gesture hardening and Arabic render-error recovery instead of a silent close

The mobile app intentionally consumes the existing API instead of duplicating business logic.
