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
- `CEPH-TASK-008`: SEQ-43 — establish and review the binding WebCeph functional-parity matrix. Strong model, documentation only.
- `CEPH-TASK-009`: SEQ-44 — implement non-destructive viewer rulers and preview transforms with regression tests.
- `CEPH-TASK-010`: SEQ-45 — implement doctor-reviewed structured assessment and explicit problem-list handoff.
- `CEPH-TASK-011`: SEQ-46 — persist named doctor-authored treatment/VTO scenarios and comparisons.
- `CEPH-TASK-012`: SEQ-47 — extend structural superimposition to multiple dated records with reference/opacity/export controls.
- `CEPH-TASK-013`: SEQ-48 — add documented PA cephalometric analysis inside the existing ceph module. Strong clinical review required.
- `CEPH-TASK-014`: SEQ-49 — expose canonical model analysis as the occlusogram workflow without duplicate calculations.
- `CEPH-TASK-015`: SEQ-50 — add real-record timelapse, unified case review, approved tags, and privacy-safe cohort analysis.
- `CEPH-TASK-016`: SEQ-51 — run authenticated final parity, accessibility, responsive, export, security, CI, and deployment QA; close the matrix.
