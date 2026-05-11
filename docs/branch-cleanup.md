# Branch Cleanup Guide

## Overview

Over the course of development, many feature and fix branches accumulate in the repository. After they are merged into `main`, these branches become stale and should be cleaned up to keep the repository organized.

## Quick Start

```bash
# List all merged branches (safe, read-only)
./scripts/list-merged-branches.sh

# Delete all merged branches (interactive confirmation)
./scripts/list-merged-branches.sh --delete
```

## Protected Branches

The following branch patterns are **never** deleted by the cleanup script:

- `main` — the production branch
- `HEAD` — symbolic reference
- `stable-*` — stable release branches
- `release-*` — release preparation branches

## Current Merged Branches

As of Sprint 6, the following remote branches have been merged into `main` and can be safely deleted:

| Branch | Description |
|--------|-------------|
| `feat/sprint-2-unified-patient-file` | Sprint 2 patient file |
| `feat/sprint-3-patient-linked-messaging` | Sprint 3 messaging |
| `feat/sprint-4-appointments-visits-core` | Sprint 4 appointments |
| `feat/sprint-4-visits-documents` | Sprint 4 documents |
| `feat/sprint-5-appointments-enhancement` | Sprint 5 enhancements |
| `feat/basic-message-notifications` | Message notifications |
| `feat/message-attachments` | Message attachments |
| `feat/messaging-portal-ux-badges` | UX badges |
| `feat/patient-message-recipient-selection` | Recipient selection |
| `feat/patient-portal-completion` | Portal completion |
| `feat/portal-messaging` | Portal messaging |
| `feat/public-booking-availability-slots` | Booking availability |
| `feat/public-website-appointment-requests` | Appointment requests |
| `fix/doctorid-column-hotfix` | DoctorId hotfix |
| `fix/integrate-patient-portal-messaging-ui` | Portal UI integration |
| `fix/middleware-allow-portal-auth` | Portal auth fix |
| `fix/patient-message-recipient-stabilization` | Recipient stabilization |
| `fix/patient-portal-auth-redirect-loop` | Auth redirect loop fix |
| `fix/patient-portal-messaging` | Portal messaging fix |
| `fix/patient-portal-stabilization` | Portal stabilization |
| `fix/patient-portal-stabilization-v2` | Portal stabilization v2 |
| `fix/portal-message-read-500` | Message read 500 fix |
| `fix/prevent-same-day-duplicate-booking-requests` | Duplicate booking fix |
| `fix/production-admin-access` | Admin access fix |
| `fix/public-homepage-routing` | Homepage routing fix |
| `fix/restore-branch-seeding-order` | Seeding order fix |
| `fix/sprint-1-stabilization` | Sprint 1 stabilization |
| `fix/voice-recorder-visibility-and-audio-types` | Voice recorder fix |
| `hotfix/patient-history-upsert` | Patient history hotfix |
| `hotfix/patient-portal-auth-db-schema` | Portal auth schema hotfix |
| `hotfix/patient-portal-migration` | Portal migration hotfix |
| `hotfix/patient-portal-migration-v2` | Portal migration v2 hotfix |
| `hotfix/secure-patient-portal-password-reset` | Portal password reset |
| `polish/public-website-final-branding` | Website branding |
| `polish/public-website-visual-design` | Visual design |
| `revert/public-doctors-services-pages` | Doctors revert |
| `style/match-approved-design-reference` | Design reference |
| `style/restore-approved-design-system` | Design system |
| `ux/two-panel-staff-patient-login` | Login UX |

## Best Practices

1. **Always fetch with prune** before listing branches: `git fetch --all --prune`
2. **Review the list** before deleting — never blindly delete branches
3. **Keep active branches** — if a branch is still being worked on, don't delete it
4. **Delete local branches** separately: `git branch -d <branch-name>`
5. **Run the script** after each sprint merge to keep the repo clean
