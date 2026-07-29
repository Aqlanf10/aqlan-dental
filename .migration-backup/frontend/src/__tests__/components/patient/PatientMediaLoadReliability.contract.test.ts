import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const tabs = [
  { file: "PhotosTab.tsx", retry: "fetchPhotos", fallback: "فشل تحميل الصور", state: "setPhotos([])" },
  { file: "RadiographsTab.tsx", retry: "fetchXrays", fallback: "فشل تحميل الأشعة", state: "setXrays([])" },
  { file: "DocumentsTab.tsx", retry: "fetchDocuments", fallback: "فشل تحميل المستندات", state: "setDocuments([])" },
];

describe("patient media loading reliability contract", () => {
  for (const tab of tabs) {
    const source = readFileSync(
      resolve(process.cwd(), `src/components/patient/tabs/${tab.file}`),
      "utf8"
    );

    it(`${tab.file} rejects stale responses and exposes a retry`, () => {
      expect(source).toContain("const requestId = ++loadRequestRef.current");
      expect(source).toContain("loadRequestRef.current === requestId");
      expect(source).toContain(`extractErrorMessage(error, "${tab.fallback}")`);
      expect(source).toContain(tab.state);
      expect(source).toContain(`onClick={${tab.retry}}`);
      expect(source).toContain('role="alert"');
    });

    it(`${tab.file} hides zero statistics during loading and errors`, () => {
      expect(source).toContain("{!loading && !error && (");
      expect(source.indexOf("{!loading && !error && (")).toBeLessThan(source.indexOf("{/* Stats row */}"));
    });
  }
});
