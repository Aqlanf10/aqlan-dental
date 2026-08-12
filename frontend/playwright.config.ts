import { defineConfig, devices } from "@playwright/test";

/**
 * Playwright configuration for Aqlan Dental Pro e2e tests (TEST-16).
 *
 * Design notes:
 * - `baseURL` comes from `E2E_API_URL` (the deployed staging frontend URL in
 *   CI). Defaults to `http://localhost:3000` for local dev. Tests MUST call
 *   `test.skip()` when `E2E_API_URL` is unset so CI stays green without a
 *   staging env.
 * - `retries: 1` only in CI (per audit recommendation).
 * - HTML + list reporters; HTML report is uploaded as a CI artifact on
 *   failure / always.
 * - `trace: "on-first-retry"` — captures trace only when a test fails first
 *   attempt, keeping the report bundle small.
 *
 * See `playwright-tests/*.spec.ts` for the specs.
 */
const CI = !!process.env.CI;
const baseURL = process.env.E2E_API_URL || "http://localhost:3000";

export default defineConfig({
  testDir: "./playwright-tests",
  fullyParallel: true,
  forbidOnly: !CI,
  retries: CI ? 1 : 0,
  workers: CI ? 1 : undefined,
  // CORE-P1-S5: the JSON report is what lets CI say how many tests actually ran. Without it
  // a run where every authenticated test skipped is indistinguishable, on the checks list,
  // from one where they all passed.
  reporter: CI
    ? [["html"], ["list"], ["json", { outputFile: "playwright-report/results.json" }]]
    : [["list"]],
  outputDir: "playwright-test-output",
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    locale: "ar-YE",
    timezoneId: "Asia/Aden",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});
