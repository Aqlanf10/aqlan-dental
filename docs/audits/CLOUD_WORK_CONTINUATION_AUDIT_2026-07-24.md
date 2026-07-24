# Cloud Work Continuation Audit — 2026-07-24

- Auditor session: Cowork continuation (GitHub-as-source-of-truth)
- Repository: `Aqlanf10/aqlan-dental`
- `main` HEAD at audit: `93c2985f9394a898793a3612c4cab8ee1270163e`
- Last merged PR on `main`: **#718** (`recovery/audit-012-opening-balances`, 2026-07-22)
- Governing queue: `docs/governance/MANDATORY_SPRINT_QUEUE.md` (active gate: **CORE-00 — freeze cephalometry, stabilize the core system baseline**)
- Supersedes/extends: `docs/roadmap/core-system-current-state.md` (baseline 2026-07-17, `73a8c3e4`)

> This document is a continuation audit. It re-verifies the 2026-07-17 Phase 0
> baseline against the code actually on `main` today, records what changed since,
> and re-selects the next actionable slice under the mandatory queue. No user file,
> repository copy, commit, or local change was deleted or moved during this audit.

---

## 1. الملخص التنفيذي (Arabic)

النظام على `main` يبني وتنجح مجموعات اختباراته الآلية (حسب آخر خط أساس موثّق)،
لكن **خضرة CI ليست دليلًا على أن رحلة المريض المصادَقة تعمل كاملة**. تطوير
السيفالومتري **مجمّد** بقرار المالك (CORE-00) وأعماله محفوظة في PRs #697/#698/#699.

منذ خط الأساس (2026-07-17) استمر Codex ونفّذ **سلسلة المالية متعددة العملات**
(PRs #711–#718: أرصدة افتتاحية، إقفال سنوي، تسوية الكاشير بالعملات، لقطات أسعار
الصرف، دفتر عملات المالية...) بالإضافة إلى ضبط فترات المالية والقيود اليدوية
(#704–#706) ومواءمة التنقّل (#701/#702/#707) ومعالجة تخصيص الدفعات (#710).
**آخر ما توقف عنده Codex** هو الأرصدة الافتتاحية (#718).

**أهم فجوة توثيقية:** ملفّا `core-system-current-state.md` و
`core-system-execution-checklist.md` بتاريخ 2026-07-17 ولم يُحدَّثا بعد سلسلة
المالية، فبعض بنودهما (خصوصًا CORE-F-007 المالي) صار **متأخرًا عن الكود**.

**أعلى المخاطر المفتوحة والمؤكَّدة في الكود الحالي:**
1. `CORE-F-001` (حرِج): عدم تطابق قفل الزيارة بين مسارَي الموعد والطابور + غياب فهرس فريد لزيارة نشطة → خطر تكرار زيارة/فاتورة.
2. `CORE-F-002` (حرِج): ازدواج ملكية المخطط بين هجرات EF و`StartupDatabaseMaintenance` — خطر نشر.
3. `CORE-F-003` (عالٍ): **تم إصلاحه في هذه الجلسة** — عقد إعادة ترتيب الطابور (الواجهة كانت ترسل `{orders}` والخادم يتوقع مصفوفة).
4. `CORE-F-005` (عالٍ): وجود `VIP` وتعارض سبب الطوارئ بين الواجهة والخادم.
5. `CORE-F-007` (عالٍ): تجميعات متعددة العملات — **جزئيًا معالَج** بسلسلة #711–#718، يحتاج إعادة تحقق.

**قيود البيئة:** `api.github.com` ومضيفات تنزيل .NET/Playwright محجوبة عبر البروكسي؛
البيئة المعزولة تقتل العمليات الطويلة (حد 45 ثانية للأمر). لذلك يتم التحقق النهائي
من البناء والاختبارات عبر **GitHub Actions CI** بعد الرفع، لا محليًا.

---

## 2. Current main state and open work

| Item | Value |
|---|---|
| `main` HEAD | `93c2985` (merge of PR #718) |
| Last merged PR | #718 opening balances (2026-07-22) |
| Remote branches | 40 total |
| Paused ceph PRs | #697/#698/#699 = branches `codex/seq-57/58/59-ceph-pilot-*` |
| Ceph drift from main | 34 commits behind (last ceph work 2026-07-16/17) |

### Unmerged / loose ends (branches ahead of `main`, product-relevant)

| Branch | Ahead | Behind | Last commit | Note |
|---|---:|---:|---|---|
| `recovery/audit-002-patient-journey` | 1 | 20 | 2026-07-21 `fix patient journey financial handoff` | **Not merged.** Touches `PatientJourneyController`, `CheckoutService`, `dailyOperationsRoute`. Needs review/rebase — see Track C. |
| `codex/seq-57/58/59-ceph-pilot-*` | 1/2/3 | 34 | 2026-07-16/17 | Paused ceph pilots (#697/#698/#699). Do **not** merge. |

Older `fix/seq-*` and `docs/seq-*` branches are far behind (120–154 commits) and
correspond to already-merged SEQ items; they carry only stale doc-completion commits
and are safe to leave as historical branches (no deletion performed).

---

## 3. Where Codex stopped (PR #700 → #718)

| PR | Source branch | Theme | Phase |
|---:|---|---|---:|
| #700 | (phase-0 baseline) | Phase 0 audit baseline, reports | 0 |
| #701 | reception appointment route | Reception `/appointments` alignment | 1 |
| #702 | canonical route/owner registry | Route ownership + redirect/role tests | 1 |
| #704 | `codex/finance-supplier-opening-balances` | Supplier opening balances | 9/10 |
| #705 | `codex/finance-manual-journals-periods` | Manual journals + periods | 9 |
| #706 | `codex/finance-period-controls` | Accounting period controls | 9 |
| #707 | `recovery/audit-001-navigation` | Navigation audit fixes | 1 |
| #710 | `recovery/audit-004-payment-allocation` | Payment allocation | 9 |
| #711 | `recovery/audit-005-advance-visibility` | Advance visibility | 9 |
| #712 | `recovery/audit-006-expense-vouchers` | Expense disbursement vouchers | 9/10 |
| #713 | `recovery/audit-007-finance-currency-ledger` | Per-currency patient balances | 9 |
| #714 | `recovery/audit-008-payment-fx-snapshots` | Immutable payment FX snapshots | 9 |
| #715 | `recovery/audit-009-cashier-multicurrency` | Cashier multi-currency | 9 |
| #716 | `recovery/audit-010-cashier-currency-reconciliation` | Cashier reconciliation by currency | 9 |
| #717 | `recovery/audit-011-year-end-close` | Auditable year-end close | 9 |
| #718 | `recovery/audit-012-opening-balances` | Opening balances into finance | 9 |

(#703, #708, #709 were squash/rebase-merged without merge commits; e.g. #709
"enforce payment branch session isolation" is present in history.)

**Interpretation:** After the Phase 0/1 navigation baseline, Codex executed a large
**multi-currency finance reconciliation program** (effectively Phase 9 work) ahead of
the documented Phase 1→9 order. The mandatory queue's SEQ list (00–51, 53, 54) is all
`✅`; ceph SEQ-52/55/56 are `⏸️`. The binding open gate is therefore **CORE-00**
(stabilization), and the next actionable non-ceph work is (a) closing the still-open
core findings and (b) reconciling the stale roadmap docs to the merged finance work.

---

## 4. Documentation vs code drift

| Document | Dated | Drift |
|---|---|---|
| `core-system-current-state.md` | 2026-07-17 | Finance section 12 predates PRs #711–#718; several listed gaps are now partially closed. Needs a currency-reconciliation re-verification pass. |
| `core-system-execution-checklist.md` | 2026-07-17 | Shows Phase 1 with S4/S5 pending and Phases 4/9 unchecked, but Phase 9 finance work merged (#711–#718) and patient-journey work partly landed. Checklist must be reconciled. |
| `core-system-priority-plan.md` | Phase 0 proposal | Phase 1 "merged work" lists only #701/#702; later merges not reflected. |

This drift is expected per the task brief and is itself a tracked deliverable
(Track A/B below): the checklist and plan are updated in the companion continuation
plan, not silently.

---

## 5. Severity-ranked defect register (re-verified 2026-07-24)

| ID | Sev | Finding | Verified today | Owning track |
|---|---|---|---|---|
| CORE-F-001 | Critical | Cross-route visit lock mismatch (appointment locks appointmentId, queue locks queueItemId); no unique filtered index on active `AppointmentId` in `Visits`; invoices non-unique on `VisitId`/`AppointmentId` | Code paths still present (needs concurrency repro) | C (incident-priority) |
| CORE-F-002 | Critical | EF migrations + `StartupDatabaseMaintenance.cs` (~5,330 lines raw DDL, rewrites `__EFMigrationsHistory`) share schema ownership; 93 migrations, 16 with `[Migration]`, duplicate prefix `20260604000000` | Structural, unchanged | A |
| CORE-F-003 | High | Queue reorder wire contract mismatch | **Confirmed live, FIXED this session** (branch `fix/core-f-003-queue-reorder-contract`) | ✅ B/queue |
| CORE-F-004 | High | Queue priority/reorder lack granular permission + full audit | Present | D |
| CORE-F-005 | High | `VIP` enum + emergency-reason mismatch (FE sends no reason; BE requires one) | Present | queue |
| CORE-F-006 | High | Reception appointment access differed across layers | Resolved by #701 | ✅ |
| CORE-F-007 | High | Mixed-currency aggregates/PDF labels | **Partially closed** by #711–#718; residual PDF hardcoded `r.y.` + notification-as-YER + journal-line currency remain to verify | E |
| CORE-F-008 | High | Identity/language/logo/print fragmented + partly hardcoded | Present | F |
| CORE-F-009 | High | Authenticated E2E can skip while CI green | Present (CI config) | A |
| CORE-F-010 | High | Backend coverage floor low + non-blocking (8.28% lines) | Present | A (cross) |
| CORE-F-011 | Medium | Patient duplicate lacks identity-number + reviewed merge | Present | (Phase 4) |
| CORE-F-012 | Medium | Lab missing target states/escalation proof | Present | G |

---

## 6. Automated verification baseline (this session)

| Check | Result | Notes |
|---|---|---|
| Repo clone integrity | Pass | Fresh clone, `main` == `origin/main` == `93c2985`, tree clean |
| Frontend `npm ci` (local) | **Blocked** | Sandbox kills processes >45s; install not completed locally |
| Backend `dotnet` (local) | **Blocked** | .NET SDK hosts (`dot.net`/`aka.ms`) blocked by proxy; SDK not installable in sandbox |
| CORE-F-003 helper logic | Pass | Verified via standalone Node runtime check (bare-array payload) |
| Last known-good baseline (2026-07-17, PR #700) | Pass | Backend build 0 errors/109 warnings; backend 2,429/2,429; frontend 383/383; FE build pass |
| Authenticated patient-journey E2E | **Not proven** | PR #700: 1 public smoke passed, 4 authenticated skipped |

**Environment constraints (evidence):** `github.com` git reachable (clone/push OK
with provided token); `registry.npmjs.org` reachable (200); `api.github.com` → proxy
403; `dot.net`/`aka.ms` → blocked. Therefore build/test verification for new work is
performed by **GitHub Actions CI** on push, and PR creation is done through the GitHub
web UI. All such items are labelled `Needs CI verification` until the PR checks run.

---

## 7. Module & patient-journey findings (carried + re-verified)

Patient journey (Patients → Appointments → Check-in → Queue → Doctor clinic → Lab →
Finance → Next appointment) canonical owners are intact (`/patients`, `/appointments`,
`/daily-operations`, `/doctor-clinic`, `/lab`, `/finance-v3`, `/ortho`). Key carried
risks, all `Needs runtime verification` in an authenticated environment:

- **Queue** (`/daily-operations`): reorder contract (fixed), VIP/emergency policy
  (F-005), granular permissions + audit for priority/reorder (F-004), strict FIFO.
- **Doctor clinic / visit** (F-001): cross-route idempotency needs a DB-level
  guarantee (unique filtered index) proven by a disposable-DB concurrency repro.
- **Finance** (F-007): re-verify per-currency aggregation after #711–#718; residual
  PDF/notification currency hardcoding.
- **Lab** (F-012): confirmed/appointment-needed states + escalation proof.

No real patient data was accessed or used; all reasoning is from source code and
governance documents only.

---

## 8. Security / privacy notes

- A GitHub token was supplied by the owner for push only; it is stored with `600`
  perms in the sandbox credential store, never printed, never committed. Recommend the
  owner **rotate/revoke it** after this work concludes (owner already intends to).
- No secrets were read from or written to the repository. `StartupDatabaseMaintenance`
  and finance code were reviewed statically; no credential values are reproduced here.
- Prior audit noted HTTP exception-leak was removed; do not reintroduce (per CLAUDE.md).

---

## 9. Items requiring runtime verification

- Authenticated end-to-end patient journey (staff + portal creds in E2E).
- Queue reorder success after the F-003 fix (drag up/down persists) on a live device.
- Multi-currency reconciliation (YER/SAR/USD in one day) across cashier/treasury/reports/PDF.
- Fresh vs representative-legacy DB migration parity.
- Cross-route visit/invoice idempotency under concurrency.

---

## 10. Safety confirmations

- No personal/user file was deleted, moved, renamed, or emptied.
- No repository copy, branch, worktree, or commit was deleted.
- No local uncommitted change was lost (host clones inspected read-only only).
- No patient data used in GitHub or tests; no secret value exposed.
- No destructive operation performed on any production database.
