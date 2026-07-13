import { test, expect } from "@playwright/test";

const apiUrl = process.env.E2E_API_URL;

test.skip(!apiUrl, "E2E_API_URL not set — skipping deployed frontend smoke QA.");

test("deployed frontend serves the staff login surface", async ({ page }) => {
  const response = await page.goto("/login");

  expect(response?.ok()).toBe(true);
  await expect(page.locator('input[name="username"]')).toBeVisible();
  await expect(page.locator('input[name="password"]')).toBeVisible();
  await expect(page.locator('button[type="submit"]').first()).toBeVisible();
});
