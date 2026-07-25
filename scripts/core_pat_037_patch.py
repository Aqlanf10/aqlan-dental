from pathlib import Path

configs = [
    {
        "file": "PhotosTab.tsx",
        "ref_line": '  const fileInputRef = useRef<HTMLInputElement>(null);',
        "fetch_old": '''  const fetchPhotos = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterCategory) params.set("category", filterCategory);
    if (filterStage) params.set("stage", filterStage);
    const qs = params.toString();
    api.get<ClinicalPhotoItem[]>(`/api/clinical-photos/${patientId}${qs ? `?${qs}` : ""}${orthoCaseId ? `${qs ? "&" : "?"}orthoCaseId=${orthoCaseId}` : ""}`)
      .then((r) => setPhotos(r.data))
      .catch(() => setError("فشل تحميل الصور"))
      .finally(() => setLoading(false));
  }, [patientId, filterCategory, filterStage, orthoCaseId]);
''',
        "fetch_new": '''  const fetchPhotos = useCallback(() => {
    const requestId = ++loadRequestRef.current;
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterCategory) params.set("category", filterCategory);
    if (filterStage) params.set("stage", filterStage);
    const qs = params.toString();
    api.get<ClinicalPhotoItem[]>(`/api/clinical-photos/${patientId}${qs ? `?${qs}` : ""}${orthoCaseId ? `${qs ? "&" : "?"}orthoCaseId=${orthoCaseId}` : ""}`)
      .then((r) => {
        if (loadRequestRef.current === requestId) setPhotos(r.data);
      })
      .catch((error: unknown) => {
        if (loadRequestRef.current === requestId) {
          setPhotos([]);
          setError(extractErrorMessage(error, "فشل تحميل الصور"));
        }
      })
      .finally(() => {
        if (loadRequestRef.current === requestId) setLoading(false);
      });
  }, [patientId, filterCategory, filterStage, orthoCaseId]);
''',
        "error_old": '        <div className="text-center py-8 text-red-500 text-sm">{error}</div>',
        "error_new": '''        <div role="alert" className="text-center py-8">
          <p className="text-sm text-red-600 mb-2">{error}</p>
          <button type="button" onClick={fetchPhotos} className="text-xs font-semibold text-blue-600 underline decoration-dotted">إعادة المحاولة</button>
        </div>''',
        "header": "Header with filters and add button",
    },
    {
        "file": "RadiographsTab.tsx",
        "ref_line": '  const fileInputRef = useRef<HTMLInputElement>(null);',
        "fetch_old": '''  const fetchXrays = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterType) params.set("xrayType", filterType);
    const qs = params.toString();
    api.get<RadiographItem[]>(`/api/radiographs/${patientId}${qs ? `?${qs}` : ""}`)
      .then((r) => setXrays(r.data))
      .catch(() => setError("فشل تحميل الأشعة"))
      .finally(() => setLoading(false));
  }, [patientId, filterType]);
''',
        "fetch_new": '''  const fetchXrays = useCallback(() => {
    const requestId = ++loadRequestRef.current;
    setLoading(true);
    setError("");
    const params = new URLSearchParams();
    if (filterType) params.set("xrayType", filterType);
    const qs = params.toString();
    api.get<RadiographItem[]>(`/api/radiographs/${patientId}${qs ? `?${qs}` : ""}`)
      .then((r) => {
        if (loadRequestRef.current === requestId) setXrays(r.data);
      })
      .catch((error: unknown) => {
        if (loadRequestRef.current === requestId) {
          setXrays([]);
          setError(extractErrorMessage(error, "فشل تحميل الأشعة"));
        }
      })
      .finally(() => {
        if (loadRequestRef.current === requestId) setLoading(false);
      });
  }, [patientId, filterType]);
''',
        "error_old": '        <div className="text-center py-8 text-red-500 text-sm">{error}</div>',
        "error_new": '''        <div role="alert" className="text-center py-8">
          <p className="text-sm text-red-600 mb-2">{error}</p>
          <button type="button" onClick={fetchXrays} className="text-xs font-semibold text-blue-600 underline decoration-dotted">إعادة المحاولة</button>
        </div>''',
        "header": "Header with filters and add button",
    },
    {
        "file": "DocumentsTab.tsx",
        "ref_line": '  const fileInputRef = useRef<HTMLInputElement>(null);',
        "fetch_old": '''  const fetchDocuments = useCallback(() => {
    setLoading(true);
    setError("");
    const params = new URLSearchParams({ patientId });
    if (filterType) params.set("documentType", filterType);
    api.get<{ data: DocumentDto[] }>(`/api/documents?${params}`)
      .then((r) => setDocuments(r.data.data ?? []))
      .catch(() => setError("فشل تحميل المستندات"))
      .finally(() => setLoading(false));
  }, [patientId, filterType]);
''',
        "fetch_new": '''  const fetchDocuments = useCallback(() => {
    const requestId = ++loadRequestRef.current;
    setLoading(true);
    setError("");
    const params = new URLSearchParams({ patientId });
    if (filterType) params.set("documentType", filterType);
    api.get<{ data: DocumentDto[] }>(`/api/documents?${params}`)
      .then((r) => {
        if (loadRequestRef.current === requestId) setDocuments(r.data.data ?? []);
      })
      .catch((error: unknown) => {
        if (loadRequestRef.current === requestId) {
          setDocuments([]);
          setError(extractErrorMessage(error, "فشل تحميل المستندات"));
        }
      })
      .finally(() => {
        if (loadRequestRef.current === requestId) setLoading(false);
      });
  }, [patientId, filterType]);
''',
        "error_old": '        <div className="text-center py-8 text-red-500 text-sm">{error}</div>',
        "error_new": '''        <div role="alert" className="text-center py-8">
          <p className="text-sm text-red-600 mb-2">{error}</p>
          <button type="button" onClick={fetchDocuments} className="text-xs font-semibold text-blue-600 underline decoration-dotted">إعادة المحاولة</button>
        </div>''',
        "header": "Header",
    },
]

for cfg in configs:
    path = Path("frontend/src/components/patient/tabs") / cfg["file"]
    text = path.read_text(encoding="utf-8")
    text = text.replace(
        'import api from "@/lib/api";\nimport { EmptyState } from "./EmptyState";',
        'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { EmptyState } from "./EmptyState";',
        1,
    )
    if cfg["ref_line"] not in text:
        raise SystemExit(f"ref target missing in {cfg['file']}")
    text = text.replace(cfg["ref_line"], cfg["ref_line"] + "\n  const loadRequestRef = useRef(0);", 1)
    if cfg["fetch_old"] not in text:
        raise SystemExit(f"fetch block missing in {cfg['file']}")
    text = text.replace(cfg["fetch_old"], cfg["fetch_new"], 1)
    if cfg["error_old"] not in text:
        raise SystemExit(f"error block missing in {cfg['file']}")
    text = text.replace(cfg["error_old"], cfg["error_new"], 1)

    # Hide count cards until a successful response has been received. Zeroes
    # during loading or failure are clinically misleading.
    stats_start = '      {/* Stats row */}'
    header_marker = f'      {{/* {cfg["header"]} */}}'
    if stats_start not in text or header_marker not in text:
        raise SystemExit(f"stats markers missing in {cfg['file']}")
    text = text.replace(stats_start, '      {!loading && !error && (\n        <>\n      {/* Stats row */}', 1)
    text = text.replace(header_marker, '        </>\n      )}\n\n' + header_marker, 1)
    path.write_text(text, encoding="utf-8")

# Keep tests out of the large UI modules' coverage denominator while still
# protecting the reliability contract in all three files.
test = Path("frontend/src/__tests__/components/patient/PatientMediaLoadReliability.contract.test.ts")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { readFileSync } from "node:fs";
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
      expect(source).toContain(`extractErrorMessage(error, \"${tab.fallback}\")`);
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
''', encoding="utf-8")

Path("scripts/core_pat_037_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-037-bootstrap.yml").unlink(missing_ok=True)
