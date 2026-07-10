# 005 Cephalometry Tasks

- `CEPH-TASK-001`: Audit AI draft wording. Strong model.
  — ✅ Done 2026-07-10 (strong model), jointly with ORTHO-TASK-003 (PR #639) —
  the ceph surfaces were in that review's scope. Verdict: PASS.
  `CephAiLandmarkDraftService.ReviewDisclaimer` explicitly disclaims certified
  tracing and mandates per-point orthodontist review before save/calculation;
  `CephAiDraftService.DisclaimerAr` + its system prompt (rules 5–6) forbid final
  treatment decisions and force the disclaimer verbatim at the end of every
  generated draft; the ceph UI labels every AI point "AI · مسودة", flags
  low-confidence points for manual review, renders the returned disclaimer, and
  honestly labels the non-AI template tool "ليست ذكاءً اصطناعيًا". Full evidence
  list in `specs/004-orthodontics-workspace/tasks.md` (ORTHO-TASK-003 entry).
- `CEPH-TASK-002`: Map ceph UI to backend DTOs. Cheap model: read-only.
- `CEPH-TASK-003`: Add tests for report/AI behavior. Strong model.
- `CEPH-TASK-004`: Runtime-verify tracing, VTO, and draft review. Needs runtime verification.
