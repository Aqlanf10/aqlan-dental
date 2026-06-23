import { test, expect } from "@playwright/test";

/**
 * Staff login golden-path e2e spec (TEST-16).
 *
 * Flow: navigate to /login → fill username + password → submit → assert the
 * browser is redirected off /login to a dashboard route (role-based default
 * under /daily-operations | /doctor-clinic | /finance-v3, or /change-password
 * if must-change-password is set on the staging account).
 *
 * Env-var driven (no hardcoded credentials):
 *   - E2E_API_URL        — staging frontend URL (also set as Playwright
 *                          `baseURL`). When unset, the whole suite skips so
 *                          CI stays green without a staging deployment.
 *   - E2E_STAFF_PHONE    — staff username/phone (the login form's "username"
 *                          field accepts either a username or a phone number).
 *   - E2E_STAFF_PASSWORD — staff password.
 *
 * These are wired from GitHub secrets in the `e2e` job in `.github/workflows/ci.yml`.
 */

const apiUrl = process.env.E2E_API_URL;
const staffPhone = process.env.E2E_STAFF_PHONE;
const staffPassword = process.env.E2E_STAFF_PASSWORD;

test.skip(!apiUrl, "E2E_API_URL not set — skipping e2e (no staging frontend).");
test.skip(
  !staffPhone || !staffPassword,
  "E2E_STAFF_PHONE / E2E_STAFF_PASSWORD not set — skipping e2e (no staff credentials)."
);

test("staff can log in and reach a dashboard route", async ({ page }) => {
  if (!apiUrl || !staffPhone || !staffPassword) {
    // TS guard — `test.skip` above already handles runtime skip.
    return;
  }

  // 1. Open the staff login page.
  await page.goto("/login");
  await page.waitForLoadState("networkidle");

  // 2. Fill credentials + submit. The form's username field is registered
  //    via react-hook-form as "username" and accepts username OR phone.
  await page.fill('input[name="username"]', staffPhone);
  await page.fill('input[name="password"]', staffPassword);
  await page.click('button[type="submit"]');

  // 3. Assert redirect away from /login to a dashboard route. Role-based
  //    defaults: Admin/Reception/Assistant/BranchManager → /daily-operations,
  //    Doctors → /doctor-clinic, Accountant → /finance-v3. If the staging
  //    account has must-change-password set, it goes to /change-password.
  await page.waitForURL((url) => !url.pathname.startsWith("/login"), {
    timeout: 30000,
  });

  const pathname = new URL(page.url()).pathname;
  const dashboardRoutes = [
    "/daily-operations",
    "/doctor-clinic",
    "/finance-v3",
    "/change-password",
  ];
  expect(
    dashboardRoutes.some((r) => pathname.startsWith(r)),
    `Expected a dashboard route, got ${pathname}`
  ).toBe(true);
});
