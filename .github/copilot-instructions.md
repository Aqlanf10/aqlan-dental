# GitHub Agent Instructions — Aqlan Dental Pro

## Read First

Before making any code change, read:

1. `AGENT_START_HERE.md`
2. `docs/AGENT_INSTRUCTIONS.md`
3. `docs/SPRINT_1_STABILIZATION.md`
4. `docs/ROADMAP.md`

## Main Rule

Continue the existing project. Do not rebuild from scratch.

## Current Stack

- Frontend: Next.js 14 + TypeScript + Tailwind CSS
- Backend: ASP.NET Core Web API .NET 8
- Database: PostgreSQL
- Deployment: Vercel + Railway
- UI: Arabic RTL

## Required Behavior

- Preserve existing architecture.
- Preserve existing modules.
- Use EF Core migrations for schema changes.
- No dummy data in production.
- No non-working buttons.
- No hard delete for clinical/financial data.
- All errors shown to users must be clear Arabic messages.
- AI simulation must be labeled as simulation, not real AI.

## Current Task Priority

Work only on Sprint 1 stabilization first:

1. Deployment verification.
2. Build verification.
3. Database migration verification.
4. Patients module testing and fixes.
5. Patient-linked messaging fix.
6. Appointments testing and fixes.
7. Finance basics testing and fixes.

Do not start HR, AI, new ceph module, or full redesign until Sprint 1 is complete.
