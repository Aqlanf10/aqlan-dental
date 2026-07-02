# 000 Master System Design

## Backend Architecture

- ASP.NET Core 8 API under `backend/src/AqlanDentalPro.API/`.
- Application layer under `backend/src/AqlanDentalPro.Application/` with DTOs, interfaces, validators, and shared constants.
- Domain layer under `backend/src/AqlanDentalPro.Domain/` with entities/enums.
- Infrastructure layer under `backend/src/AqlanDentalPro.Infrastructure/` with EF Core `AppDbContext`, migrations, repositories, and services.
- PostgreSQL via EF Core/Npgsql in `Program.cs`.
- Startup maintenance lives in `backend/src/AqlanDentalPro.API/Configuration/StartupDatabaseMaintenance.cs`.
- Real-time messaging uses SignalR hub `backend/src/AqlanDentalPro.API/Hubs/MessagingHub.cs`.

## Frontend Architecture

- Next.js 14 App Router under `frontend/src/app/`.
- Staff area under `frontend/src/app/(dashboard)/`.
- Portal area under `frontend/src/app/(portal)/`.
- Public booking/home under `frontend/src/app/(public)/home/`.
- Shared components under `frontend/src/components/`.
- API client and route/permission helpers under `frontend/src/lib/`.
- Hooks/stores/types under `frontend/src/hooks/`, `frontend/src/stores/`, and `frontend/src/types/`.
- Arabic RTL root is `frontend/src/app/layout.tsx`.

## Deployment And CI

- Backend is designed for Railway + PostgreSQL per `CLAUDE.md` and `Program.cs`.
- Frontend is designed for Vercel per `CLAUDE.md`.
- CI: `.github/workflows/ci.yml`.
- Arabic encoding guard: `.github/workflows/encoding-guard.yml` and `scripts/check-mojibake.sh`.

## Database

- DbContext: `backend/src/AqlanDentalPro.Infrastructure/Data/AppDbContext.cs`.
- Migrations: `backend/src/AqlanDentalPro.Infrastructure/Data/Migrations/`.
- Migrations are high-risk and must not be edited casually.

## Permissions

- Backend policies: `AuthorizationPolicyConfiguration.cs`.
- Fine-grained permission guard: `PermissionGuard.cs` and `RolePermission`.
- Frontend route guard: `frontend/src/lib/routePermissions.ts`.
- Frontend permission keys/fallback: `frontend/src/hooks/usePermissions.ts`.
- Patient access filter: `PatientAccessFilter.cs`.

## Settings

- Entity: `Setting`.
- API: `SettingsController.cs` and settings-specific controllers.
- Finance settings: `FinanceSettingsKeys.cs`, `FinanceSettingsReader.cs`.
- Clinic/report identity: `FinanceClinicIdentity.cs`.

## PDF And Reporting

- PDF service: `PdfService.cs`.
- Specialized generators include ceph, photo analysis, ortho, ortho model, ortho surgical, and lab order PDF generators.
- Reports route: `ReportsController.cs`, `frontend/src/app/(dashboard)/reports/`.

## Integration Points

- Daily operations integrates appointments, booking requests, queue, rooms, finance checkout, lab, reports.
- Ortho integrates ceph, model analysis, photos, finance, surgery planning, lab.
- Lab integrates finance payables and doctor commissions.
- Patient portal integrates auth, appointments, finance, messages, prescriptions, and treatments.

## Runtime Verification

The static repository evidence confirms architecture and ownership. Exact interactive workflows, screenshots, and deployment behavior require `Needs runtime verification`.
