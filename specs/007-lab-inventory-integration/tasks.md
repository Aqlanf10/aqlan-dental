# 007 Lab Inventory Tasks

- `LABINV-TASK-001`: Map lab routes to backend controllers. Cheap model: read-only.
- `LABINV-TASK-002`: Add tests for lab payables/security. Medium/strong.
- `LABINV-TASK-003`: Improve lab/inventory UI copy. Medium model.
- `LABINV-TASK-004`: Any commission/cost impact. Strong model.
- `LABINV-TASK-005`: Runtime verify overdue lab and payable reports. Needs runtime verification.

## Chairside Parity Tasks (CORE-LAB-CHAIR)

Ordered by impact on the three real problems in `CLAUDE.md`, not by ease.
Each task is an independently mergeable slice.

- `LABINV-TASK-006` — `LABINV-REQ-010` exchange rates. **Strong model.** Highest risk:
  the rate feeds order cost and therefore commission. Presets in `Settings`, optional
  live refresh, explicit staleness, manual override recorded as manual. Failure must be
  visible — no silent default. Tests: preset resolution, refresh failure, manual
  override, and that a failed refresh never writes a rate.
- `LABINV-TASK-007` — `LABINV-REQ-006` FDI tooth picker. **Medium model.** Most-used
  screen; writes the existing free-text field. Tests: selector output equals typed
  input, existing non-standard values still render and save.
- `LABINV-TASK-008` — `LABINV-REQ-007` shade picker from `Settings`. **Medium model.**
  Same modals as 007, land after it to avoid two conflicting edits to one file.
- `LABINV-TASK-009` — `LABINV-REQ-008` scannable code on the order PDF + permission-checked
  lookup screen. **Strong model** for the lookup endpoint (authorization surface),
  medium for the UI. Tests must include an out-of-branch code returning the standard
  refusal without disclosing existence.
- `LABINV-TASK-010` — `LABINV-REQ-009` WhatsApp dispatch to the lab. **Medium model.**
  Reuses the existing `wa.me` pattern; identity from `Settings`; missing phone is an
  Arabic error.
- `LABINV-TASK-011` — `LABINV-REQ-011` consumables against a lab order via owner APIs.
  **Strong model.** Touches inventory and cost; needs runtime verification.

Runtime verification required for `LABINV-TASK-006`, `LABINV-TASK-009`, and
`LABINV-TASK-011` before their exit criteria can be claimed.
