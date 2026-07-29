import { describe, expect, it } from "vitest";
import { resolveApiBaseUrl } from "@/lib/apiClient";

describe("resolveApiBaseUrl", () => {
  const publicApi = "https://aqlan-dental-production.up.railway.app";

  it("uses the same-origin proxy on generated Vercel preview domains", () => {
    expect(
      resolveApiBaseUrl(
        publicApi,
        "aqlan-dental-git-claude-reports-example.vercel.app"
      )
    ).toBe("");
  });

  it.each([
    "aqlan-dental.vercel.app",
    "aqlan-dental-pro.vercel.app",
    "localhost",
  ])("keeps the configured API URL on %s", (hostname) => {
    expect(resolveApiBaseUrl(publicApi, hostname)).toBe(publicApi);
  });
});
