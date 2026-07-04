# 004 Orthodontics Workspace Requirements

## Current State

Evidence: `frontend/src/app/(dashboard)/ortho/`, `frontend/src/components/ortho/`, `OrthoCasesController.cs`, `OrthoCaseAiController.cs`, `OrthoModelAnalysesController.cs`, `OrthoService`, `OrthoCaseQueryService`, ortho docs under `docs/ortho-module/`.

- `ORTHO-REQ-001`: `/ortho` SHALL be the canonical orthodontics workspace.
- `ORTHO-REQ-002`: Ortho case, diagnosis, records checklist, photos, treatment plans, visits, retention, model analysis, finance tab, surgical planning, and AI draft panels SHALL extend existing tabs/components.
- `ORTHO-REQ-003`: Ortho AI SHALL be draft/supporting only, not an automatic clinical decision.
- `ORTHO-REQ-004`: Ortho finance SHALL not bypass FinanceV3 rules.
- `ORTHO-REQ-005`: Lab orders linked to ortho SHALL use existing lab owners.
- `ORTHO-REQ-006`: When case-header overview data fails to load, the workspace SHALL show a visible Arabic error state with retry — never silent "—" placeholders (QA4-01).

## Target State

Unified orthodontic workflow without split workspaces.

## Risks

Duplicate ortho screens, clinical AI overclaiming, finance drift, lab/ortho link breakage.

## Allowed Future Work

Enhance existing tabs, reports, model analysis, image preparation, case presentation, lab integration.

## Forbidden Future Work

Second ortho workspace, fake AI, hardcoded plan labels, finance shortcuts.

## Acceptance Criteria

- WHEN ortho work changes THEN `/ortho` owners SHALL be extended.
- WHEN AI drafts appear THEN doctor review SHALL be clear. Needs runtime verification.
