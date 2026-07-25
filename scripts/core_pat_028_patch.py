from pathlib import Path
import re

path = Path("frontend/src/components/patient/tabs/OverviewTab.tsx")
text = path.read_text(encoding="utf-8")


def replace_once(pattern: str, replacement: str, *, flags: int = 0, label: str) -> None:
    global text
    updated, count = re.subn(pattern, replacement, text, count=1, flags=flags)
    if count != 1:
        raise SystemExit(f"CORE-PAT-028: expected one {label} match, found {count}")
    text = updated


replace_once(
    re.escape('import api from "@/lib/api";\n'),
    'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\n',
    label="error helper import",
)

replace_once(
    re.escape('interface SurgeryCase {\n  id: string;\n  caseNumber: string;\n  surgeryType: string;\n  status: string;\n  doctorName?: string;\n}\n'),
    'interface SurgeryCase {\n  id: string;\n  caseNumber: string;\n  surgeryType: string;\n  status: string;\n  doctorName?: string;\n}\n\ntype OverviewSection = "timeline" | "ortho" | "surgery";\ntype OverviewSectionErrors = Partial<Record<OverviewSection, string>>;\n',
    label="overview section error types",
)

replace_once(
    re.escape('  const [error, setError] = useState<string | null>(null);\n'),
    '  const [error, setError] = useState<string | null>(null);\n'
    '  const [sectionErrors, setSectionErrors] = useState<OverviewSectionErrors>({});\n'
    '  const [reloadKey, setReloadKey] = useState(0);\n',
    label="overview load states",
)

replace_once(
    r'''  useEffect\(\(\) => \{\n    setLoading\(true\);\n    setError\(null\);\n    Promise\.all\(\[.*?^  \}, \[patientId\]\);''',
    '''  useEffect(() => {
    let cancelled = false;

    const loadOverview = async () => {
      setLoading(true);
      setError(null);
      setSectionErrors({});

      const [timelineResult, orthoResult, surgeryResult] = await Promise.allSettled([
        api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`).then((response) => response.data),
        api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientId}&pageSize=5`).then((response) => response.data),
        api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientId}&pageSize=5`)
          .then((response) => response.data.data ?? []),
      ]);

      if (cancelled) return;

      const failures: OverviewSectionErrors = {};
      if (timelineResult.status === "fulfilled") {
        setEvents(timelineResult.value);
      } else {
        setEvents([]);
        failures.timeline = extractErrorMessage(timelineResult.reason, "تعذر تحميل السجل الزمني");
      }

      if (orthoResult.status === "fulfilled") {
        setOrthoCases(orthoResult.value);
      } else {
        setOrthoCases([]);
        failures.ortho = extractErrorMessage(orthoResult.reason, "تعذر تحميل حالات التقويم");
      }

      if (surgeryResult.status === "fulfilled") {
        setSurgeryCases(surgeryResult.value);
      } else {
        setSurgeryCases([]);
        failures.surgery = extractErrorMessage(surgeryResult.reason, "تعذر تحميل حالات الجراحة");
      }

      if (Object.keys(failures).length === 3) {
        setError("تعذر تحميل بيانات النظرة العامة — تحقق من الاتصال وحاول مجدداً");
      } else {
        setSectionErrors(failures);
      }
      setLoading(false);
    };

    loadOverview();
    return () => {
      cancelled = true;
    };
  }, [patientId, reloadKey]);''',
    flags=re.MULTILINE | re.DOTALL,
    label="overview data effect",
)

replace_once(
    r'''  if \(error\) \{\n    return \(\n      <div className="text-center py-12">.*?^  \}\n''',
    '''  if (error) {
    return (
      <div role="alert" className="text-center py-12">
        <AlertTriangle className="w-10 h-10 text-red-400 mx-auto mb-3" />
        <p className="text-sm text-red-500">{error}</p>
        <p className="text-xs text-red-400 mt-1">لم نعرض قوائم فارغة بدل البيانات التي فشل تحميلها.</p>
        <button
          type="button"
          onClick={() => setReloadKey((value) => value + 1)}
          className="mt-3 text-xs font-semibold text-[#3d7ab5] hover:underline"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }
''',
    flags=re.MULTILINE | re.DOTALL,
    label="full overview error state",
)

return_marker = '''  return (
    <div className="space-y-5">

      {/* ════════════════════ Quick Actions ════════════════════ */}
'''
if return_marker not in text:
    raise SystemExit("CORE-PAT-028: overview return marker not found")
warning_block = '''  return (
    <div className="space-y-5">

      {Object.keys(sectionErrors).length > 0 && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 flex items-start gap-3">
          <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs font-bold text-amber-800">تم تحميل جزء من نظرة المريض فقط</p>
            <ul className="mt-1 space-y-0.5 text-xs text-amber-700">
              {Object.entries(sectionErrors).map(([section, message]) => (
                <li key={section}>• {message}</li>
              ))}
            </ul>
          </div>
          <button
            type="button"
            onClick={() => setReloadKey((value) => value + 1)}
            className="text-xs font-semibold text-amber-800 underline flex-shrink-0"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {/* ════════════════════ Quick Actions ════════════════════ */}
'''
text = text.replace(return_marker, warning_block, 1)

old_timeline_empty = '''          {events.length === 0 ? (
            <EmptyState
              icon={Clock}
              title="لا يوجد نشاط بعد"
              description="ستظهر هنا المواعيد والزيارات والمدفوعات والمستندات"
            />
          ) : (
'''
new_timeline_empty = '''          {sectionErrors.timeline ? (
            <div className="text-center py-8">
              <AlertTriangle className="w-8 h-8 text-amber-400 mx-auto mb-2" />
              <p className="text-xs font-semibold text-amber-700">{sectionErrors.timeline}</p>
              <p className="text-[11px] text-amber-600 mt-1">هذه ليست حالة «لا يوجد نشاط».</p>
            </div>
          ) : events.length === 0 ? (
            <EmptyState
              icon={Clock}
              title="لا يوجد نشاط بعد"
              description="ستظهر هنا المواعيد والزيارات والمدفوعات والمستندات"
            />
          ) : (
'''
if old_timeline_empty not in text:
    raise SystemExit("CORE-PAT-028: timeline empty state not found")
text = text.replace(old_timeline_empty, new_timeline_empty, 1)
path.write_text(text, encoding="utf-8")

# Regression tests for complete and partial failures.
test = Path("frontend/src/__tests__/components/patient/OverviewTabLoadFailure.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { OverviewTab } from "@/components/patient/tabs/OverviewTab";
import type { PatientProfile } from "@/types/patient";
import type { PatientSummary } from "@/types/patientSummary";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

const patient = {
  id: "patient-1",
  patientNumber: "P-001",
  firstName: "مريض",
  lastName: "تجريبي",
  gender: "Male",
  isActive: true,
} as PatientProfile;

const summary: PatientSummary = {
  totalAppointments: 0,
  completedAppointments: 0,
  activeOrthoCases: 0,
  totalPaid: null,
  totalOutstanding: null,
  prescriptionsCount: 0,
};

function renderOverview() {
  return render(
    <OverviewTab
      patientId="patient-1"
      patient={patient}
      summary={summary}
      canViewFinance={false}
    />,
  );
}

describe("OverviewTab honest section loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a full retryable error instead of false empty sections when every request fails", async () => {
    vi.mocked(api.get).mockRejectedValue({
      isAxiosError: true,
      response: { status: 503, data: { message: "الخدمة غير متاحة حالياً" } },
    });

    renderOverview();

    expect(await screen.findByRole("alert")).toHaveTextContent("تعذر تحميل بيانات النظرة العامة");
    expect(screen.getByText(/لم نعرض قوائم فارغة/)).toBeInTheDocument();
    expect(screen.queryByText("لا يوجد نشاط بعد")).not.toBeInTheDocument();
  });

  it("keeps successful sections and identifies a failed timeline without claiming there is no activity", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.includes("/timeline")) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 500, data: { message: "تعذر تحميل السجل السريري" } },
        });
      }
      if (url.includes("/ortho-cases")) return Promise.resolve({ data: [] });
      return Promise.resolve({ data: { data: [] } });
    });

    renderOverview();

    expect(await screen.findByText("تم تحميل جزء من نظرة المريض فقط")).toBeInTheDocument();
    expect(screen.getAllByText("تعذر تحميل السجل السريري").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("هذه ليست حالة «لا يوجد نشاط».")).toBeInTheDocument();
    expect(screen.queryByText("لا يوجد نشاط بعد")).not.toBeInTheDocument();
  });

  it("retries all sections without reloading the browser", async () => {
    let attempt = 0;
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (attempt == 0) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "انقطاع مؤقت" } },
        });
      }
      if (url.includes("/timeline")) return Promise.resolve({ data: [] });
      if (url.includes("/ortho-cases")) return Promise.resolve({ data: [] });
      return Promise.resolve({ data: { data: [] } });
    });

    renderOverview();
    const retry = await screen.findByRole("button", { name: "إعادة المحاولة" });
    attempt = 1;
    fireEvent.click(retry);

    expect(await screen.findByText("لا يوجد نشاط بعد")).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(6));
    expect(screen.queryByText("تعذر تحميل بيانات النظرة العامة")).not.toBeInTheDocument();
  });
});
''', encoding="utf-8")

for temporary in (
    Path(".github/workflows/core-pat-028-bootstrap.yml"),
    Path("scripts/core_pat_028_patch.py"),
):
    temporary.unlink(missing_ok=True)
