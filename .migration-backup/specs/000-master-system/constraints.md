# Master Constraints

- No duplicate modules.
- No second dashboard or control panel.
- No duplicate module routes or sidebar shortcuts.
- Existing modules must be extended before creating anything new.
- No hardcoded finance calculations when Settings or service helpers exist.
- No unsafe patient access or frontend-only authorization.
- No destructive or casual migrations.
- No fake clinical AI, no fake diagnosis, no fake treatment certainty.
- No unreviewed cephalometry diagnosis; AI output is draft-only until doctor review.
- No PDF/report identity hardcoding outside approved fallback helpers.
- No default-allow routes. `routePermissions.ts` currently denies unmatched dashboard routes.
- No new production secrets, committed credentials, or environment assumptions.
- No bypass of cashier-session rules for payments/refunds/commission payouts.
- No change to upload privacy behavior without security review.
- No cheap-model edits to finance, auth, permissions, patient access, migrations, deployment, or clinical AI.
