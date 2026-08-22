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

- [ ] MOBILE-05 Daily Operations / Patient Journey
  - [x] Today journey list and filters.
  - [x] Confirm appointment.
  - [x] Intake / arrival.
  - [x] Send to queue.
  - [x] Call patient / enter room.
  - [x] Start visit.
  - [x] Clinical handoff to reception.
  - [x] Checkout workflow action.
  - [x] Patient daily journey summary (medical alerts, finance, ortho, timeline).
  - [ ] Final CI/API behavior verification.
- [ ] MOBILE-06 Finance basics: patient balances, statements, invoices, payments/receipts, cashier-safe actions.
- [ ] MOBILE-07 Advanced appointments: daily/weekly views, status workflow, recall, reminders.
- [ ] MOBILE-08 Orthodontics workspace.
- [ ] MOBILE-09 General dentistry + FDI chart.
- [ ] MOBILE-10 Surgery / orthognathic workspace.
- [ ] MOBILE-11 Photos, radiographs, documents, prescriptions, referrals.
- [ ] MOBILE-12 Lab orders / labs / status / shades.
- [ ] MOBILE-13 Inventory operational views.
- [ ] MOBILE-14 Reports and management dashboard.
- [ ] MOBILE-15 Settings, permissions-aware navigation, account/security polish.
- [ ] MOBILE-16 Offline/error resilience, performance, accessibility, device testing.
- [ ] MOBILE-17 Final main synchronization + regression review + release APK.

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
