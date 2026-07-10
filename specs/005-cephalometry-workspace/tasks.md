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
