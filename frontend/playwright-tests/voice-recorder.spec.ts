import { test, expect } from "@playwright/test";

/**
 * VoiceRecorder visibility e2e spec (TEST-16 refactor).
 *
 * Previously this spec hardcoded a production Vercel URL + admin credentials
 * (audit finding: "Single Playwright spec hardens production URL + admin
 * password, NOT enabled in CI"). It now:
 *   - Reads the frontend base URL from `E2E_API_URL` (provided by the CI
 *     `e2e` job from a GitHub secret pointing at a staging deployment).
 *   - Reads staff credentials from `E2E_STAFF_PHONE` / `E2E_STAFF_PASSWORD`.
 *   - Skips entirely when `E2E_API_URL` is unset, so CI stays green without
 *     a staging env.
 *
 * The flow: log in as staff → open the dashboard messages page → assert the
 * voice-recorder button is present and visible inside an open conversation.
 */

const apiUrl = process.env.E2E_API_URL;
const staffPhone = process.env.E2E_STAFF_PHONE;
const staffPassword = process.env.E2E_STAFF_PASSWORD;

test.skip(!apiUrl, "E2E_API_URL not set — skipping e2e (no staging frontend).");
test.skip(
  !staffPhone || !staffPassword,
  "E2E_STAFF_PHONE / E2E_STAFF_PASSWORD not set — skipping e2e (no staff credentials)."
);

test("VoiceRecorder mic button is visible in dashboard messages", async ({ page }) => {
  if (!apiUrl || !staffPhone || !staffPassword) {
    // Guards for TS — `test.skip` above already handles runtime skip.
    return;
  }

  // 1. Staff login.
  await page.goto(`${apiUrl}/login`);
  await page.waitForLoadState("networkidle");

  await page.fill('input[name="username"]', staffPhone);
  await page.fill('input[name="password"]', staffPassword);
  await page.click('button[type="submit"]');
  await page.waitForLoadState("networkidle");

  // 2. Navigate to messages.
  await page.goto(`${apiUrl}/messages`);
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(2000);

  // 3. Open the first conversation (the voice recorder button only renders
  //    inside an active conversation thread).
  const firstConv = page.locator('[data-testid="conversation-item"], .cursor-pointer').first();
  if (await firstConv.isVisible().catch(() => false)) {
    await firstConv.click();
    await page.waitForTimeout(2000);
  }

  // 4. Assert the voice-recorder button is present + visible.
  const voiceBtn = page.getByTestId("voice-recorder-button");
  await expect(voiceBtn).toBeVisible({ timeout: 10000 });
});
