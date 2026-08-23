import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { ar, en } from "@/i18n/messages";
import { navKeyFor } from "@/components/layout/Sidebar";

/**
 * Every sidebar entry must have a key in both bundles.
 *
 * The manifest carries an Arabic `label` beside each entry, and `t(navKeyFor(href), label)`
 * uses it as the fallback. That fallback is the safety net that makes a gradual migration
 * safe — but it also means a missing key is invisible: the entry simply stays Arabic in
 * English, looking exactly like an entry nobody has translated yet.
 *
 * Eight entries were in that state — recall, finance, the two inventory pages and all four
 * HR pages — plus `/ceph`, which had Arabic and no English. None of it showed up as an
 * error anywhere; the sidebar just half-translated itself.
 *
 * So the guarantee is stated as key coverage rather than as "no Arabic in the file": the
 * labels are deliberate fallbacks and are supposed to be there.
 */
describe("sidebar translation keys", () => {
  const source = readFileSync(
    resolve(__dirname, "../../..", "src/components/layout/Sidebar.tsx"),
    "utf8",
  );

  const hrefs = [...new Set([...source.matchAll(/href: "([^"]+)"/g)].map((m) => m[1]))];

  it("finds the sidebar entries at all", () => {
    // A regex that silently matched nothing would make every assertion below vacuous.
    expect(hrefs.length).toBeGreaterThan(20);
  });

  it.each(hrefs)("%s has an Arabic key", (href) => {
    expect(ar[navKeyFor(href)], `${navKeyFor(href)} missing from the Arabic bundle`).toBeTruthy();
  });

  it.each(hrefs)("%s has an English key", (href) => {
    expect(en[navKeyFor(href)], `${navKeyFor(href)} missing from the English bundle`).toBeTruthy();
  });

  it("translates every section heading and badge, not just the links", () => {
    // These are separate from the links and were the last hardcoded Arabic in the frame.
    const keys = [...new Set([
      ...[...source.matchAll(/section: "([^"]+)"/g)].map((m) => m[1]),
      ...[...source.matchAll(/badge: "([^"]+)"/g)].map((m) => m[1]),
    ])];

    expect(keys.length).toBeGreaterThan(5);
    for (const key of keys) {
      // A badge can be a bare pictogram ("⭐"), which reads the same in any language and
      // needs no key. Anything with letters in it is text somebody has to read.
      if (!/\p{Letter}/u.test(key)) continue;

      expect(key, "a section or badge with words in it must be a bundle key").toMatch(/^nav\./);
      expect(ar[key], `${key} missing from the Arabic bundle`).toBeTruthy();
      expect(en[key], `${key} missing from the English bundle`).toBeTruthy();
    }
  });
});
