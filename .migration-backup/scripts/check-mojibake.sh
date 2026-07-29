#!/usr/bin/env bash
set -euo pipefail

# Prevent Arabic/UTF-8 mojibake from reaching main.
# This check catches common artifacts produced when UTF-8 Arabic text is
# accidentally decoded/re-saved as Windows-1256/Latin-1/CP1252.

export LC_ALL=C.UTF-8

ROOTS=(
  "frontend/src"
  "backend/src/AqlanDentalPro.API"
  "backend/src/AqlanDentalPro.Application"
  "backend/src/AqlanDentalPro.Infrastructure"
)

# Keep the pattern focused on mojibake markers, not valid Arabic text.
# Examples blocked:
#   المركز المالي -> ط§ظ„ظ…ط±ظƒط² ط§ظ„ظ…ط§ظ„ظٹ
#   فشل التحميل   -> ظپط´ظ„ ط§ظ„طھط­ظ…ظٹظ„
#   decorative box chars -> â• / â”
BAD_PATTERN='(ط§|ظ„|ظپ|طھ|ط±|ط®|ط¬|ط¹|ط¥|ط£|Ø|Ù|Â|Ã|â€|â•|â”|ï»¿|�)'

EXTENSIONS=(
  -name "*.ts" -o -name "*.tsx" -o -name "*.js" -o -name "*.jsx" -o
  -name "*.cs" -o -name "*.json" -o -name "*.resx" -o -name "*.html" -o
  -name "*.css" -o -name "*.md" -o -name "*.yml" -o -name "*.yaml"
)

EXCLUDES=(
  -not -path "*/node_modules/*"
  -not -path "*/bin/*"
  -not -path "*/obj/*"
  -not -path "*/.next/*"
  -not -path "*/coverage/*"
  -not -path "*/dist/*"
  -not -path "*/build/*"
  -not -path "*/playwright-report/*"
  -not -path "*/playwright-test-output/*"
)

found=0

printf 'Checking source files for Arabic mojibake / corrupted UTF-8...\n'

for root in "${ROOTS[@]}"; do
  [ -d "$root" ] || continue

  while IFS= read -r -d '' file; do
    if matches=$(grep -I -n -E "$BAD_PATTERN" "$file" || true); then
      if [ -n "$matches" ]; then
        printf '\n::error file=%s::Possible Arabic mojibake / encoding corruption detected\n' "$file"
        printf '%s\n' "$matches"
        found=1
      fi
    fi
  done < <(find "$root" -type f \( "${EXTENSIONS[@]}" \) "${EXCLUDES[@]}" -print0)
done

if [ "$found" -ne 0 ]; then
  cat <<'MSG'

Arabic mojibake was detected. Fix corrupted text before merging.
Common examples:
  - ط§ظ„... instead of readable Arabic
  - Ø / Ù sequences from broken UTF-8
  - â€ / â• / â” sequences from broken punctuation or box characters
  - � replacement characters

If this is a false positive, inspect the file manually before changing the guard.
MSG
  exit 1
fi

printf 'No Arabic mojibake detected.\n'
