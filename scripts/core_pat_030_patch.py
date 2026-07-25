from pathlib import Path
import re

path = Path("frontend/src/components/patient/tabs/OrthoSurgicalTab.tsx")
text = path.read_text(encoding="utf-8")


def replace_once(pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    global text
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-030: expected one {label} match, found {count}")
    text = updated


replace_once(
    re.escape('import { GitBranch, ChevronLeft, Plus, Loader2, Stethoscope, Scissors } from "lucide-react";\n'),
    'import { GitBranch, ChevronLeft, Plus, Loader2, Stethoscope, Scissors, AlertTriangle } from "lucide-react";\n',
    label="error icon import",
)
replace_once(
    re.escape('import api from "@/lib/api";\n'),
    'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\n',
    label="error helper import",
)
replace_once(
    re.escape('  const [loading, setLoading] = useState(true);\n'),
    '  const [loading, setLoading] = useState(true);\n'
    '  const [loadError, setLoadError] = useState("");\n'
    '  const [orthoCasesError, setOrthoCasesError] = useState("");\n',
    label="load error state",
)

replace_once(
    r"  const fetchCases = useCallback\(async \(\) => \{.*?^  \}, \[patientId\]\);",
    '''  const fetchCases = useCallback(async () => {
    setLoading(true);
    setLoadError("");
    setOrthoCasesError("");

    const [surgicalResult, orthoResult] = await Promise.allSettled([
      api.get<{ data: OrthoSurgicalCaseListItem[] }>(`/api/ortho-surgical-cases?patientId=${patientId}`)
        .then((response) => response.data?.data ?? []),
      api.get<OrthoCaseLite[]>(patientActiveOrthoCasesUrl(patientId))
        .then((response) => response.data ?? []),
    ]);

    if (surgicalResult.status === "rejected") {
      setCases([]);
      setOrthoCases(orthoResult.status === "fulfilled" ? orthoResult.value : []);
      setLoadError(
        extractErrorMessage(
          surgicalResult.reason,
          "تعذر تحميل الحالات التقويمية الجراحية — تحقق من الاتصال وحاول مجدداً"
        )
      );
      setLoading(false);
      return;
    }

    setCases(surgicalResult.value);
    if (orthoResult.status === "fulfilled") {
      setOrthoCases(orthoResult.value);
    } else {
      setOrthoCases([]);
      setShowCreate(false);
      setSelectedOrthoCase("");
      setOrthoCasesError(
        extractErrorMessage(
          orthoResult.reason,
          "تعذر تحميل حالات التقويم النشطة؛ إنشاء حالة تقويمية جراحية غير متاح حالياً"
        )
      );
    }
    setLoading(false);
  }, [patientId]);''',
    flags=re.MULTILINE | re.DOTALL,
    label="fetchCases block",
)

replace_once(
    r'''    \} catch \(e\) \{\n      const msg = \(e as \{ response\?: \{ data\?: \{ message\?: string \} \} \}\)\?\.response\?\.data\?\.message;\n      toast\.error\(msg \?\? "فشل إنشاء الحالة"\);\n    \} finally \{''',
    '''    } catch (error: unknown) {
      toast.error(extractErrorMessage(error, "فشل إنشاء الحالة"));
    } finally {''',
    label="create error handler",
)

loading_end = '''  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 2 }).map((_, i) => <div key={i} className="h-24 bg-[#f1f5f9] rounded-xl" />)}
      </div>
    );
  }

'''
if loading_end not in text:
    raise SystemExit("CORE-PAT-030: loading block not found")
full_error = loading_end + '''  if (loadError) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-center">
        <AlertTriangle className="w-10 h-10 mx-auto mb-3 text-red-400" />
        <p className="text-sm font-semibold text-red-700">{loadError}</p>
        <p className="text-xs text-red-500 mt-1">
          لم نعتبر فشل الطلب «لا توجد حالات تقويمية جراحية».
        </p>
        <button
          type="button"
          onClick={fetchCases}
          className="mt-4 px-4 py-2 text-sm font-semibold rounded-lg bg-white border border-red-200 text-red-700 hover:bg-red-100 transition"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

'''
text = text.replace(loading_end, full_error, 1)

# Add a partial warning before both empty and populated render paths.
empty_marker = '''  if (cases.length === 0 && !showCreate) {
    return (
      <div className="space-y-4">
'''
if empty_marker not in text:
    raise SystemExit("CORE-PAT-030: empty state marker not found")
empty_replacement = '''  if (cases.length === 0 && !showCreate) {
    return (
      <div className="space-y-4">
        {orthoCasesError && (
          <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 flex items-start gap-3">
            <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
            <div className="flex-1">
              <p className="text-xs font-semibold text-amber-800">{orthoCasesError}</p>
              <p className="text-[11px] text-amber-700 mt-0.5">يمكن عرض الحالات الجراحية، لكن لا يمكن التحقق من أهلية إنشاء حالة جديدة.</p>
            </div>
            <button type="button" onClick={fetchCases} className="text-xs font-semibold text-amber-800 underline">إعادة المحاولة</button>
          </div>
        )}
'''
text = text.replace(empty_marker, empty_replacement, 1)

return_marker = '''  return (
    <div className="space-y-4">
      {cases.map((c) => (
'''
if return_marker not in text:
    raise SystemExit("CORE-PAT-030: populated return marker not found")
populated_replacement = '''  return (
    <div className="space-y-4">
      {orthoCasesError && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 flex items-start gap-3">
          <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <p className="text-xs font-semibold text-amber-800">{orthoCasesError}</p>
            <p className="text-[11px] text-amber-700 mt-0.5">الحالات الحالية ظاهرة، لكن إنشاء حالة جديدة معطّل حتى ينجح تحميل حالات التقويم النشطة.</p>
          </div>
          <button type="button" onClick={fetchCases} className="text-xs font-semibold text-amber-800 underline">إعادة المحاولة</button>
        </div>
      )}
      {cases.map((c) => (
'''
text = text.replace(return_marker, populated_replacement, 1)
path.write_text(text, encoding="utf-8")

# Regression tests.
test = Path("frontend/src/__tests__/components/patient/OrthoSurgicalTabLoadFailure.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { OrthoSurgicalTab } from "@/components/patient/tabs/OrthoSurgicalTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: ReactNode }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

describe("OrthoSurgicalTab honest loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a retryable error instead of a false empty state when surgical cases fail", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.startsWith("/api/ortho-surgical-cases")) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "خدمة الحالات الجراحية غير متاحة" } },
        });
      }
      return Promise.resolve({ data: [] });
    });

    render(<OrthoSurgicalTab patientId="patient-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة الحالات الجراحية غير متاحة");
    expect(screen.getByText(/لم نعتبر فشل الطلب/)).toBeInTheDocument();
    expect(screen.queryByText("لا توجد حالات تقويمية جراحية")).not.toBeInTheDocument();
  });

  it("keeps a genuine empty surgical list but disables creation when active ortho cases fail", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.startsWith("/api/ortho-surgical-cases")) return Promise.resolve({ data: { data: [] } });
      return Promise.reject({
        isAxiosError: true,
        response: { status: 500, data: { message: "تعذر تحميل حالات التقويم النشطة" } },
      });
    });

    render(<OrthoSurgicalTab patientId="patient-1" />);

    expect(await screen.findByText("لا توجد حالات تقويمية جراحية")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("تعذر تحميل حالات التقويم النشطة");
    expect(screen.queryByRole("button", { name: "إنشاء حالة تقويمية جراحية" })).not.toBeInTheDocument();
  });

  it("retries both requests without reloading the page", async () => {
    let attempt = 0;
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (attempt === 0) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "انقطاع مؤقت" } },
        });
      }
      if (url.startsWith("/api/ortho-surgical-cases")) return Promise.resolve({ data: { data: [] } });
      return Promise.resolve({ data: [{ id: "ortho-1", caseNumber: "O-001", status: "Active" }] });
    });

    render(<OrthoSurgicalTab patientId="patient-1" />);
    const retry = await screen.findByRole("button", { name: "إعادة المحاولة" });
    attempt = 1;
    fireEvent.click(retry);

    expect(await screen.findByRole("button", { name: "إنشاء حالة تقويمية جراحية" })).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(4));
    expect(screen.queryByText("انقطاع مؤقت")).not.toBeInTheDocument();
  });
});
''', encoding="utf-8")

for temporary in (
    Path(".github/workflows/core-pat-030-bootstrap.yml"),
    Path("scripts/core_pat_030_patch.py"),
):
    temporary.unlink(missing_ok=True)
