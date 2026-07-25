from pathlib import Path

path = Path("frontend/src/components/patient/tabs/TimelineTab.tsx")
text = path.read_text(encoding="utf-8")
text = text.replace(
    'import api from "@/lib/api";\nimport { EmptyState } from "./EmptyState";',
    'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { EmptyState } from "./EmptyState";',
    1,
)
old = '''  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);
  const [filterType, setFilterType] = useState("all");

  useEffect(() => {
    setFetchError(false);
    api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`)
      .then((r) => setEvents(r.data))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
  }, [patientId, retryKey]);
'''
new = '''  const [events, setEvents] = useState<TimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);
  const [filterType, setFilterType] = useState("all");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setFetchError(null);

    api.get<TimelineEvent[]>(`/api/patients/${patientId}/timeline`)
      .then((r) => {
        if (!cancelled) setEvents(r.data);
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setEvents([]);
          setFetchError(extractErrorMessage(error, "فشل تحميل السجل الزمني"));
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => { cancelled = true; };
  }, [patientId, retryKey]);
'''
if old not in text:
    raise SystemExit("Timeline loading block not found")
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
    raise SystemExit("Timeline error block not found")
path.write_text(text.replace(old, new, 1), encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/TimelineTabReliability.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { TimelineTab } from "@/components/patient/tabs/TimelineTab";

vi.mock("@/lib/api", () => ({ default: { get: vi.fn() } }));

const event = {
  type: "visit",
  id: "visit-1",
  date: "2026-07-25T10:00:00Z",
  title: "زيارة متابعة",
  description: "تمت معاينة المريض",
};

describe("TimelineTab reliability", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the backend load error without a false empty timeline", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { status: 503, data: { message: "السجل الزمني غير متاح" } },
    });

    render(<TimelineTab patientId="patient-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("السجل الزمني غير متاح");
    expect(screen.queryByText("لا يوجد سجل زمني")).not.toBeInTheDocument();
  });

  it("returns to loading during retry and renders recovered events", async () => {
    let recovered = false;
    vi.mocked(api.get).mockImplementation(() =>
      recovered
        ? Promise.resolve({ data: [event] })
        : Promise.reject({ response: { status: 500, data: { message: "فشل أولي" } } })
    );

    render(<TimelineTab patientId="patient-1" />);
    expect(await screen.findByRole("alert")).toHaveTextContent("فشل أولي");

    recovered = true;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(screen.queryByText("لا يوجد سجل زمني")).not.toBeInTheDocument();
    expect(await screen.findByText("زيارة متابعة")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it("ignores a stale response after the patient changes", async () => {
    let resolveOld: ((value: { data: typeof event[] }) => void) | undefined;
    vi.mocked(api.get).mockImplementation((url) => {
      if (String(url).includes("patient-old")) {
        return new Promise((resolve) => { resolveOld = resolve; });
      }
      return Promise.resolve({ data: [{ ...event, id: "visit-new", title: "زيارة المريض الجديد" }] });
    });

    const { rerender } = render(<TimelineTab patientId="patient-old" />);
    rerender(<TimelineTab patientId="patient-new" />);

    expect(await screen.findByText("زيارة المريض الجديد")).toBeInTheDocument();
    resolveOld?.({ data: [{ ...event, title: "زيارة قديمة" }] });

    await waitFor(() => expect(screen.queryByText("زيارة قديمة")).not.toBeInTheDocument());
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_036_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-036-bootstrap.yml").unlink(missing_ok=True)
