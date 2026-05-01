# Sprint 1 — Stabilization Checklist

## Goal

Before adding new modules, verify and stabilize the existing Aqlan Dental Pro system.

No redesign. No new large modules. No rebuild from scratch.

## 1. Environment and Deployment

### Frontend / Vercel

- [ ] Vercel project is connected to GitHub repository `Aqlanf10/aqlan-dental`.
- [ ] Production branch is `main`.
- [ ] Frontend root directory is correct, likely `frontend`.
- [ ] `NEXT_PUBLIC_API_URL` points to the Railway backend URL.
- [ ] Latest `main` deployment completed successfully.

### Backend / Railway

- [ ] Railway backend service is connected to GitHub repository `Aqlanf10/aqlan-dental`.
- [ ] Production branch is `main`.
- [ ] Backend root directory/build path is correct, likely `backend`.
- [ ] PostgreSQL connection string is set.
- [ ] Redis connection string is set if Redis is used.
- [ ] JWT variables are set.
- [ ] `AllowedOrigins` includes the Vercel frontend URL.
- [ ] Latest backend deployment completed successfully.

## 2. Build Tests

### Frontend

```bash
cd frontend
npm install
npm run build
```

Result:

- [ ] Build passes.
- [ ] No TypeScript errors.
- [ ] No missing app/pages directory issue.

### Backend

```bash
cd backend
dotnet restore
dotnet build
```

Result:

- [ ] Build passes.
- [ ] No missing SDK/workload errors.
- [ ] No nullable/blocking compile errors.

## 3. Database and Migrations

- [ ] EF Core migrations exist.
- [ ] Railway PostgreSQL has latest migrations applied.
- [ ] Seed data does not reset production data.
- [ ] `Patients` table contains:
  - [ ] `NormalizedPhone`
  - [ ] `NormalizedWhatsApp`
  - [ ] `IsActive`
  - [ ] `DeletedAt`
  - [ ] `DeletedBy`
- [ ] Unique indexes exist for:
  - [ ] Patient number
  - [ ] Normalized phone
  - [ ] Normalized WhatsApp
- [ ] Existing duplicate patients are identified before adding unique constraints.

## 4. Patients Module

### Add/Edit

- [ ] Add new patient works.
- [ ] Edit patient works.
- [ ] Required validation works.
- [ ] Arabic error messages appear clearly.

### Duplicate Prevention

These numbers must be detected as the same number:

- `770245745`
- `0770245745`
- `+967770245745`
- `00967770245745`
- `٧٧٠٢٤٥٧٤٥`

Checklist:

- [ ] Adding duplicate phone fails.
- [ ] Adding duplicate WhatsApp fails.
- [ ] Editing a patient to another patient’s phone fails.
- [ ] Duplicate warning shows existing patient name and file number.

### Archive / Restore

- [ ] Archive patient works.
- [ ] Restore patient works.
- [ ] Archived patient disappears from active list.
- [ ] Archived patient appears in archived filter.
- [ ] All filter shows active and archived.

### Actions

- [ ] Row actions menu opens.
- [ ] Right-click context menu opens.
- [ ] Open file works.
- [ ] Edit works.
- [ ] Create appointment works.
- [ ] Add payment works.
- [ ] Open ortho case works.
- [ ] Internal message works.
- [ ] WhatsApp link works.
- [ ] Print summary works or is hidden until implemented.

## 5. Patient File

- [ ] Patient profile opens.
- [ ] Basic data appears correctly.
- [ ] Medical history appears.
- [ ] Dental history appears.
- [ ] Summary values load.
- [ ] Timeline loads.
- [ ] Patient archived status appears clearly.

## 6. Messaging

- [ ] Messages page opens.
- [ ] Conversations list loads.
- [ ] Unread count works.
- [ ] Send staff-to-staff message works.
- [ ] Open `/messages?patientId=...` works.
- [ ] Patient-linked conversation does not require a patient portal account.
- [ ] Message is linked to PatientId.
- [ ] Message remains after refresh.
- [ ] Clear Arabic error message appears when sending fails.

## 7. Appointments

- [ ] Appointment list opens.
- [ ] Daily view works.
- [ ] Weekly view works.
- [ ] Monthly view works.
- [ ] Doctor filter works.
- [ ] Create appointment works.
- [ ] Edit appointment works.
- [ ] Conflict detection works.
- [ ] Update appointment status works.
- [ ] Patient appointment history link works.

## 8. Finance Basics

- [ ] Finance page opens.
- [ ] Contracts list loads.
- [ ] Create contract works.
- [ ] Payments list loads.
- [ ] Create payment works.
- [ ] Finance summary loads.
- [ ] Overdue list loads.
- [ ] Patient-linked payment from patient actions works.

## 9. Required Fixes if Broken

If any item fails:

1. Fix the smallest possible scope.
2. Do not redesign the whole module.
3. Do not remove working features.
4. Add migration if schema changes are needed.
5. Add clear Arabic error messages.
6. Retest the exact failed case.

## 10. PR Report Template

When opening the PR, include:

```md
## Summary

## Tested
- [ ] Frontend build
- [ ] Backend build
- [ ] Patients
- [ ] Messaging
- [ ] Appointments
- [ ] Finance basics

## Bugs Found

## Bugs Fixed

## Files Changed

## Migrations

## Remaining Issues
```
