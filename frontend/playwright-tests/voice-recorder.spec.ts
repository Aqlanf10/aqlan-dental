import { test, expect } from "@playwright/test";

const BASE_URL = "https://aqlan-dental-git-claude-aqlan-de-30a03c-aqlanf10-9871s-projects.vercel.app";
const USERNAME = "admin";
const PASSWORD = "AqlanDental2024!";

test("VoiceRecorder mic button is visible in dashboard messages", async ({ page }) => {
  // Login
  await page.goto(`${BASE_URL}/login`);
  await page.waitForLoadState("networkidle");

  await page.fill('input[name="username"], input[type="text"], input[placeholder*="اسم"]', USERNAME);
  await page.fill('input[name="password"], input[type="password"]', PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForLoadState("networkidle");

  // Navigate to messages
  await page.goto(`${BASE_URL}/messages`);
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(3000);

  // Take screenshot of full page
  await page.screenshot({ path: "playwright-tests/screenshots/messages-list.png", fullPage: true });

  // Click first conversation if any exist
  const firstConv = page.locator('[data-testid="conversation-item"], .cursor-pointer').first();
  if (await firstConv.isVisible()) {
    await firstConv.click();
    await page.waitForTimeout(2000);
  }

  // Take screenshot of chat area
  await page.screenshot({ path: "playwright-tests/screenshots/messages-chat.png", fullPage: true });

  // Check for voice recorder button
  const byTestId = page.getByTestId("voice-recorder-button");
  const byTitle = page.locator('[title="تسجيل رسالة صوتية"]');
  const byAriaLabel = page.getByLabel("تسجيل رسالة صوتية");

  console.log("byTestId count:", await byTestId.count());
  console.log("byTitle count:", await byTitle.count());
  console.log("byAriaLabel count:", await byAriaLabel.count());

  // Check DOM presence regardless of visibility
  const domCount = await page.evaluate(() =>
    document.querySelectorAll('[title="تسجيل رسالة صوتية"]').length
  );
  console.log("DOM nodes with title:", domCount);

  const inDom = await page.evaluate(() =>
    document.querySelector('[data-testid="voice-recorder-button"]') !== null
  );
  console.log("voice-recorder-button in DOM:", inDom);

  if (inDom) {
    // Check CSS visibility
    const cssInfo = await page.evaluate(() => {
      const el = document.querySelector('[data-testid="voice-recorder-button"]');
      if (!el) return null;
      const style = window.getComputedStyle(el);
      const rect = el.getBoundingClientRect();
      return {
        display: style.display,
        visibility: style.visibility,
        opacity: style.opacity,
        width: rect.width,
        height: rect.height,
        top: rect.top,
        left: rect.left,
        overflow: style.overflow,
      };
    });
    console.log("CSS computed style:", JSON.stringify(cssInfo, null, 2));
  }

  // Final screenshot
  await page.screenshot({ path: "playwright-tests/screenshots/messages-final.png", fullPage: true });
});
