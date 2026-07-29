import { test, expect } from "@playwright/test";

/**
 * Patient portal login golden-path e2e spec (TEST-16).
 *
 * Flow: navigate to /portal/login → fill username + password → submit →
 * assert redirect to /portal (the patient portal home).
 *
 * Env-var driven (no hardcoded credentials):
 *   - E2E_API_URL          — staging frontend URL (also Playwright `baseURL`).
 *                            When unset, the whole suite skips so CI stays
 *                            green without a staging deployment.
 *   - E2E_PORTAL_USERNAME  — patient portal username (file number, e.g. GM0001).
 *   - E2E_PORTAL_PASSWORD  — patient portal password.
 *
 * Wired from GitHub secrets in the `e2e` job in `.github/workflows/ci.yml`.
 */

const apiUrl = process.env.E2E_API_URL;
const portalUsername = process.env.E2E_PORTAL_USERNAME;
const portalPassword = process.env.E2E_PORTAL_PASSWORD;

test.skip(!apiUrl, "E2E_API_URL not set — skipping e2e (no staging frontend).");
test.skip(
  !portalUsername || !portalPassword,
  "E2E_PORTAL_USERNAME / E2E_PORTAL_PASSWORD not set — skipping portal e2e (no portal credentials)."
);

test("patient can log in to the portal and reach /portal", async ({ page }) => {
  if (!apiUrl || !portalUsername || !portalPassword) {
    // TS guard — `test.skip` above already handles runtime skip.
    return;
  }

  // 1. Open the patient portal login page.
  await page.goto("/portal/login");
  await page.waitForLoadState("networkidle");

  // 2. The portal login uses controlled inputs without `name` attributes, so
  //    target by placeholder + password input type.
  await page.getByPlaceholder("GM0001").fill(portalUsername);
  await page.locator('input[type="password"]').fill(portalPassword);

  // 3. Submit — the button text is "تسجيل الدخول".
  await page.getByRole("button", { name: "تسجيل الدخول" }).click();

  // 4. Assert redirect to /portal (portal home).
  await page.waitForURL((url) => url.pathname.startsWith("/portal") && url.pathname !== "/portal/login", {
    timeout: 30000,
  });

  expect(new URL(page.url()).pathname.startsWith("/portal")).toBe(true);
});
