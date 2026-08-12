#!/usr/bin/env bash
#
# CORE-P1-S5 — prepares the staff account of an EPHEMERAL test stack so the authenticated
# Playwright specs can actually run.
#
# Why this exists
# ---------------
# The seeded staff account is created with MustChangePassword = true, which is correct: a
# fresh deployment must not hand out a usable account without a password change. The
# consequence for testing is that logging in lands on /change-password, so
#
#   * ceph-runtime.spec.ts skips itself outright ("must change its password first"), and
#   * voice-recorder.spec.ts never reaches a dashboard route,
#
# which is how two specs sat broken without anyone knowing — the suite skipped for want of
# credentials, so nothing ever executed them.
#
# This performs the password change once, through the real endpoint, exactly as a human would
# on first login. It does not write to the database and it does not weaken the requirement.
#
# Only ever point this at a disposable stack. It is not, and must not become, a production tool.
#
# Usage:
#   API_URL=http://localhost:5080 \
#   E2E_STAFF_USERNAME=admin \
#   E2E_SEED_PASSWORD='...' \
#   E2E_STAFF_PASSWORD='...' \
#   scripts/e2e/prepare-e2e-account.sh
#
set -euo pipefail

API_URL="${API_URL:?API_URL is required}"
USERNAME="${E2E_STAFF_USERNAME:?E2E_STAFF_USERNAME is required}"
SEED_PASSWORD="${E2E_SEED_PASSWORD:?E2E_SEED_PASSWORD is required}"
TARGET_PASSWORD="${E2E_STAFF_PASSWORD:?E2E_STAFF_PASSWORD is required}"

json_field() { python3 -c "import json,sys; print(json.load(sys.stdin).get('$1',''))"; }

login() {
  curl -sS -X POST "$API_URL/api/auth/login" \
    -H 'Content-Type: application/json' \
    -d "$(python3 -c "
import json,sys
print(json.dumps({'username': sys.argv[1], 'password': sys.argv[2]}))
" "$USERNAME" "$1")"
}

# Already changed? Then this script has run before against the same stack — treat that as
# success rather than an error, so re-running the pipeline is safe.
if token=$(login "$TARGET_PASSWORD" | json_field accessToken) && [ -n "$token" ]; then
  echo "E2E staff account already uses the target password — nothing to do."
  exit 0
fi

token=$(login "$SEED_PASSWORD" | json_field accessToken)
if [ -z "$token" ]; then
  echo "ERROR: could not log in with the seed password. The stack may not have seeded yet." >&2
  exit 1
fi

status=$(curl -sS -o /dev/null -w '%{http_code}' -X POST "$API_URL/api/auth/change-password" \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $token" \
  -d "$(python3 -c "
import json,sys
print(json.dumps({'currentPassword': sys.argv[1], 'newPassword': sys.argv[2]}))
" "$SEED_PASSWORD" "$TARGET_PASSWORD")")

if [ "$status" != "200" ] && [ "$status" != "204" ]; then
  echo "ERROR: change-password returned HTTP $status" >&2
  exit 1
fi

# Prove it rather than assume it: the whole point is that the account is now usable without
# the change-password interstitial.
if [ -z "$(login "$TARGET_PASSWORD" | json_field accessToken)" ]; then
  echo "ERROR: the new password does not authenticate." >&2
  exit 1
fi

echo "E2E staff account is ready (password changed, no interstitial)."
