# Aqlan Dental Pro Mobile V1 — Execution Plan

This document tracks the long-lived mobile implementation branch `feat/mobile-v1-complete`.

## Merge policy

- Do not merge partial mobile slices into `main`.
- Keep PR #832 Draft / DO NOT MERGE while development is active.
- Reuse the existing ASP.NET Core backend and PostgreSQL database; do not create a parallel backend.
- Periodically inspect `main` for Claude Code changes that affect API contracts, permissions, schema, or shared behavior.
- Before final merge: synchronize the then-current `main` into this branch, resolve drift, run full CI + mobile bundles + APK build, review open threads, then merge once.

## Completed foundations

- [x] MOBILE-01 native Expo/React Native shell, secure native auth, token refresh/rotation, RTL.
- [x] MOBILE-02 patients create/search/detail + appointment booking.
- [x] MOBILE-03 messaging, notifications, patient access hardening, attachment rendering.
- [x] MOBILE-04 clinical visits/history create/edit/detail.

## Active build sequence

- [x] MOBILE-05 Daily Operations / Patient Journey
  - [x] Today journey list and filters.
  - [x] Confirm appointment.
  - [x] Intake / arrival.
  - [x] Send to queue.
  - [x] Call patient / enter room.
  - [x] Start visit.
  - [x] Clinical handoff to reception.
  - [x] Checkout workflow action.
  - [x] Patient daily journey summary (medical alerts, finance, ortho, timeline).
  - [x] Final CI/API behavior verification.
- [x] MOBILE-06 Finance basics: patient balances, statements, invoices, payments/receipts, cashier-safe actions.
- [x] MOBILE-07 Advanced appointments: daily views, status workflow, recall, reminders.
- [x] MOBILE-08 Orthodontics workspace.
- [x] MOBILE-09 General dentistry + FDI chart.
- [x] MOBILE-10 Surgery / orthognathic workspace.
- [x] MOBILE-11 Photos, radiographs, documents, prescriptions, referrals.
- [x] MOBILE-12 Lab orders / labs / status / shades.
- [x] MOBILE-13 Inventory operational views.
- [x] MOBILE-14 Reports and management dashboard.
- [x] MOBILE-15 Settings, permissions-aware navigation, account/security polish.
- [ ] MOBILE-16 Offline/error resilience, performance, accessibility, device testing.
  - [x] Safe network timeout and user-facing connection errors.
  - [x] Shared keyboard and accessibility improvements.
  - [x] On-device release, API health, session and permission diagnostics.
  - [x] Standalone Android Release APK with embedded JavaScript verification.
  - [ ] Physical-device acceptance testing and feedback fixes.
- [ ] MOBILE-17 Final main synchronization + regression review + release approval.
  - [x] Current `main` is already contained in the mobile branch.
  - [x] Full CI, mobile bundles and standalone APK build are green.
  - [ ] Resolve findings from physical-device testing.
  - [ ] Final owner review and explicit merge approval.

## MOBILE-18 — Stability and official brand rebuild (0.2.0)

- **Spec:** `specs/012-mobile-client/` (`MOBILE-REQ-003..010`).
- [x] Official clinic logo, navy/blue/orange identity, adaptive app icon and branded splash.
- [x] `GestureHandlerRootView` at the application root to harden Android navigation/taps.
- [x] Arabic render error boundary with safe recovery instead of a silent close.
- [x] Rebuilt sign-in, loading shell, home, tabs, patients, appointments, messages, account and diagnostics.
- [x] Shared cards, fields, buttons, spacing, shadows and accessibility states applied across existing workspaces.
- [x] Stale patient/message/appointment requests are aborted during search/filter changes.
- [x] TypeScript, Android export and iOS export pass for version 0.2.0.
- [ ] Standalone 0.2.0 APK build with embedded bundle verification.
- [ ] Physical-device tap/navigation acceptance — `Needs runtime verification`.

## Release gates

- TypeScript clean.
- Expo Doctor clean.
- Android bundle clean.
- iOS bundle clean.
- Installable Android APK build clean.
- Backend/frontend CI regression green.
- Encoding Guard green.
- Vercel preview green where applicable.
- No unresolved critical review threads.
- Latest `main` reconciled immediately before final merge.
