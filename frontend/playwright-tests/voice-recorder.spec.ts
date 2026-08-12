import { test, expect } from "@playwright/test";

/**
 * VoiceRecorder visibility e2e spec.
 *
 * CORE-F-015 — this spec used to open /messages and click whatever conversation happened to
 * be there. Against a staging database with history that worked; against a brand-new instance
 * there is nothing to click, so the assertion failed for want of data rather than for want of
 * a working recorder. Worse, the whole suite skipped for want of credentials, so nobody could
 * see either outcome — and the spec had meanwhile rotted into a strict-mode failure on the
 * login page without anyone noticing.
 *
 * It now creates the conversation it needs through the API before touching the UI. That makes
 * it independent of what is in the database, repeatable, and honest: if the recorder is
 * missing the test fails, and it can no longer pass or skip for unrelated reasons.
 *
 * Requires `E2E_BACKEND_URL` so the fixture can be created. The ephemeral CI stack supplies it.
 */

const apiUrl = process.env.E2E_API_URL;
const backendUrl = process.env.E2E_BACKEND_URL;
const staffPhone = process.env.E2E_STAFF_PHONE;
const staffPassword = process.env.E2E_STAFF_PASSWORD;

test.skip(!apiUrl, "E2E_API_URL not set — no frontend under test.");
test.skip(!staffPhone || !staffPassword, "Staff credentials not set.");
test.skip(
  !backendUrl,
  "E2E_BACKEND_URL not set — this spec builds its own conversation and will not fall back to " +
    "whatever data happens to exist.",
);

/** Seeded orthodontist. Deterministic by design in DbSeeder, so the fixture needs no lookup. */
const SEEDED_DOCTOR_USER_ID = "20000000-0000-0000-0000-000000000002";

const FIXTURE_TITLE = "E2E — voice recorder fixture";

/** How the seeded orthodontist appears in the conversation list. */
const SEEDED_DOCTOR_DISPLAY_NAME = "د. عقلان الكامل";

test("VoiceRecorder mic button is visible in a conversation this test creates", async ({ page, request }) => {
  if (!apiUrl || !backendUrl || !staffPhone || !staffPassword) return;

  // ── Fixture: a conversation of our own, created through the real API ──────────
  const loginResponse = await request.post(`${backendUrl}/api/auth/login`, {
    data: { username: staffPhone, password: staffPassword },
  });
  expect(loginResponse.ok(), "the fixture needs a working staff login").toBeTruthy();
  const { accessToken } = await loginResponse.json();

  const conversationResponse = await request.post(`${backendUrl}/api/messages/conversations`, {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: {
      title: FIXTURE_TITLE,
      isGroup: false,
      participantIds: [SEEDED_DOCTOR_USER_ID],
      initialMessage: "fixture message",
      conversationType: "StaffToStaff",
    },
  });
  expect(
    conversationResponse.status(),
    "the fixture conversation must be created, not assumed",
  ).toBe(201);
  const conversation = await conversationResponse.json();
  expect(conversation.id).toBeTruthy();

  // ── UI: log in and open a conversation ───────────────────────────────────────
  await page.goto("/login");
  await page.waitForLoadState("networkidle");
  await page.fill('input[name="username"]', staffPhone);
  await page.fill('input[name="password"]', staffPassword);
  // The login page carries two submit buttons — staff and patient portal — so this must be
  // targeted by name.
  await page.getByRole("button", { name: "دخول إلى لوحة التحكم" }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/login"), { timeout: 30_000 });

  // Navigate IN-APP rather than with page.goto. A full navigation discards the in-memory
  // access token (W04) and the silent refresh cannot recover it here: the refresh cookie is
  // Secure, and the ephemeral stack runs over plain HTTP, so the browser never sends it. A
  // page.goto would therefore land on /login and this spec would be testing the login page.
  await page.getByRole("link", { name: "الرسائل" }).first().click();
  await page.waitForURL((url) => url.pathname.startsWith("/messages"), { timeout: 30_000 });
  await page.waitForLoadState("networkidle");

  // A one-to-one thread is titled after the OTHER participant, not the title we supplied, so
  // the fixture is located by the seeded doctor's display name. Targeting the thread this way
  // rather than "whatever is first in the list" means the test cannot quietly pass against
  // some unrelated conversation.
  // The list row is a button; matching the bare text hits a node inside it, and the click
  // then does nothing while the right-hand pane keeps saying "اختر محادثة للبدء".
  const thread = page.getByRole("button", { name: new RegExp(SEEDED_DOCTOR_DISPLAY_NAME) }).first();
  await expect(thread, "the conversation created above should be listed").toBeVisible({
    timeout: 15_000,
  });
  await thread.click();

  const voiceButton = page.getByTestId("voice-recorder-button");
  await expect(voiceButton).toBeVisible({ timeout: 15_000 });
});
