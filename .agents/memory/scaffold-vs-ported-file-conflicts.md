---
name: Scaffold vs ported file conflicts during framework migration
description: When porting a real app into a scaffolded artifact, never delete/rename either side's files to resolve name/casing conflicts without checking real usage first.
---

When an artifact scaffold (e.g. shadcn UI kit) and a ported real app both populate the same
directory (e.g. `components/ui/`) with overlapping or case-colliding filenames, do NOT resolve
the conflict by deleting or renaming either side based on assumption.

**Why:** On one migration, the ported app's real `Button.tsx`/`Card.tsx`/etc. were deleted and
replaced with scaffold-added lowercase shadcn equivalents to fix a casing conflict. This silently
swapped real, in-use UI implementations for unrelated scaffold placeholders — a real behavioral
regression that looked fine visually (login screen still rendered) but changed app internals.

**How to apply:** Before deleting/renaming any conflicting file, grep the entire codebase for
real import-alias usage (e.g. `@/components/ui/X`) of both candidates. Diff the ported file
against its pre-migration backup. Only delete the side with zero real usage. If both sides are
used somewhere, the conflict needs a rename with all call sites updated — never a silent swap.

Same applies to hooks/utilities scaffolds add speculatively (e.g. `use-toast.ts`, `use-mobile.tsx`)
— confirm zero real `@/...` import usage before deleting.
