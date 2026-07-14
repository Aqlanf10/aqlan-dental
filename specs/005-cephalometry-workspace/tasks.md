# 005 Cephalometry Tasks

- `CEPH-TASK-001`: Audit AI draft wording. Strong model.
  — ✅ Done 2026-07-10 (strong model), jointly with ORTHO-TASK-003 (PR #639) —
  the ceph surfaces were in that review's scope. Verdict: PASS.
  `CephAiLandmarkDraftService.ReviewDisclaimer` explicitly disclaims certified
  tracing and mandates per-point orthodontist review before save/calculation;
  `CephAiDraftService.DisclaimerAr` + its system prompt (rules 5–6) forbid final
  treatment decisions and REQUEST the disclaimer verbatim at the end of every
  draft — and since a prompt is a request, not a guarantee (Codex P2 on #641),
  `GenerateDraftAsync` now also ENFORCES it server-side: any draft body missing
  `DisclaimerAr` gets it appended before return (pinned by 2 unit tests:
  append-when-missing, no-duplicate-when-compliant). The ceph UI labels every AI
  point "AI · مسودة", flags low-confidence points for manual review, renders the
  returned disclaimer, and honestly labels the non-AI template tool
  "ليست ذكاءً اصطناعيًا". Full evidence list in
  `specs/004-orthodontics-workspace/tasks.md` (ORTHO-TASK-003 entry).
- `CEPH-TASK-002`: Map ceph UI to backend DTOs. Cheap model: read-only.
  — ✅ Done 2026-07-10. Full map in `specs/005-cephalometry-workspace/dto-map.md`:
  21 endpoint→DTO→consumer rows verified against the actual controllers, DTO
  files, ts types and pages. Notable finding: the ts `CephAnalysis` detail type
  drops `IsAutoTraced`/`DoctorId`/`Notes` present in `CephAnalysisDetailDto`
  (with `Notes` round-tripped on create but never displayable) — real drift
  flagged for a future UI decision, not silently "fixed". No frontend call
  targets a non-existent endpoint.
- `CEPH-TASK-003`: Add tests for report/AI behavior. Strong model.
- `CEPH-TASK-004`: Runtime-verify tracing, VTO, and draft review. Needs runtime verification.
- `CEPH-TASK-005`: SEQ-40 (owner directive 2026-07-12) — add soft-tissue
  Pogonion (`SPog`), first-molar landmarks (`U6`, `L6`), draw the functional
  occlusal plane (incisal overbite midpoint → molar occlusion midpoint), and
  upgrade Wits + Steiner S-line + Ricketts E-line to their true clinical
  definitions when the new points are placed. The 24-point core set stays the
  readiness gate — the 3 new points are optional enhancements, so previously
  saved analyses keep working and their historic values don't shift (legacy
  approximations remain the fallback). Strong model.
- `CEPH-TASK-006`: SEQ-41 (owner directive 2026-07-13) — add an Excel-compatible
  UTF-8 CSV export for the saved cephalometric measurement table. Reuse the
  final-report approval and clean-state gate, include case metadata plus values,
  norms, deviations, groups, and interpretations, and neutralize spreadsheet
  formula prefixes in text cells. Frontend-only; no API, schema, permission, or
  clinical calculation changes. Strong model. — ✅ Done in PR #678; eight
  focused tests plus Backend, Frontend, E2E, Encoding Guard, and Vercel passed.
- `CEPH-TASK-007`: SEQ-42 (owner directive 2026-07-13) — expose the existing
  analysis approval flag in the ceph list DTO and derive an honest workflow
  stage in `/ceph`. Saved measurements alone must not be presented as final-
  report readiness; the next action becomes review/approval until the doctor
  approves. No schema, permission, AI, or clinical-calculation changes. Strong
  model. — ✅ Done in PR #679; Backend, Frontend, E2E, Encoding Guard, and
  Vercel passed.
- `CEPH-TASK-008`: SEQ-43 — establish and review the binding WebCeph functional-parity matrix. Strong model, documentation only. — ✅ Done in PR #680; full CI and Vercel passed.
- `CEPH-TASK-009`: SEQ-44 — implement non-destructive viewer rulers and preview transforms with regression tests. — ✅ Done in PR #681; full CI and Vercel passed.
- `CEPH-TASK-010`: SEQ-45 — implement doctor-reviewed structured assessment and explicit problem-list handoff. — ✅ Done in PR #682; full CI and Vercel passed.
- `CEPH-TASK-011`: SEQ-46 — persist named doctor-authored treatment/VTO scenarios and comparisons. — ✅ Implemented in PR #683 with immutable versions, approval/access gates, saved before/after snapshots, PDF inclusion, and an explicit no-response-prediction disclaimer; local backend/frontend suites and production builds passed.
- `CEPH-TASK-012`: SEQ-47 — extend structural superimposition to multiple dated records with reference/opacity/export controls. — ✅ Implemented in PR #684 with 2–6 same-case analyses/versions, explicit reference, stable colors, per-layer opacity, non-overlapping legend, and metadata-bearing SVG export; frontend suites and production build passed locally.
- `CEPH-TASK-013`: SEQ-48 — add documented PA cephalometric analysis inside the existing ceph module. Strong clinical review required. — ✅ Implemented with 15 manual PA landmarks, tilted ZR–ZL/MSR geometry, calibrated transverse and asymmetry measurements, live/backend parity, saved snapshots, approval gates, PDF/CSV integration, descriptive no-norm handling, and focused visual/unit/build verification.
- `CEPH-TASK-014`: SEQ-49 — expose canonical model analysis as the occlusogram workflow without duplicate calculations. — ✅ Implemented with three clinical modes (tooth size, arch width/length, irregularity), reusable occlusal case photos, saved-version navigation, approval/PDF controls, and the unchanged canonical model-analysis API/calculator.
- `CEPH-TASK-015`: SEQ-50 — add real-record timelapse, unified case review, approved tags, and privacy-safe cohort analysis. — ✅ Implemented with ordered ceph/photo playback without interpolation, a case entry that reuses the canonical presentation owner, allowlisted tags from doubly approved structured diagnoses, and aggregate-only cohorts using one latest accessible record per patient plus a five-patient privacy threshold; 373 frontend tests, 189 ceph backend tests, both production builds, and desktop/mobile visual QA passed locally.
- `CEPH-TASK-016`: SEQ-51 — run final parity, accessibility, responsive, export, security, CI, and deployment QA; close the original workflow matrix. — ✅ PR #688 passed Backend, Frontend, E2E, Encoding Guard, and Vercel and was merged. Public deployment smoke always runs; authenticated ceph coverage runs when dedicated E2E staff credentials are configured and reports an explicit non-evidentiary skip otherwise.
- `CEPH-TASK-017`: SEQ-52 — add landmark provenance and explicit review gates, account-owned WebCeph Landmark Table import, model-specific doctor-correction metrics, and a safe official-API migration boundary. — 🟡 Implementation in progress; WebCeph-equivalent accuracy awaits a labelled reference benchmark, and patient/record/image sync awaits the partner agreement, plan, key, and final contract.
- `CEPH-TASK-018`: SEQ-52 clinical-accuracy foundation — document the observed WebCeph workflow, audit the native Aqlan implementation, freeze the first landmark-definition and validation/data-governance contracts, publish an honest empty baseline, and sequence the remaining work into small PRs. Documentation only: no model, schema, provider, clinical calculation, or production behavior changes. Strong model and orthodontist review required. — ✅ Done in PR #690; all six required checks passed and the PR was merged.
- `CEPH-TASK-019`: SEQ-53 gold-standard tooling — add the strict de-identified benchmark-manifest schema and an Admin-only stateless validator; enforce patient-cluster split isolation, two independent reviews of all 24 core landmarks, physical-coordinate disagreement, explicit missing-anatomy decisions, and third-reviewer/consensus adjudication without automatic averaging. Include synthetic unit fixtures and CI documentation-contract checks. — ✅ Done in PR #691; all six CI/deployment checks passed and the PR was merged.
- `CEPH-TASK-020`: SEQ-54 landmark evaluation engine — accept a strict pinned prediction/benchmark request for exactly one non-training split, preserve missing outputs as failures, calculate radial-error/SDR/completeness/not-visible metrics overall and by image/landmark/subgroup, and produce deterministic patient-clustered MRE/SDR confidence intervals without exposing patient linkage hashes. Add an Admin-only stateless endpoint, schema/docs, synthetic golden fixtures, and CI evidence. — ✅ Done in PR #692; all six CI/deployment checks passed and the PR was merged.
- `CEPH-TASK-021`: SEQ-55 geometry and advanced evaluation — extract the canonical lateral geometry into one versioned engine and add derived-measurement, repeatability/ICC, paired comparator, confidence/coverage, and clinical-tolerance evaluation with backend/frontend parity fixtures. — 🟡 Frozen geometry was merged in PR #693; measurement, paired comparator, repeatability/ICC, and confidence/coverage calculators are implemented on `codex/seq-55-ceph-advanced-evaluation` pending PR CI and statistics/clinical review. Clinical accuracy remains unmeasured.
