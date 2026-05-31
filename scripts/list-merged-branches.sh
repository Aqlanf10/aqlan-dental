#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# list-merged-branches.sh
#
# Lists all remote branches that have been merged into origin/main.
# Useful for identifying stale branches that can be safely deleted.
#
# Usage:
#   ./scripts/list-merged-branches.sh          # List merged branches
#   ./scripts/list-merged-branches.sh --delete  # Delete merged branches
#
# Safety:
#   - Never deletes: main, HEAD, stable-*, release-*
#   - Always fetches with --prune before listing
#   - Writes results to merged-branches.txt
# ──────────────────────────────────────────────────────────────────────────────

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

# Fetch and prune stale remote references
echo "Fetching remote branches with prune..."
git fetch --all --prune 2>/dev/null

# List merged branches (excluding main, HEAD, and protected patterns)
MERGED=$(git branch -r --merged origin/main | \
  grep -v 'main' | \
  grep -v 'HEAD' | \
  grep -v 'stable-' | \
  grep -v 'release-' | \
  sed 's/origin\///' | \
  sed 's/^[[:space:]]*//' | \
  sort)

if [ -z "$MERGED" ]; then
  echo "No merged branches found (other than protected branches)."
  echo "" > merged-branches.txt
  exit 0
fi

BRANCH_COUNT=$(echo "$MERGED" | wc -l | tr -d ' ')
echo "Found $BRANCH_COUNT merged branch(es):"
echo ""

echo "$MERGED" | while read -r branch; do
  echo "  - $branch"
done

# Write to file
echo "$MERGED" > merged-branches.txt
echo ""
echo "List saved to merged-branches.txt"

# Delete mode
if [ "${1:-}" = "--delete" ]; then
  echo ""
  echo "WARNING: This will delete $BRANCH_COUNT remote branch(es)!"
  read -p "Are you sure? (yes/no): " confirm
  if [ "$confirm" = "yes" ]; then
    echo "$MERGED" | while read -r branch; do
      echo "Deleting origin/$branch..."
      git push origin --delete "$branch" 2>/dev/null || echo "  Failed to delete $branch (may not exist)"
    done
    echo "Done! Re-running prune..."
    git fetch --all --prune 2>/dev/null
  else
    echo "Aborted. No branches deleted."
  fi
fi
