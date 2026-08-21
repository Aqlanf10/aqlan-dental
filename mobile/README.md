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

## V1 screens

- Staff sign-in
- Mandatory temporary-password change
- Dashboard stats and attention alerts
- Patient search/list
- Patient profile
- Appointment day view
- Account/session screen

The mobile app intentionally consumes the existing API instead of duplicating business logic.
