import { describe, expect, it } from "vitest";
import { shouldEnableRecaptcha } from "@/lib/recaptcha";

describe("shouldEnableRecaptcha", () => {
  const siteKey = "configured-site-key";

  it("disables the provider on generated Vercel preview domains", () => {
    expect(
      shouldEnableRecaptcha(
        siteKey,
        "aqlan-dental-git-codex-preview-example.vercel.app"
      )
    ).toBe(false);
  });

  it.each([
    "aqlan-dental.vercel.app",
    "aqlan-dental-pro.vercel.app",
    "localhost",
  ])("keeps the provider enabled on %s", (hostname) => {
    expect(shouldEnableRecaptcha(siteKey, hostname)).toBe(true);
  });

  it("disables the provider when no site key is configured", () => {
    expect(shouldEnableRecaptcha("", "aqlan-dental.vercel.app")).toBe(false);
  });
});
