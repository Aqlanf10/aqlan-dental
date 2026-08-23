import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { ar, en } from "@/i18n/messages";

/**
 * CORE-REQ-006 — a surface may only be called bilingual once it actually is.
 *
 * The Arabic fallback in `t()` is what makes a gradual migration safe: an untranslated key
 * renders the string the screen already showed, so a half-migrated app never shows a blank or
 * a raw key to a receptionist mid-shift. The cost is that a finished surface and an untouched
 * one look identical from the outside — switch to English and Arabic text is equally
 * consistent with "translated" and with "nobody has started".
 *
 * So finishing a surface is a claim, and this is the check on the claim. Add a file to
 * TRANSLATED_SURFACES only when it is done; the two rules below then hold it to that:
 *
 *   1. No Arabic string literal may remain in the file — every user-visible string goes
 *      through `t()`.
 *   2. Every key namespace the surface owns must be complete in `en` — a missing English key
 *      falls back to Arabic, which is exactly the silence this test exists to break.
 */

const frontend = resolve(__dirname, "../..", "..");

/** Surfaces claimed complete, with the key namespaces each one owns. */
const TRANSLATED_SURFACES: ReadonlyArray<{ file: string; namespaces: readonly string[] }> = [
  { file: "src/components/layout/Topbar.tsx", namespaces: ["topbar."] },
];

/**
 * Any Arabic outside a comment is user-visible text — the same rule
 * `scripts/i18n-coverage-scan.py` applies, so the two cannot disagree.
 *
 * The first version of this matched only quoted strings and text between two tags on one
 * line, and so missed bare JSX text on its own line, which is the most common shape of all.
 * It passed while a hardcoded «تسجيل الخروج» sat in the file it was guarding. Comments are
 * stripped first because Arabic commentary is legitimate and stays.
 */
const COMMENT = /\/\*[\s\S]*?\*\/|\/\/[^\n]*/g;
const ARABIC_RUN = /[؀-ۿ][؀-ۿ\s\u200f\u200e.،:!؟\-()/0-9]*/g;

function arabicOutsideComments(source: string): string[] {
  const code = source.replace(COMMENT, (match) => " ".repeat(match.length));
  return [...code.matchAll(ARABIC_RUN)].map((m) => m[0].trim()).filter(Boolean);
}

describe("surfaces claimed as translated", () => {
  it.each(TRANSLATED_SURFACES)("$file has no Arabic left in the source", ({ file }) => {
    const source = readFileSync(resolve(frontend, file), "utf8");
    // If the file could not be read, every assertion below would be checking an empty string.
    expect(source.length).toBeGreaterThan(200);

    const leftovers = arabicOutsideComments(source);

    expect(leftovers, `${file} still hardcodes Arabic, so it is not bilingual yet`).toEqual([]);
  });

  it.each(TRANSLATED_SURFACES)("$file has full English coverage", ({ namespaces }) => {
    const owned = Object.keys(ar).filter((key) => namespaces.some((ns) => key.startsWith(ns)));
    // A surface that owns no keys is a typo in the registry, not a finished translation.
    expect(owned.length).toBeGreaterThan(0);

    const untranslated = owned.filter((key) => !en[key]);
    expect(untranslated, "these fall back to Arabic on a surface claimed as done").toEqual([]);
  });

  it("never lets the English bundle claim a key Arabic does not define", () => {
    // Arabic is the source of truth. An English-only key is dead weight at best, and at worst
    // a renamed key whose Arabic side was deleted — which would silently render the key name.
    const orphans = Object.keys(en).filter((key) => !ar[key]);
    expect(orphans).toEqual([]);
  });
});
