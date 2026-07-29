# Repository Cleanup Report Before SpecKit Governance

Date: 2026-07-02  
Branch: `chore/repository-cleanup-before-speckit`  
Scope: cleanup-only. No runtime feature work was performed.

## Branches Deleted

The following remote branches were deleted after `git fetch origin --prune` and after verifying they were fully merged into `origin/main` and did not have open pull requests:

- `chore/docs-cleanup-20260702`
- `feat/c-08-startup-maintenance-audit`
- `feat/clin-05-unify-ortho-visit`
- `feat/clin-10-ceph-norms-age-gender`
- `feat/fe-06-merge-patient-screens`
- `feat/fin-13-finance-reports-sql-aggregations`
- `feat/sec-09-hash-portal-refresh-tokens`
- `feature/ceph-10-10-ux-readiness`
- `feature/ceph-pdf-quality-link`
- `feature/ceph-quality-checklist`
- `fix/ortho-wizard-case-draft-clean-01`

Post-cleanup verification:

- `git branch -r --merged origin/main` returns only `origin/HEAD -> origin/main` and `origin/main`.
- No branch with an open PR was deleted.

## Branches Kept And Why

Protected / canonical:

- `main` was kept because it is the protected base branch.

Open PR branches:

- `feature/ortho-wizard-case-endpoint` — kept because PR #407 is open.
- `codex/ortho-p4-professional-treatment-plan` — kept because PR #363 is open.

Not fully merged into `main` or uncertain active work:

- `chore/repository-cleanup-2026-07-02`
- `claude/c15-appointment-race`
- `claude/fe18-finance-reception-access`
- `claude/finance-dynamic-permissions`
- `claude/finance-exception-leak`
- `claude/finance-vouchers-identity`
- `claude/sec-c12-leave-authorize`
- `claude/wizardly-carson-qys671`
- `cleanup/dead-code-modals-split`
- `cleanup/sms-messages-split`
- `codex/finance-multicurrency-vouchers`
- `codex/finance-period-comparison`
- `codex/fix-finance-migration-discovery`
- `codex/fix-finance-migration-metadata-arabic`
- `codex/ortho-p4-professional-treatment-plan`
- `codex/ortho-surgical-inside-ortho`
- `codex/video-inspired-clinic-polish`
- `docs/ortho-surgical-a9-a11-handoff`
- `docs/ortho-surgical-workspace-plan`
- `docs/ortho-wizard-ai-clinical-qa`
- `docs/reference-video-ux-report`
- `feat/ai-ceph-improvements`
- `feat/clin-12-async-pdf-file-io`
- `feat/clin-15-ortho-overview-projections`
- `feat/clin-23-24-prescription-referral-ownership`
- `feat/collections-filters-currency`
- `feat/db-02-financial-xmin-tokens`
- `feat/doctor-room-assignments`
- `feat/extract-patient-journey-service`
- `feat/fe-15-loading-error-boundaries`
- `feat/fe-22-patient-combobox-adoption`
- `feat/fe-30-react-hook-form-adoption`
- `feat/fe15-loading-error-boundaries`
- `feat/finance-currency-totals`
- `feat/integration-tests`
- `feat/multi-currency-payments`
- `feat/ortho-surgical-a1-backend`
- `feat/ortho-surgical-a2-frontend`
- `feat/ortho-surgical-a3-readiness`
- `feat/ortho-surgical-a4-collaboration`
- `feat/ortho-surgical-a5-joint-plan`
- `feat/ortho-surgical-a6-surgery-summary`
- `feat/ortho-surgical-a7-reports`
- `feat/ortho-surgical-a8-ai-assistant`
- `feat/ortho-surgical-a9-vto`
- `feat/sec-10-portal-refresh-cookie`
- `feat/sec-11-password-complexity`
- `feat/sec-docs-body-ownership-check`
- `feat/test-02-surgery-controller-tests`
- `feat/test-12-visits-controller-tests`
- `feat/test-13-documents-uploads-comms-tests`
- `feat/test-16-playwright-ci`
- `feat/test-19-coverage-thresholds`
- `feature/ceph-analysis-quality`
- `feature/ceph-link-quality`
- `feature/ceph-quality-actions`
- `feature/ceph-vto-quality-link`
- `feature/ortho-case-ai-endpoint`
- `feature/ortho-case-level-ai-clinical-draft`
- `feature/ortho-wizard-case-endpoint`
- `feature/unified-orthodontics-workspace`
- `finance/perms-group-a-invoices-contracts`
- `finance/perms-group-b-expenses-suppliers`
- `fix/audit-followup-c03-c07-c12-sec23-fin11`
- `fix/batch-clin30-fe31-fe34-fin22`
- `fix/batch-fin15-fin18-clin31`
- `fix/batch-sec24-sec25-clin21-clin19-fe36`
- `fix/batch2-fe36-fe37-fin23-fin24`
- `fix/clin-01-patient-access-filter`
- `fix/clin-01b-photos-radiographs-access`
- `fix/clin-01c-visits-referrals-access`
- `fix/clin-01d-documents-treatmentplan-access`
- `fix/clin-01e-laborders-surgery-access`
- `fix/clin-01f-general-invoices-payments-access`
- `fix/clin-03-surgery-status-transitions`
- `fix/clin-10-ceph-age-sex-norms`
- `fix/clin-12-async-pdf-file-io`
- `fix/clin-16-treatment-step-transitions`
- `fix/clin-17-forbid-string-fix`
- `fix/clin-19-legacy-endpoint-cleanup`
- `fix/clin-20-lab-overdue-notifications`
- `fix/clin-23-24-25-existence-validation`
- `fix/clin-32-ai-log-analysis-id-fix`
- `fix/clin12-flaky-pdf-byte-test`
- `fix/dashboard-treasury-yer-only`
- `fix/db-03-cashier-session-unique-index`
- `fix/db-07-remove-duplicate-patient-index`
- `fix/db-09-labpayable-cascade-restrict`
- `fix/db-treasury-xmin-migration`
- `fix/dockerfile-include-integrationtests`
- `fix/fe-01-17-35-delete-mockup-queue-commissions`
- `fix/fe-02-route-permissions-default-deny`
- `fix/fe-03-18-sidebar-route-reconciliation`
- `fix/fe-05-16-api-upload-download-helpers`
- `fix/fe-07-unified-payment-modal`
- `fix/fe-08-lab-status-centralize`
- `fix/fe-09-centralize-appointment-status`
- `fix/fe-10-patient-journey-shared-helpers`
- `fix/fe-11-centralize-error-helper`
- `fix/fe-13-use-doctors-hook`
- `fix/fe-13b-use-doctors-hook-batch2`
- `fix/fe-13c-use-doctors-batch3`
- `fix/fe-14-laborder-panel-misleading-message`
- `fix/fe-15-loading-error-boundaries`
- `fix/fe-27-standardize-print-pdf`
- `fix/fe-29-toaster-rtl-position`
- `fix/fe-36-remove-redundant-dir-rtl`
- `fix/fin-01-02-cashier-close-reconcile-tx`
- `fix/fin-03-cashier-closing-manager-approval`
- `fix/fin-04-invoice-update-transaction`
- `fix/fin-05-vault-transfer-source-balance-tx`
- `fix/fin-06-treasury-xmin-concurrency-token`
- `fix/fin-09-commission-recognition-protection`
- `fix/fin-10-earned-from-collections-date-filter`
- `fix/fin-12-unify-patient-balance`
- `fix/fin-21-audit-log-details`
- `fix/fin13-n1-query-optimization`
- `fix/fin16-clin07-clinic-timezone`
- `fix/finance-multicurrency-runtime`
- `fix/flaky-ortho-presentation-test`
- `fix/foreign-payment-applied-amount`
- `fix/nav-cleanup-and-ceph-image-load`
- `fix/ortho-surgical-oralsurgeon-route-access`
- `fix/ortho-surgical-surgery-side-integration`
- `fix/overdue-job-clinic-timezone`
- `fix/patient-access-ortho-surgical-link`
- `fix/patient-create-ispregnant-enum`
- `fix/patient-form-submit-diagnostic`
- `fix/phase3-clin08-fin14-fin20-clin06`
- `fix/phase3b-clin09-fin11`
- `fix/sec-01-password-hardening`
- `fix/sec-03-uploads-behind-auth`
- `fix/sec-05-staffonly-hr-controllers`
- `fix/sec-06-07-recaptcha-whatsapp-enforce`
- `fix/sec-08-encrypt-backups`
- `fix/sec-14-reject-known-bad-jwt-defaults`
- `fix/sec-docs-body-ownership-check`
- `fix/sec-route-id-ownership-bypass`
- `fix/security-loading-fixes`
- `fix/surg-status-snake-case-parse`
- `product/ceph-cb-cd-complete`
- `product/daily-ops-status-cards`
- `product/finance-settings-configuration`
- `product/signalr-realtime-journey`
- `product/sprint3-ortho-improvements`
- `product/sprint4-cephalo-webceph`
- `refactor/ortho-query`
- `refactor/split-ortho-page`
- `sprint0/audit-execution-checklist`
- `sprint1/treasury-concurrency`
- `sprint10/design-system`
- `sprint11a/settings-split`
- `sprint11b/daily-ops-split`
- `sprint12/backend-cleanup`
- `sprint13-14/api-unify-refund-commission`
- `sprint15-16-17/ortho-finance-followup-rtl`
- `sprint18-19/techdebt-final-report`
- `sprint2/negative-balance-block`
- `sprint3/finance-permissions`
- `sprint4/exmessage-leaks`
- `sprint5/csp-hardening`
- `sprint6/ceph-approval`
- `sprint7/ceph-quality`
- `sprint8/ortho-ceph-tests`
- `sprint9/sidebar-cleanup`
- `yolo-s1/appointment-enhancements`
- `yolo-s2/services`
- `yolo-s3/settings-hub`
- `yolo-s4-s5/inventory-segments`

Reason: these branches were not returned by `git branch -r --merged origin/main`, or are known open-PR branches. They were left untouched.

## Docs Deleted

None.

No documentation file was deleted because the current repository is about to enter SpecKit governance and the audit/report files may still be useful as historical evidence or active planning context.

## Docs Kept And Why

The following candidate audit/report files were inspected by name and kept:

- `docs/branch-cleanup.md` — branch cleanup history; keep until owner confirms it is obsolete.
- `docs/ORIGINAL_PLAN.md` — historical plan; may be referenced during governance.
- `docs/SPRINT_PATIENT_PORTAL_AUTH_COMPLETED.md` — sprint completion record.
- `docs/SPRINT_1_STABILIZATION.md` — sprint stabilization record.
- `docs/sprint-8a-production-stability-audit.md` — production stability audit.
- `docs/sprint-8a-branch-inventory-review.md` — branch inventory review.
- `docs/audits/SPRINT_0_REAL_USER_PRODUCTION_AUDIT.md` — production audit evidence.
- `docs/audits/SPRINT_1_CRITICAL_PRODUCTION_FIXES.md` — production fix evidence.
- `docs/replit/REPLIT_PRODUCTION_SMOKE_QA_AUDIT.md` — smoke QA audit evidence.
- `docs/agent-audit/*` — agent audit reports and roadmap files; likely old but require owner confirmation before deletion.
- `docs/ortho-module/*PLAN*.md`, `*HANDOFF*.md`, `*STATUS*.md` — active module planning/governance context; not deleted.
- `docs/technical-debt/FINANCE-PROGRAM-CLEANUP.md` — technical-debt cleanup record; keep for governance baseline.

## Risks

- Some kept audit files may be obsolete, but deleting them before SpecKit governance could remove useful historical context.
- Many unmerged remote branches remain. They may include stale work, but they are not safely deletable under the cleanup rules because they are not fully merged into `main`.
- A separate remote branch named `chore/repository-cleanup-2026-07-02` exists and was not merged; it was kept as potentially active/uncertain work.

## Items Needing Owner Confirmation

- Whether old audit bundles under `docs/agent-audit/` can be archived or deleted.
- Whether `docs/audits/SPRINT_0_REAL_USER_PRODUCTION_AUDIT.md` and `docs/audits/SPRINT_1_CRITICAL_PRODUCTION_FIXES.md` should remain as governance evidence.
- Whether sprint history files at the root of `docs/` should be moved into an archive folder instead of deleted.
- Whether old unmerged branch groups (`sprint*`, `yolo-*`, `feat/*`, `fix/*`) are truly abandoned and can be closed/deleted manually later.

## Safety Confirmations

- No runtime code changed.
- No database migrations changed.
- No package files changed.
- No secrets, environment files, Railway, Vercel, or GitHub Actions files were touched.
- No production deployment, migration, or environment mutation was performed.

## Next Step

After this PR is merged, run the SpecKit governance prompt to define the permanent repository governance and archival policy.
