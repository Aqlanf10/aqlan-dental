# 004 Orthodontics Tasks

- `ORTHO-TASK-001`: Audit ortho tabs and owners. Cheap model: read-only.
  — ✅ Done 2026-07-10 (SEQ-08 audit, PR #636): all 5 spec-004 acceptance rules
  checked by direct code inspection; the ORTHO-REQ-006 gap found was fixed in-PR.
- `ORTHO-TASK-002`: Add/update ortho unit tests for changed behavior. Medium/strong depending risk.
- `ORTHO-TASK-003`: Verify AI draft copy is clinically safe. Strong model.
  — ✅ Done 2026-07-10 (strong model). All 6 AI surfaces reviewed against: draft
  labeling, mandatory doctor-review language, no diagnostic overclaiming, honest
  errors, Arabic correctness. Verdict: PASS across the board —
  `CephAiLandmarkDraftService.ReviewDisclaimer` (explicit "ليست تتبعاً معتمداً" +
  "يجب مراجعة وتحريك كل نقطة"), `CephAiDraftService.DisclaimerAr` + system-prompt
  rules 5–6 (no final decisions, disclaimer forced verbatim),
  `OrthoCaseDraftService`/`OrthoSurgicalDraftService` disclaimers (dual-specialist
  for surgical), ceph UI ("AI · مسودة" chips, low-confidence flags, honest
  "ليست ذكاءً اصطناعيًا" on the template simulation), `AiAssistantPanel` (copy-only,
  amber disclaimer box), VTO cards (per-card disclaimer with `VTO_DISCLAIMER_AR`
  fallback). One consistency fix applied: `OrthoCaseDraftAiButton` injected English
  section labels ("AI draft"/"Evidence:"/"Missing:"/"Warnings:") into the doctor's
  Arabic draft — Arabized.
- `ORTHO-TASK-004`: Runtime-check unified workspace navigation. Needs runtime verification.
