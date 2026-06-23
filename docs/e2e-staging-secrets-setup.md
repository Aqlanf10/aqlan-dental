# E2E Staging Secrets — Setup Guide

## Context
The CI workflow (`.github/workflows/ci.yml`) has an `e2e` job that runs Playwright specs against a staging/production frontend. The specs skip gracefully when secrets are missing, so CI stays green — but to actually RUN the e2e tests, the 5 secrets below must be set.

## Required secrets (GitHub Actions → Settings → Secrets and variables → Actions)

| Secret name | Value | Status |
|---|---|---|
| `E2E_API_URL` | `https://aqlan-dental.vercel.app` | ✅ Set (by orchestrator) |
| `E2E_STAFF_PHONE` | Staff login username (e.g. `admin`) | ⬜ Owner must set |
| `E2E_STAFF_PASSWORD` | Staff login password (the `ADMIN_DEFAULT_PASSWORD` deployed on Railway) | ⬜ Owner must set |
| `E2E_PORTAL_USERNAME` | Patient portal username (e.g. `GM0001`) | ⬜ Owner must set |
| `E2E_PORTAL_PASSWORD` | Patient portal password | ⬜ Owner must set |

## How to set the remaining 4 secrets

### Option A — GitHub Web UI
1. Go to https://github.com/Aqlanf10/aqlan-dental/settings/secrets/actions
2. Click "New repository secret"
3. Add each of the 4 secrets above with their real values.

### Option B — `gh` CLI
```bash
gh secret set E2E_STAFF_PHONE --body "admin"
gh secret set E2E_STAFF_PASSWORD --body "<the real admin password>"
gh secret set E2E_PORTAL_USERNAME --body "GM0001"
gh secret set E2E_PORTAL_PASSWORD --body "<the real portal password>"
```

### Option C — GitHub API (if no `gh` CLI)
```bash
# Requires pynacl: pip install pynacl
# See scripts/set-github-secret.py for a working example
```

## Important notes

1. **Use a dedicated test account, not the production admin.** Create a staff user specifically for e2e (e.g. username `e2e-runner`, role `Admin`) and a patient portal account for e2e. This avoids locking out the real admin if Playwright retries fail repeatedly (rate-limit on login).

2. **The test account must NOT have `MustChangePassword = true`** — otherwise login redirects to `/change-password` and the e2e spec fails.

3. **The backend must be reachable** from the GitHub Actions runner (ubuntu-latest). The Vercel frontend proxies `/api/*` to the Railway backend, so as long as `E2E_API_URL` points to the Vercel app, the runner only needs public internet access.

4. **Secrets are masked in logs** — the actual password values never appear in CI output.

## Verifying the e2e job runs

After setting all 5 secrets, push any commit to `main` (or open a PR). The `e2e` job will:
1. Detect `E2E_API_URL` is set → proceed (no longer skip).
2. Install Playwright Chromium browsers.
3. Run the 3 specs:
   - `auth.spec.ts` — staff login → dashboard redirect
   - `portal-login.spec.ts` — patient portal login → /portal redirect
   - `voice-recorder.spec.ts` — voice recorder widget
4. Upload the HTML report + traces as a 7-day artifact on failure.

If a spec fails, download the `playwright-report` artifact to view the full trace + screenshots.

## Current e2e coverage (minimal)

The specs cover the **login golden paths only**. The audit's full e2e wishlist (booking, payment, receipt PDF, cashier-session-close) requires deterministic seeded staging data and is tracked as a follow-up. To add a new spec, create `frontend/playwright-tests/<name>.spec.ts` — the CI job picks up all `*.spec.ts` files automatically.

## Related
- PR #509 — added the `e2e` job + specs (TEST-16)
- PR #512 — FE-15 loading/error boundaries (not e2e-related, but merged in the same batch)
- `.github/workflows/ci.yml` — the `e2e` job definition
- `frontend/playwright.config.ts` — Playwright configuration
