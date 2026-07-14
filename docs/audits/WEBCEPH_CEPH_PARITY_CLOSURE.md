# WebCeph Functional Parity Closure

Date: 2026-07-14

## Decision

The Aqlan Dental Pro cephalometry workspace now covers every workflow in the
binding WebCeph functional matrix with Aqlan-owned Arabic RTL interfaces,
canonical orthodontic data owners, doctor approval gates, and documented
clinical limits. This is functional parity, not a copy of WebCeph branding,
artwork, proprietary layout, or undocumented algorithms.

## Final Gap Closed

The lateral and frontal photo-analysis rows now share one navigation model and
support chronological before/after comparison at
`/ceph/photo/compare`. The comparison:

- selects two persisted analyses of the same case and view type;
- enforces an earlier `before` date and a later `after` date;
- compares the two saved images with a keyboard-operable divider;
- calculates deltas only from saved finite measurement values;
- never creates an intermediate frame or predicts growth/treatment response.

## Verification Evidence

- Functional matrix: every row is `Complete`.
- Privacy/security: `OrthoAccess` remains on ceph/photo controllers; list,
  detail, exports, case review, Timelapse, and cohort retain patient-access
  guards. Cohorts remain aggregate-only and suppressed below five independent
  patients.
- Clinical truthfulness: AI remains draft-only; approval and clean saved-state
  gates remain authoritative; PA, VTO, Timelapse, comparison, and cohort do not
  fabricate clinical data.
- Accessibility/responsive: desktop 1440x1000 and mobile 390x844 visual checks
  show no document overflow; the comparison has semantic headings, labelled
  selects, named links/buttons, and a keyboard-operable range control.
- Local verification: 377/377 frontend tests and 2365/2365 backend unit tests
  pass; TypeScript and the Next.js production build pass; Playwright discovers
  the authenticated ceph runtime scenario. The full backend run also exposed
  and fixed a pre-existing UTC/local-date test boundary at Aden midnight.
- PR #688 passed Backend, Frontend, E2E, Encoding Guard, and Vercel checks and
  was merged on 2026-07-14.

## Runtime Scenario

`frontend/playwright-tests/staging-smoke.spec.ts` always verifies the deployed
public login surface when `E2E_API_URL` is configured.
`frontend/playwright-tests/ceph-runtime.spec.ts` logs in with environment-held
staff credentials and verifies authenticated ceph routes and eligible exports
only when dedicated `E2E_STAFF_PHONE` and `E2E_STAFF_PASSWORD` secrets are
available. CI reports an explicit skip when those credentials are absent; that
skip is not represented as authenticated clinical-workflow evidence.

## Expanded Scope After Closure

After PR #688 closed the original functional-workflow matrix, the owner added
AI landmark accuracy and WebCeph account migration requirements under `SEQ-52`.
The original closure remains valid, while the new rows are tracked separately:

- account-owned Landmark Table import is implemented with calibration,
  provenance, preservation of extra points, and mandatory doctor review;
- AI quality is measured from saved doctor corrections, but WebCeph-equivalent
  accuracy is not claimed without a labelled reference benchmark;
- patient, record, and image synchronization awaits a WebCeph partner agreement,
  Premium-or-higher account, server-held partner key, and final official API
  contract; landmarks and clinical analysis data remain export-only.

## Residual Clinical Boundary

The software remains a doctor-reviewed clinical support workspace. It does not
replace orthodontist interpretation, does not automatically accept AI output,
and does not claim biological prediction. Changes to clinical definitions,
norms, or approval rules require the same specification, deterministic-test,
and clinical-review process used by SEQ-43 through SEQ-51.
