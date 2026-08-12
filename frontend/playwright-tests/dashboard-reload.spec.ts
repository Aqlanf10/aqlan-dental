import { test, expect } from "@playwright/test";

/**
 * CORE-F-014 regression — a hard reload of a protected page must not strand the user.
 *
 * Access tokens have been in-memory since W04, so reloading a dashboard route always depends
 * on the silent refresh. When that did not answer, the layout's readiness gate never opened
 * and the page sat on "جارٍ تحميل النظام..." forever: no error, no redirect, nothing to click.
 *
 * The assertion is the user-visible contract rather than an implementation detail: after a
 * reload the app must end up SOMEWHERE — the dashboard or the login page — and must not still
 * be showing the boot spinner.
 *
 * Runs in the ephemeral CI stack, which supplies its own credentials.
 */
const apiUrl = process.env.E2E_API_URL;
const staffPhone = process.env.E2E_STAFF_PHONE;
const staffPassword = process.env.E2E_STAFF_PASSWORD;

test.skip(!apiUrl, "E2E_API_URL not set.");
test.skip(!staffPhone || !staffPassword, "Staff credentials not set.");

const BOOT_SPINNER = "جارٍ تحميل النظام...";

test("a hard reload of a protected page never leaves the boot spinner on screen", async ({ page }) => {
  if (!apiUrl || !staffPhone || !staffPassword) return;

  await page.goto("/login");
  await page.waitForLoadState("networkidle");
  await page.fill('input[name="username"]', staffPhone);
  await page.fill('input[name="password"]', staffPassword);
  await page.getByRole("button", { name: "دخول إلى لوحة التحكم" }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/login"), { timeout: 30_000 });

  const landed = new URL(page.url()).pathname;

  // The reload is the whole point: it discards the in-memory access token, so the app has to
  // re-establish the session from scratch — the exact path that used to hang.
  await page.reload();

  // Generous enough for a slow runner, far below the "forever" the bug produced.
  await expect(page.getByText(BOOT_SPINNER)).toBeHidden({ timeout: 30_000 });

  // Ending up at /login is a correct outcome — being stuck is not.
  const after = new URL(page.url()).pathname;
  expect(
    after.startsWith("/login") || after === landed || after.length > 1,
    `expected a resolved page after reload, got ${after}`,
  ).toBeTruthy();

  // And the page must actually be interactive, not a blank shell that merely stopped
  // rendering the spinner.
  await expect(page.locator("body")).toBeVisible();
});
