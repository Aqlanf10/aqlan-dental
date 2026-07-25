from pathlib import Path
import re

path = Path("frontend/src/components/patients/PatientTable.tsx")
text = path.read_text(encoding="utf-8")


def replace_once(pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    global text
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-022: expected one {label} match, found {count}")
    text = updated


replace_once(
    re.escape('import { GENDER_LABELS, formatPhoneForWhatsApp, normalizePhone } from "@/lib/utils";\n'),
    'import { GENDER_LABELS, formatPhoneForWhatsApp, normalizePhone } from "@/lib/utils";\n'
    'import { getPatientPaginationItems } from "@/lib/patientTablePagination";\n',
    label="pagination import",
)

replace_once(
    re.escape('  const [loading, setLoading] = useState(true);\n  const [loadError, setLoadError] = useState(false);\n'),
    '  const [loading, setLoading] = useState(true);\n'
    '  const [loadError, setLoadError] = useState(false);\n'
    '  const [exporting, setExporting] = useState(false);\n'
    '  const patientRequestIdRef = useRef(0);\n',
    label="patient table state",
)

replace_once(
    r"  const fetchPatients = useCallback\(async \(\) => \{.*?^  \}, \[page, search, gender, doctorId, status\]\);",
    '''  const fetchPatients = useCallback(async () => {
    const requestId = ++patientRequestIdRef.current;
    setLoading(true);
    setLoadError(false);
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: "20", status });
      if (search) params.set("search", search);
      if (gender) params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(
        `/api/patients?${params}`
      );
      if (requestId !== patientRequestIdRef.current) return;
      setData(res);
    } catch (error) {
      if (requestId !== patientRequestIdRef.current) return;
      setLoadError(true);
      toast.error(extractErrorMessage(error, "تعذر تحميل بيانات المرضى حالياً"));
    } finally {
      if (requestId === patientRequestIdRef.current) {
        setLoading(false);
      }
    }
  }, [page, search, gender, doctorId, status]);''',
    flags=re.MULTILINE | re.DOTALL,
    label="fetchPatients block",
)

replace_once(
    r"  const handleExport = async \(\) => \{.*?^  \};",
    '''  const handleExport = async () => {
    if (exporting) return;
    setExporting(true);
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "1000", status });
      if (search) params.set("search", search);
      if (gender) params.set("gender", gender);
      if (doctorId) params.set("doctorId", doctorId);
      const { data: res } = await api.get<PaginatedResponse<PatientListItem>>(`/api/patients?${params}`);
      exportCsv(res.data);
      toast.success(`تم تصدير ${res.data.length} مريض`);
    } catch (error) {
      toast.error(extractErrorMessage(error, "تعذر تصدير قائمة المرضى حالياً"));
    } finally {
      setExporting(false);
    }
  };''',
    flags=re.MULTILINE | re.DOTALL,
    label="handleExport block",
)

replace_once(
    r'''            <button\n              onClick=\{handleExport\}.*?^            </button>''',
    '''            <button
              onClick={handleExport}
              disabled={exporting}
              className="h-10 flex items-center gap-2 px-3 text-sm font-bold rounded-lg transition-all duration-200 border border-slate-200 text-slate-700 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <Download className="w-4 h-4 text-slate-500" />
              {exporting ? "جارٍ التصدير..." : "تصدير"}
            </button>''',
    flags=re.MULTILINE | re.DOTALL,
    label="export button",
)

replace_once(
    r'''          \{/\* Pagination Footer \*/\}\n          \{data && data\.totalPages > 1 && \(.*?^          \)\}''',
    '''          {/* Pagination Footer */}
          {data && data.totalPages > 1 && (
            <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3" style={{ borderTop: "1px solid #f1f5f9" }}>
              <span className="text-xs" style={{ color: "#94a3b8" }}>
                عرض {(page - 1) * 20 + 1}–{Math.min(page * 20, data.totalCount)} من {data.totalCount}
              </span>
              <div className="flex items-center gap-1.5" aria-label="ترقيم صفحات المرضى">
                <button
                  type="button"
                  onClick={() => setPage((current) => Math.max(1, current - 1))}
                  disabled={page <= 1}
                  className="h-8 px-2.5 rounded-md text-xs font-semibold transition disabled:opacity-40 disabled:cursor-not-allowed"
                  style={{ border: "1px solid #dce8f5", background: "#fff", color: "#64748b" }}
                >
                  السابق
                </button>
                {getPatientPaginationItems(data.totalPages, page).map((item, index) =>
                  typeof item === "number" ? (
                    <button
                      type="button"
                      key={item}
                      aria-current={page === item ? "page" : undefined}
                      onClick={() => setPage(item)}
                      className="w-8 h-8 rounded-md text-xs font-semibold transition"
                      style={{
                        border: "1px solid #dce8f5",
                        background: page === item ? "#3d7ab5" : "#fff",
                        color: page === item ? "#fff" : "#64748b",
                      }}
                    >
                      {item}
                    </button>
                  ) : (
                    <span key={`${item}-${index}`} className="w-6 text-center text-xs text-slate-400" aria-hidden="true">…</span>
                  )
                )}
                <button
                  type="button"
                  onClick={() => setPage((current) => Math.min(data.totalPages, current + 1))}
                  disabled={page >= data.totalPages}
                  className="h-8 px-2.5 rounded-md text-xs font-semibold transition disabled:opacity-40 disabled:cursor-not-allowed"
                  style={{ border: "1px solid #dce8f5", background: "#fff", color: "#64748b" }}
                >
                  التالي
                </button>
              </div>
            </div>
          )}''',
    flags=re.MULTILINE | re.DOTALL,
    label="pagination footer",
)

path.write_text(text, encoding="utf-8")

Path("frontend/src/lib/patientTablePagination.ts").write_text(
    '''export type PatientPaginationItem = number | "ellipsis-left" | "ellipsis-right";

export function getPatientPaginationItems(
  totalPages: number,
  currentPage: number,
): PatientPaginationItem[] {
  const total = Math.max(1, Math.floor(totalPages));
  const current = Math.min(total, Math.max(1, Math.floor(currentPage)));

  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  if (current <= 4) {
    return [1, 2, 3, 4, 5, "ellipsis-right", total];
  }

  if (current >= total - 3) {
    return [1, "ellipsis-left", total - 4, total - 3, total - 2, total - 1, total];
  }

  return [1, "ellipsis-left", current - 1, current, current + 1, "ellipsis-right", total];
}
''',
    encoding="utf-8",
)

unit_test = Path("frontend/src/__tests__/lib/patientTablePagination.test.ts")
unit_test.parent.mkdir(parents=True, exist_ok=True)
unit_test.write_text(
    '''import { describe, expect, it } from "vitest";
import { getPatientPaginationItems } from "@/lib/patientTablePagination";

describe("patient table pagination", () => {
  it("shows every page when the result set is short", () => {
    expect(getPatientPaginationItems(4, 1)).toEqual([1, 2, 3, 4]);
  });

  it("keeps the current middle page reachable with both ellipses", () => {
    expect(getPatientPaginationItems(10, 5)).toEqual([
      1, "ellipsis-left", 4, 5, 6, "ellipsis-right", 10,
    ]);
  });

  it("keeps the final pages reachable instead of stopping at page three", () => {
    expect(getPatientPaginationItems(10, 9)).toEqual([
      1, "ellipsis-left", 6, 7, 8, 9, 10,
    ]);
  });

  it("clamps invalid current pages safely", () => {
    expect(getPatientPaginationItems(10, 99)).toContain(10);
    expect(getPatientPaginationItems(10, -4)).toContain(1);
  });
});
''',
    encoding="utf-8",
)

for temporary in (
    Path(".github/workflows/core-pat-022-patch.yml"),
    Path(".github/workflows/core-pat-022-bootstrap.yml"),
    Path("scripts/core_pat_022_patch.py"),
):
    temporary.unlink(missing_ok=True)
