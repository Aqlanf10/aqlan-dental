#!/usr/bin/env python3
"""
CORE-REQ-006 — how much of the interface still speaks only Arabic.

The Arabic fallback in t() is what makes a gradual migration safe: an untranslated key renders
the string the screen already showed. The cost is that a translated surface and an untranslated
one look identical from the outside, so "we are making progress" cannot be checked by looking
at the app.

This counts the Arabic string literals still embedded in components — the number that must
reach zero for a surface to be genuinely bilingual. The frontend test
`i18nTranslatedSurfaces.test.ts` turns the same measurement into a gate for the surfaces that
claim to be finished, so a claim cannot outrun the code.

Usage:  python3 scripts/i18n-coverage-scan.py [path-prefix ...]
"""
import collections
import os
import re
import sys

ROOT = "frontend/src"
BUNDLE = os.path.join(ROOT, "i18n", "messages.ts")

# Any Arabic outside a comment is user-visible text. An earlier version of this matched only
# quoted strings and text between two tags on one line, which silently missed bare JSX text on
# its own line — the most common shape of all. It reported a file as finished while a hardcoded
# label sat in it, so the rule is now "Arabic anywhere in the code", with comments removed
# first because Arabic commentary is legitimate and permanent.
COMMENT = re.compile(r"/\*[\s\S]*?\*/|//[^\n]*")
ARABIC_RUN = re.compile(r"[؀-ۿ][؀-ۿ\s\u200f\u200e.،:!؟\-()/0-9]*")


def strip_comments(source: str) -> str:
    # Replaced with spaces rather than deleted so nothing on either side is joined together.
    return COMMENT.sub(lambda m: " " * len(m.group(0)), source)


def count_file(path: str) -> int:
    with open(path, encoding="utf-8") as fh:
        source = fh.read()

    code = strip_comments(source)
    return sum(1 for m in ARABIC_RUN.finditer(code) if m.group(0).strip())


def scan(prefixes: list[str]) -> collections.Counter:
    counts: collections.Counter = collections.Counter()
    for dirpath, _, filenames in os.walk(ROOT):
        # Tests assert on Arabic copy deliberately; they are not user-facing surfaces.
        if "__tests__" in dirpath:
            continue
        for filename in filenames:
            if not filename.endswith((".tsx", ".ts")):
                continue
            path = os.path.join(dirpath, filename)
            # The bundle is where the Arabic is supposed to live. Counting it made the number
            # go *up* as surfaces were migrated — every string moved out of a component landed
            # here — which is the opposite of what this measures.
            if path == BUNDLE:
                continue
            if prefixes and not any(path.startswith(p) for p in prefixes):
                continue
            found = count_file(path)
            if found:
                counts[path] = found
    return counts


def main() -> int:
    prefixes = sys.argv[1:]
    counts = scan(prefixes)
    total = sum(counts.values())

    print(f"files with Arabic literals: {len(counts)}    total literals: {total}")
    if counts:
        print("\nlargest surfaces:")
        for path, found in counts.most_common(15):
            print(f"{found:5}  {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
