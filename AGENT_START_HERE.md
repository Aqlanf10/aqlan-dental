# START HERE — Aqlan Dental Pro Agent Task

You are working on the existing Aqlan Dental Pro repository.

## Critical Decision

Do not rebuild this project from scratch.
Do not migrate the frontend to another framework.
Do not replace the current architecture.

Continue development on the existing codebase.

## Current Stack

- Frontend: Next.js 14 + TypeScript + Tailwind CSS
- Backend: ASP.NET Core Web API .NET 8
- Database: PostgreSQL
- Deployment: Vercel + Railway
- UI: Arabic RTL
- Currency: Yemeni Rial YER

## Read These Files First

1. `docs/AGENT_INSTRUCTIONS.md`
2. `docs/ROADMAP.md`
3. `docs/SPRINT_1_STABILIZATION.md`

## Your First Mission

Complete Sprint 1 only.

Do not start finance, HR, AI, ceph improvements, or new large modules before Sprint 1 is verified.

## Sprint 1 Goal

Stabilize the current system and verify that what exists already works correctly in production.

## Sprint 1 Tasks

### 1. Deployment Verification

- Verify frontend deployment on Vercel.
- Verify backend deployment on Railway.
- Verify both are connected to `main` branch.
- Verify `NEXT_PUBLIC_API_URL` points to Railway backend.
- Verify Railway backend has correct PostgreSQL connection string.
- Verify CORS `AllowedOrigins` includes the Vercel frontend URL.

### 2. Build Verification

Run:

```bash
cd frontend
npm install
npm run build
```

Run:

```bash
cd backend
dotnet restore
dotnet build
```

Fix only build-breaking errors.

### 3. Database Verification

- Check EF Core migrations.
- Ensure migrations are applied to Railway PostgreSQL.
- Verify patient columns exist:
  - NormalizedPhone
  - NormalizedWhatsApp
  - IsActive
  - DeletedAt
  - DeletedBy
- Verify unique indexes for patient numbers and normalized phones.

### 4. Patients Module Testing

Test and fix:

- Add patient.
- Edit patient.
- Duplicate phone prevention.
- Phone normalization.
- Archive patient.
- Restore patient.
- Active / archived / all filter.
- Row actions menu.
- Right-click context menu.
- Search by name, patient number, and phone.

Phone formats that must be treated as the same number:

- 770245745
- 0770245745
- +967770245745
- 00967770245745
- ٧٧٠٢٤٥٧٤٥

### 5. Messaging Test

Test `/messages?patientId=...`.

If it fails because the patient has no portal account, fix it.

Required behavior:

- Staff can open an internal conversation linked to PatientId.
- The patient does not need a portal account.
- Conversation should be visible from messages page and patient file.

### 6. Appointments Test

Test:

- Create appointment.
- Edit appointment.
- Conflict detection.
- Update status.
- Patient appointment link.
- Doctor filter.

### 7. Finance Basics Test

Test:

- Contracts list.
- Create contract.
- Payments list.
- Create payment.
- Finance summary.
- Overdue list.

## Do Not Do Yet

- Do not build HR yet.
- Do not build AI yet.
- Do not rebuild ceph from scratch.
- Do not redesign UI globally.
- Do not add dummy pages.
- Do not add buttons that do not work.

## Expected Output

After completing Sprint 1, provide:

1. Summary of what was tested.
2. Bugs found.
3. Bugs fixed.
4. Files changed.
5. Database migrations added, if any.
6. Remaining issues.
7. Confirmation that build passes.

## Branch Name

Use:

```text
fix/sprint-1-stabilization
```

Open a Pull Request when finished.
