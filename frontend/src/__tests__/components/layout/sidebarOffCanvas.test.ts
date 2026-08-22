import { describe, it, expect } from "vitest";
import { execFileSync } from "node:child_process";
import { mkdtempSync, readFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import postcss from "postcss";

/**
 * The desktop sidebar must not be parked off-canvas.
 *
 * CORE-REQ-006 replaced the sidebar's physical `right-0` with logical `start-0`, and the
 * off-canvas transform with direction variants. translateX is physical, so those variants
 * were necessary — but Tailwind emits `rtl:*` after the responsive variants, and `:where()`
 * leaves both rules at equal specificity. The unscoped rtl rule therefore came last and beat
 * `lg:translate-x-0`, so the sidebar slid off-screen on desktop as well as mobile. Main's
 * whole navigation was gone, and CI surfaced it only as a Playwright click timing out with
 * "element is outside of the viewport".
 *
 * This asserts the compiled stylesheet rather than the class string, because the class
 * string read correctly the entire time — the defect existed only in the cascade.
 */
describe("sidebar off-canvas transform", () => {
  it("keeps every off-canvas transform below the lg breakpoint", () => {
    const frontend = resolve(__dirname, "../../../..");
    const out = join(mkdtempSync(join(tmpdir(), "twcss-")), "sidebar.css");

    execFileSync(
      "npx",
      ["tailwindcss", "--input", "./src/app/globals.css",
       "--content", "./src/components/layout/Sidebar.tsx", "-o", out],
      { cwd: frontend, stdio: "pipe" },
    );

    const root = postcss.parse(readFileSync(out, "utf8"));

    let sawDesktopOverride = false;
    const unscoped: string[] = [];

    root.walkRules((rule) => {
      if (rule.selector.includes("lg\\:translate-x-0")) sawDesktopOverride = true;
      if (!/translate-x-(full|\[100)/.test(rule.selector)) return;

      // Walk up: the rule is safe only if some ancestor query excludes desktop widths.
      let scopedBelowLg = false;
      for (let node = rule.parent; node; node = (node as { parent?: unknown }).parent as never) {
        const params = (node as { params?: string }).params;
        if (params && /max-width|not all and \(min-width/.test(params)) scopedBelowLg = true;
      }
      if (!scopedBelowLg) unscoped.push(rule.selector);
    });

    expect(sawDesktopOverride, "the compile must have produced lg:translate-x-0").toBe(true);
    expect(unscoped, "this transform still applies on desktop and hides the sidebar").toEqual([]);
  });
});
