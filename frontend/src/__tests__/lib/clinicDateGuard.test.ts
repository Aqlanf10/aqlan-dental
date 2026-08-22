import { describe, it, expect } from "vitest";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, resolve } from "node:path";

/**
 * The clinic-day trap, enforced instead of documented.
 *
 * `CLAUDE.md` has said for months: use `localDateString()` for "today", never
 * `toISOString().slice(0, 10)`. Yemen is UTC+3, so between midnight and 03:00 local time the
 * UTC date is still *yesterday*. A value defaulted from `toISOString()` in that window is
 * dated into the previous day.
 *
 * That was not hypothetical. A go-live dry run found it live in two finance screens: the
 * opening-balance date and the manual journal-entry date. A journal entry posted at 01:00 in
 * Taiz would have been booked into the previous accounting day — which may already be closed.
 *
 * Filenames are exempt: a CSV named after yesterday is untidy, not wrong.
 */

const SRC = resolve(__dirname, "../../");

function walk(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      if (entry === "__tests__" || entry === "node_modules") continue;
      walk(full, out);
    } else if (/\.(ts|tsx)$/.test(entry)) {
      out.push(full);
    }
  }
  return out;
}

describe("clinic day", () => {
  it("no screen derives today's date from UTC", () => {
    const offenders: string[] = [];

    for (const file of walk(SRC)) {
      const text = readFileSync(file, "utf8");
      text.split("\n").forEach((line, i) => {
        if (!/toISOString\(\)\s*\.\s*(slice\(\s*0\s*,\s*10\s*\)|split\(["']T["']\))/.test(line)) return;
        // A comment naming the forbidden pattern is documentation, not a use of it —
        // localDateString's own docstring warns against exactly this string.
        const code = line.trim();
        if (code.startsWith("//") || code.startsWith("*") || code.startsWith("/*")) return;
        // A download filename is cosmetic; a stored or submitted date is not.
        if (/download|filename|\.csv|\.svg|\.pdf/i.test(line)) return;
        offenders.push(`${file.replace(SRC, "src")}:${i + 1}`);
      });
    }

    expect(
      offenders,
      "use localDateString() — Yemen is UTC+3, so after midnight the UTC date is still " +
        `yesterday and the value lands in the wrong clinic day:\n${offenders.join("\n")}`,
    ).toEqual([]);
  });
});
