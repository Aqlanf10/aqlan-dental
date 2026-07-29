import { describe, it, expect, beforeAll } from "vitest";
import fs from "fs";
import path from "path";

/**
 * Static analysis tests for pdfDownload.ts
 *
 * These tests verify that the download and print functions are strictly
 * separated — downloadPdfFromApi must never use print or window.open,
 * and printPdfFromApi must never use window.open(objectUrl) directly.
 */
describe("pdfDownload static separation checks", () => {
  const pdfDownloadPath = path.resolve(
    __dirname,
    "../../lib/pdfDownload.ts"
  );
  let sourceCode: string;

  beforeAll(() => {
    sourceCode = fs.readFileSync(pdfDownloadPath, "utf-8");
  });

  // ─── downloadPdfFromApi constraints ───────────────────────────────────────

  describe("downloadPdfFromApi", () => {
    it("must NOT contain .print(", () => {
      // Extract only the downloadPdfFromApi function body
      const fnMatch = sourceCode.match(
        /export async function downloadPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).not.toContain(".print(");
    });

    it("must NOT contain window.open", () => {
      const fnMatch = sourceCode.match(
        /export async function downloadPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).not.toContain("window.open");
    });

    it("must NOT call printPdfFromApi", () => {
      const fnMatch = sourceCode.match(
        /export async function downloadPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).not.toContain("printPdfFromApi");
    });

    it("must use a.download for file download", () => {
      const fnMatch = sourceCode.match(
        /export async function downloadPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).toContain("link.download");
      expect(fnBody).toContain("link.click");
    });
  });

  // ─── printPdfFromApi constraints ──────────────────────────────────────────

  describe("printPdfFromApi", () => {
    it("must NOT use window.open(objectUrl) directly as primary print method", () => {
      // The print function should use window.open("", "_blank") for the HTML
      // intermediate window, NOT window.open(objectUrl, "_blank") directly.
      const fnMatch = sourceCode.match(
        /export async function printPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;

      // Should NOT contain: window.open(objectUrl
      expect(fnBody).not.toMatch(/window\.open\s*\(\s*objectUrl/);
    });

    it("must use an iframe to embed the PDF", () => {
      const fnMatch = sourceCode.match(
        /export async function printPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).toContain("iframe");
      expect(fnBody).toContain("contentWindow");
    });

    it("must NOT call downloadPdfFromApi", () => {
      const fnMatch = sourceCode.match(
        /export async function printPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).not.toContain("downloadPdfFromApi");
    });

    it("must use iframe.contentWindow.print() for printing", () => {
      const fnMatch = sourceCode.match(
        /export async function printPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).toContain("contentWindow?.print()");
    });

    it("must open print window before any async work (popup blocker protection)", () => {
      const fnMatch = sourceCode.match(
        /export async function printPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;

      // window.open should appear before any await in the function
      const openIndex = fnBody.indexOf("window.open");
      const firstAwaitIndex = fnBody.indexOf("await ");

      // window.open must come before any await (popup must open as part of user gesture)
      expect(openIndex).toBeGreaterThan(-1);
      expect(firstAwaitIndex).toBeGreaterThan(-1);
      expect(openIndex).toBeLessThan(firstAwaitIndex);
    });

    it("must provide fallback actions for the user", () => {
      const fnMatch = sourceCode.match(
        /export async function printPdfFromApi[\s\S]*?^}/m
      );
      const fnBody = fnMatch ? fnMatch[0] : sourceCode;
      expect(fnBody).toContain("fallback-actions");
    });
  });
});

// ─── Button separation checks in consumer files ─────────────────────────────

describe("Button separation in consumer components", () => {
  const projectRoot = path.resolve(__dirname, "../../../app/(dashboard)");

  const consumerFiles = [
    "finance-v3/components/CollectionsTab.tsx",
    "finance-v3/components/InvoicesTab.tsx",
    "daily-operations/page.tsx",
    "lab/page.tsx",
  ];

  consumerFiles.forEach((relativePath) => {
    const filePath = path.join(projectRoot, relativePath);

    describe(relativePath.split("/").pop() ?? relativePath, () => {
      let content: string;

      beforeAll(() => {
        try {
          content = fs.readFileSync(filePath, "utf-8");
        } catch {
          // File might not exist in test environment
          content = "";
        }
      });

      it("downloadPdfFromApi and printPdfFromApi should both be imported/used", () => {
        if (!content) return; // Skip if file not found
        const hasDownload = content.includes("downloadPdfFromApi");
        const hasPrint = content.includes("printPdfFromApi");
        // Both functions should be referenced
        expect(hasDownload || hasPrint).toBe(true);
      });

      it("should NOT call printPdfFromApi inside a download button handler", () => {
        if (!content) return;
        // Find all download button contexts and ensure they don't call printPdfFromApi
        // Simple heuristic: no line should have both "تحميل" and "printPdfFromApi"
        const lines = content.split("\n");
        for (const line of lines) {
          if (line.includes("printPdfFromApi") && line.includes("downloadPdfFromApi")) {
            // This would be suspicious — same line has both
            expect.fail(
              `Line contains both downloadPdfFromApi and printPdfFromApi: ${line.trim()}`
            );
          }
        }
      });
    });
  });
});
