import { test, expect, type Page } from "@playwright/test";

const apiUrl = process.env.E2E_API_URL;
const staffPhone = process.env.E2E_STAFF_PHONE;
const staffPassword = process.env.E2E_STAFF_PASSWORD;

test.skip(!apiUrl, "E2E_API_URL not set — skipping authenticated ceph runtime QA.");

interface CephListRecord {
  id: string;
  orthoCaseId: string;
  analysisType: string;
  landmarkCount: number;
  hasMeasurements: boolean;
  isApproved: boolean;
}

async function login(page: Page) {
  expect(staffPhone, "Set E2E_STAFF_PHONE to run the required authenticated ceph QA.").toBeTruthy();
  expect(staffPassword, "Set E2E_STAFF_PASSWORD to run the required authenticated ceph QA.").toBeTruthy();
  await page.goto("/login");
  await page.locator('input[name="username"]').fill(staffPhone!);
  await page.locator('input[name="password"]').fill(staffPassword!);
  await page.locator('button[type="submit"]').click();
  await page.waitForURL(url => !url.pathname.startsWith("/login"), { timeout: 30_000 });
}

test("authenticated ceph workflow, responsive surfaces, and eligible exports", async ({ page }) => {
  test.setTimeout(120_000);
  await login(page);
  test.skip(new URL(page.url()).pathname.startsWith("/change-password"), "The staging staff account must change its password first.");

  const listResponsePromise = page.waitForResponse(response => {
    const url = new URL(response.url());
    return response.request().method() === "GET" && url.pathname.endsWith("/api/ceph") && !url.searchParams.has("orthoCaseId");
  });
  await page.goto("/ceph");
  await expect(page.getByRole("heading", { name: "السيفالومتري" })).toBeVisible();
  const listResponse = await listResponsePromise;
  expect(listResponse.status()).toBe(200);
  const analyses = await listResponse.json() as CephListRecord[];

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole("heading", { name: "السيفالومتري" })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
  await page.setViewportSize({ width: 1440, height: 1000 });

  const cohortResponsePromise = page.waitForResponse(response => new URL(response.url()).pathname.endsWith("/api/ceph/cohort"));
  await page.goto("/ceph/cohort");
  await expect(page.getByRole("heading", { name: "تحليل مجموعات السيفالو" })).toBeVisible();
  expect((await cohortResponsePromise).status()).toBe(200);
  await expect(page.getByText("حد الخصوصية: 5 مرضى")).toBeVisible();

  const first = analyses[0];
  if (first) {
    const caseResponsePromise = page.waitForResponse(response => {
      const url = new URL(response.url());
      return url.pathname.endsWith("/api/ceph") && url.searchParams.get("orthoCaseId") === first.orthoCaseId;
    });
    await page.goto(`/ceph/case/${first.orthoCaseId}`);
    await expect(page.getByRole("heading", { name: "مراجعة حالة السيفالو" })).toBeVisible();
    expect((await caseResponsePromise).status()).toBe(200);

    await page.goto(`/ceph/timelapse/${first.orthoCaseId}`);
    await expect(page.getByRole("heading", { name: /Timelapse السجلات السريرية/ })).toBeVisible();
    await expect(page.getByText(/دون إنشاء إطارات أو حركة علاجية وسيطة/)).toBeVisible();
  }

  const exportable = analyses.find(record =>
    record.isApproved
    && record.hasMeasurements
    && record.landmarkCount >= (record.analysisType === "pa" ? 15 : 24));
  if (exportable) {
    await page.goto(`/ceph/${exportable.id}`);
    const csvButton = page.getByRole("button", { name: "تصدير القياسات CSV" });
    const pdfButton = page.getByRole("button", { name: "تحميل التقرير PDF" });
    await expect(csvButton).toBeEnabled();
    await expect(pdfButton).toBeEnabled();

    const csvDownload = page.waitForEvent("download");
    await csvButton.click();
    await expect((await csvDownload).suggestedFilename()).toMatch(/\.csv$/);

    const pdfDownload = page.waitForEvent("download");
    await pdfButton.click();
    await expect((await pdfDownload).suggestedFilename()).toMatch(/\.pdf$/);
  }
});
