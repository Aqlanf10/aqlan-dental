from pathlib import Path

path = Path("frontend/src/components/patient/tabs/OrthodonticsTab.tsx")
text = path.read_text(encoding="utf-8")

text = text.replace(
    'import api from "@/lib/api";\nimport { EmptyState } from "./EmptyState";',
    'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { EmptyState } from "./EmptyState";',
    1,
)

old = '''  const [cases, setCases] = useState<OrthoCaseDto[]>([]);
  const [overviews, setOverviews] = useState<Record<string, OrthoOverviewDto>>({});
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setFetchError(false);
    api
      .get<OrthoCaseDto[]>(`/api/ortho-cases?patientId=${patientId}`)
      .then((r) => {
        setCases(r.data);
        // Fetch overview for each case
        r.data.forEach((c) => {
          api
            .get<OrthoOverviewDto>(`/api/ortho-cases/${c.id}/overview`)
            .then((ov) => setOverviews((prev) => ({ ...prev, [c.id]: ov.data })))
            .catch(() => {});
        });
      })
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);
'''
new = '''  const [cases, setCases] = useState<OrthoCaseDto[]>([]);
  const [overviews, setOverviews] = useState<Record<string, OrthoOverviewDto>>({});
  const [failedOverviewIds, setFailedOverviewIds] = useState<string[]>([]);
  const [loading, setLoading] = useState(true);
  const [overviewLoading, setOverviewLoading] = useState(false);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [overviewError, setOverviewError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    let cancelled = false;

    const loadCases = async () => {
      setLoading(true);
      setOverviewLoading(false);
      setFetchError(null);
      setOverviewError(null);
      setCases([]);
      setOverviews({});
      setFailedOverviewIds([]);

      try {
        const response = await api.get<OrthoCaseDto[]>(`/api/ortho-cases?patientId=${patientId}`);
        if (cancelled) return;

        const nextCases = response.data;
        setCases(nextCases);
        setLoading(false);

        if (nextCases.length === 0) return;

        setOverviewLoading(true);
        const overviewResults = await Promise.allSettled(
          nextCases.map((orthoCase) =>
            api.get<OrthoOverviewDto>(`/api/ortho-cases/${orthoCase.id}/overview`)
          )
        );
        if (cancelled) return;

        const nextOverviews: Record<string, OrthoOverviewDto> = {};
        const failures: string[] = [];
        overviewResults.forEach((result, index) => {
          const caseId = nextCases[index].id;
          if (result.status === "fulfilled") {
            nextOverviews[caseId] = result.value.data;
          } else {
            failures.push(caseId);
          }
        });

        setOverviews(nextOverviews);
        setFailedOverviewIds(failures);
        if (failures.length > 0) {
          setOverviewError(
            failures.length === nextCases.length
              ? "تعذر تحميل تفاصيل الحالات التقويمية — أعد المحاولة"
              : `تعذر تحميل تفاصيل ${failures.length} من الحالات التقويمية`
          );
        }
        setOverviewLoading(false);
      } catch (error: unknown) {
        if (cancelled) return;
        setCases([]);
        setFetchError(extractErrorMessage(error, "فشل تحميل الحالات التقويمية"));
        setLoading(false);
      }
    };

    void loadCases();
    return () => { cancelled = true; };
  }, [patientId, retryKey]);
'''
if old not in text:
    raise SystemExit("OrthodonticsTab loading block not found")
text = text.replace(old, new, 1)

old = '''  if (fetchError) {
    return (
      <div className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }
'''
new = '''  if (fetchError) {
    return (
      <div role="alert" className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">{fetchError}</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }
'''
if old not in text:
    raise SystemExit("OrthodonticsTab full error block not found")
text = text.replace(old, new, 1)

old = '''  return (
    <div className="space-y-4">
      {cases.map((c) => {
'''
new = '''  return (
    <div className="space-y-4">
      {overviewError && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-center">
          <p className="text-sm text-amber-800">{overviewError}</p>
          <button
            type="button"
            onClick={() => setRetryKey((key) => key + 1)}
            className="mt-1 text-xs font-semibold text-blue-600 underline decoration-dotted"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      {cases.map((c) => {
'''
if old not in text:
    raise SystemExit("OrthodonticsTab list start not found")
text = text.replace(old, new, 1)

old = '''            {/* ── Status Chips (only if overview loaded) ── */}
            {ov && (
'''
new = '''            {overviewLoading && !ov && (
              <div className="mx-4 mb-4 h-16 rounded-xl bg-slate-100 animate-pulse" aria-label="جار تحميل تفاصيل الحالة" />
            )}
            {!overviewLoading && failedOverviewIds.includes(c.id) && (
              <div className="mx-4 mb-4 rounded-lg border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
                تعذر تحميل تفاصيل هذه الحالة
              </div>
            )}

            {/* ── Status Chips (only if overview loaded) ── */}
            {ov && (
'''
if old not in text:
    raise SystemExit("OrthodonticsTab overview UI target not found")
text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/OrthodonticsTabReliability.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { OrthodonticsTab } from "@/components/patient/tabs/OrthodonticsTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const cases = [
  { id: "case-1", caseNumber: "ORTHO-1", status: "active", stagePercentage: 20 },
  { id: "case-2", caseNumber: "ORTHO-2", status: "completed", stagePercentage: 100 },
];

const overview = {
  hasClinicalExam: true,
  problemsCount: 2,
  hasDiagnosis: true,
  isDiagnosisApproved: true,
  hasTreatmentPlan: true,
  isTreatmentPlanApproved: true,
  treatmentPlansCount: 1,
  completedStages: 1,
  totalStages: 4,
  visitsCount: 3,
  photosCount: 5,
  cephAnalysesCount: 1,
  hasRetention: false,
  checklistCompleted: 3,
  checklistTotal: 5,
};

describe("OrthodonticsTab reliability", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows a retryable list error without a false empty state", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { status: 503, data: { message: "خدمة حالات التقويم غير متاحة" } },
    });

    render(<OrthodonticsTab patientId="patient-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة حالات التقويم غير متاحة");
    expect(screen.queryByText("لا توجد حالات تقويمية")).not.toBeInTheDocument();
  });

  it("keeps successful cases visible and reports partial overview failures", async () => {
    vi.mocked(api.get).mockImplementation((url) => {
      const value = String(url);
      if (value.includes("patientId=")) return Promise.resolve({ data: cases });
      if (value.includes("case-1/overview")) return Promise.resolve({ data: overview });
      return Promise.reject({ response: { status: 500 } });
    });

    render(<OrthodonticsTab patientId="patient-1" />);

    expect(await screen.findByText("حالة تقويم ORTHO-1")).toBeInTheDocument();
    expect(screen.getByText("حالة تقويم ORTHO-2")).toBeInTheDocument();
    expect(await screen.findByRole("alert")).toHaveTextContent("تعذر تحميل تفاصيل 1 من الحالات التقويمية");
    expect(screen.getByText("تعذر تحميل تفاصيل هذه الحالة")).toBeInTheDocument();
    expect(screen.getByText("تشخيص معتمد")).toBeInTheDocument();
  });

  it("clears stale failures and reloads all data on retry", async () => {
    let recovered = false;
    vi.mocked(api.get).mockImplementation((url) => {
      if (!recovered) return Promise.reject({ response: { status: 500, data: { message: "فشل أولي" } } });
      if (String(url).includes("patientId=")) return Promise.resolve({ data: [cases[0]] });
      return Promise.resolve({ data: overview });
    });

    render(<OrthodonticsTab patientId="patient-1" />);
    expect(await screen.findByRole("alert")).toHaveTextContent("فشل أولي");

    recovered = true;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("حالة تقويم ORTHO-1")).toBeInTheDocument();
    expect(await screen.findByText("تشخيص معتمد")).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText("فشل أولي")).not.toBeInTheDocument());
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_035_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-035-bootstrap.yml").unlink(missing_ok=True)
