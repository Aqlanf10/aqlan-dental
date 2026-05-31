# Sprint 8A — Branch Inventory Review

Date: 2026-05-12
Scope: Review-only branch inventory. No branches are deleted by this document or PR.

## Current Count

Remote branches found: **73**

This is too many for a production repository because it increases agent confusion, stale PR risk, and the chance of work starting from an obsolete branch.

## Hard Safety Rules

Never delete automatically:

- `main`
- `stable-*`
- `release-*`
- recovery/backup branches unless explicitly reviewed by the owner
- any branch with an open PR
- any branch that is currently being used for active work

## Keep / Protect

These branches should be kept unless Dr. Aqlan explicitly decides otherwise:

| Branch | Reason |
|---|---|
| `main` | Production branch |
| `stable-after-recovery-2026-05-06` | Recovery/stable reference |
| `backup-polluted-main-2026-05-06` | Recovery/backup branch; keep until no longer needed |
| `docs/sprint-8a-branch-inventory-review` | Current documentation branch |

## Needs Owner Review Before Any Deletion

These may contain unmerged, experimental, or unclear work. Review before deletion:

| Branch | Notes |
|---|---|
| `chore/persistent-upload-storage` | Potentially relevant to uploads/storage |
| `claude/aqlan-dental-pro-setup-vZwJo` | Agent-generated setup branch |
| `claude/complete-program-build-OHuID` | Agent-generated broad build branch; high risk to merge blindly |
| `docs/add-currency-language-sprint` | May contain useful roadmap docs |
| `docs/add-design-system-sprint` | May contain useful roadmap docs |
| `docs/add-public-website-booking-sprint` | May contain useful roadmap docs |
| `docs/update-roadmap` | May contain roadmap history |
| `feat/sprint-4.5-clinic-queue` | Older queue work; compare with merged Sprint 7 queue first |
| `feature/finance-contracts-payments-core` | Important future module; inspect before deletion |
| `feature/photos-radiographs-documents-core` | Important clinical module; inspect before deletion |
| `feature/reports-printing-core` | Important reporting module; inspect before deletion |
| `feature/visits-treatments-core` | Important clinical module; inspect before deletion |
| `feature/public-doctors-services-pages` | There is also a revert branch; review carefully |

## Likely Safe Deletion Candidates After Verification

These appear to be old feature/fix/polish branches that likely correspond to merged or superseded work. They should still be verified against merged PRs before deletion.

| Branch |
|---|
| `chore/critical-stability-cleanup` |
| `chore/post-pr69-stability-check` |
| `chore/sprint-7b-clinic-queue-polish` |
| `chore/sprint-8a-production-stability-cleanup` |
| `feat/basic-message-notifications` |
| `feat/message-attachments` |
| `feat/messaging-portal-ux-badges` |
| `feat/patient-message-recipient-selection` |
| `feat/patient-portal-completion` |
| `feat/portal-messaging` |
| `feat/public-booking-availability-slots` |
| `feat/public-website-appointment-requests` |
| `feat/sprint-2-unified-patient-file` |
| `feat/sprint-3-patient-linked-messaging` |
| `feat/sprint-4-appointments-visits-core` |
| `feat/sprint-4-visits-documents` |
| `feat/sprint-5-appointments-enhancement` |
| `feat/sprint-7-clinic-queue-rooms` |
| `feat/sprint-7-complete-clinic-queue` |
| `feature/branches-doctors-schedules` |
| `feature/patient-clinical-file-core` |
| `feature/public-booking-finalization` |
| `fix/auditlog-patient-portal-fk` |
| `fix/block-portal-jwt-from-staff-endpoints` |
| `fix/booking-request-reopen-actions` |
| `fix/clinic-display-announce-file-number` |
| `fix/clinic-display-right-click-replay` |
| `fix/clinic-display-voice-calling` |
| `fix/doctorid-column-hotfix` |
| `fix/improve-clinic-display-voice-replay` |
| `fix/integrate-patient-portal-messaging-ui` |
| `fix/middleware-allow-portal-auth` |
| `fix/patient-message-recipient-stabilization` |
| `fix/patient-portal-auth-redirect-loop` |
| `fix/patient-portal-messaging` |
| `fix/patient-portal-stabilization` |
| `fix/patient-portal-stabilization-v2` |
| `fix/portal-message-read-500` |
| `fix/prevent-same-day-duplicate-booking-requests` |
| `fix/production-admin-access` |
| `fix/public-clinic-display-access` |
| `fix/public-homepage-routing` |
| `fix/restore-branch-seeding-order` |
| `fix/sprint-1-stabilization` |
| `fix/voice-recorder-visibility-and-audio-types` |
| `hotfix/patient-history-upsert` |
| `hotfix/patient-portal-auth-db-schema` |
| `hotfix/patient-portal-migration` |
| `hotfix/patient-portal-migration-v2` |
| `hotfix/secure-patient-portal-password-reset` |
| `polish/public-website-final-branding` |
| `polish/public-website-visual-design` |
| `revert/public-doctors-services-pages` |
| `style/match-approved-design-reference` |
| `style/restore-approved-design-system` |
| `ux/two-panel-staff-patient-login` |

## Recommended Cleanup Process

1. Keep this PR review-only.
2. After merge, manually inspect each candidate branch against closed/merged PRs.
3. Delete in small batches, not all at once.
4. Never delete recovery/stable branches during the first cleanup pass.
5. After cleanup, run another branch inventory and confirm the reduced count.

## Recommended First Deletion Batch

Start only with branches that are directly tied to recently merged PRs and clearly superseded:

- `fix/clinic-display-announce-file-number` — merged as PR #81
- `fix/public-clinic-display-access` — merged as PR #80
- `fix/clinic-display-right-click-replay` — merged as PR #79
- `fix/improve-clinic-display-voice-replay` — merged as PR #78
- `fix/clinic-display-voice-calling` — superseded and PR #77 closed stale
- `chore/sprint-8a-production-stability-cleanup` — merged as PR #82

No deletion should happen inside this documentation PR.
