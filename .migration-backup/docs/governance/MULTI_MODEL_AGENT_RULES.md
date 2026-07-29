# Multi-Model Agent Rules

## Assignment Examples

- Claude Code: architecture, complex refactors, finance/auth/patient-access review, migrations review, PR creation.
- Codex: code implementation, tests, focused bug fixes, documentation updates tied to specs.
- Gemini: analysis, planning, UI review, route comparisons, summaries.
- OpenCode: narrow code edits in explicitly listed files.
- Cheap model: read-only audit, checklist, copy edits, simple test drafts, duplicated label detection.

## Communication Protocol

Every agent must state:

- Which spec it read.
- Which files it will touch before editing.
- Which files are forbidden.
- Which tests it expects to run.
- Which risks are present.

Every agent must produce a completion report:

- Spec read.
- Files inspected.
- Files changed.
- Tests run.
- Unverified items.
- Follow-up needed.

## Stop Conditions

An agent must stop if:

- The requested feature has no spec.
- The existing module owner is unclear.
- It detects a spec/code mismatch.
- It would need to touch finance/auth/patient access/migrations but is not a strong model.
- It cannot cite exact files.
- It finds duplicate route/controller/service responsibility.

## Forbidden Coordination Pattern

Do not let one model invent architecture and another implement it without checking the repository and specs. Each handoff must include exact files and stop conditions.
