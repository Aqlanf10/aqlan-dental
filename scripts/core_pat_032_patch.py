from pathlib import Path

page = Path("frontend/src/app/(dashboard)/patients/[id]/page.tsx")
text = page.read_text(encoding="utf-8")

old = '''  const { id }  = useParams<{ id: string }>();
  const router   = useRouter();
  const { user } = useAuthStore();
  // isClinicalRole covers: Doctor, Orthodontist, GeneralDentist, OralSurgeon
'''
new = '''  const { id }  = useParams<{ id: string }>();
  const router   = useRouter();
  const { user } = useAuthStore();
  const patientIdentifier = typeof id === "string" ? id : "";
  const hasGuidPatientId = isGuid(patientIdentifier);
  // isClinicalRole covers: Doctor, Orthodontist, GeneralDentist, OralSurgeon
'''
if old not in text:
    raise SystemExit("component header target not found")
text = text.replace(old, new, 1)

old = '''  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState<string | null>(null);
  const [activeTab,    setActiveTab]    = useState<Tab>("overview");
'''
new = '''  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState<string | null>(null);
  const [resolutionRetryable, setResolutionRetryable] = useState(false);
  const [resolutionRetryKey, setResolutionRetryKey] = useState(0);
  const [profileRetryKey, setProfileRetryKey] = useState(0);
  const [activeTab,    setActiveTab]    = useState<Tab>("overview");
'''
if old not in text:
    raise SystemExit("state target not found")
text = text.replace(old, new, 1)

old = '''  useEffect(() => {
    const patientId = id as string;
    if (patientId && !isGuid(patientId)) {
      api.get(`/api/patients/by-number/${encodeURIComponent(patientId)}`)
        .then(({ data }) => {
          if (data?.id) {
            router.replace(`/patients/${data.id}`);
          } else {
            setError(`لا يوجد مريض برقم الملف ${patientId}`);
            setLoading(false);
          }
        })
        .catch(() => {
          setError(`لا يوجد مريض برقم الملف ${patientId}`);
          setLoading(false);
        });
    }
  }, [id, router]);
'''
new = '''  useEffect(() => {
    const patientId = patientIdentifier;
    if (!patientId || hasGuidPatientId) {
      setResolutionRetryable(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);
    setResolutionRetryable(false);

    api.get(`/api/patients/by-number/${encodeURIComponent(patientId)}`)
      .then(({ data }) => {
        if (cancelled) return;
        if (data?.id) {
          router.replace(`/patients/${data.id}`);
          return;
        }
        setError(`لا يوجد مريض برقم الملف ${patientId}`);
        setLoading(false);
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        const status = (err as { response?: { status?: number } })?.response?.status;
        if (status === 404) {
          setError(`لا يوجد مريض برقم الملف ${patientId}`);
          setResolutionRetryable(false);
        } else {
          setError(extractErrorMessage(
            err,
            "تعذر البحث عن المريض برقم الملف — تحقق من الاتصال وحاول مجدداً"
          ));
          setResolutionRetryable(true);
        }
        setLoading(false);
      });

    return () => { cancelled = true; };
  }, [patientIdentifier, hasGuidPatientId, router, resolutionRetryKey]);
'''
if old not in text:
    raise SystemExit("identifier resolution effect target not found")
text = text.replace(old, new, 1)

old = '''  useEffect(() => {
    setError(null);
    api.get<PatientProfile>(`/api/patients/${id}`)
      .then(r  => setPatient(r.data))
      .catch(err => {
        setError(extractErrorMessage(err, "فشل تحميل بيانات المريض"));
      })
      .finally(() => setLoading(false));

    setSummaryError(null);
    api.get<PatientSummary>(`/api/patients/${id}/summary`)
      .then(r => { setSummary(r.data); setSummaryError(null); })
      .catch(err => {
        setSummary(null);
        setSummaryError(extractErrorMessage(err, "تعذر تحميل ملخص المريض"));
      });

    setRelatedCasesError(null);
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${id}&pageSize=10`)
      .then(r => setOrthoCases(r.data))
      .catch(err => setRelatedCasesError(extractErrorMessage(err, "تعذر تحميل الحالات المرتبطة")));
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${id}&pageSize=10`)
      .then(r => setSurgeryCases(r.data.data ?? []))
      .catch(err => setRelatedCasesError(extractErrorMessage(err, "تعذر تحميل الحالات المرتبطة")));
  }, [id, financeRefreshKey]);
'''
new = '''  useEffect(() => {
    // CORE-PAT-032: GUID-only endpoints must never receive a patient number.
    // Resolve the number first, then let the redirected GUID route load the file.
    if (!patientIdentifier || !hasGuidPatientId) return;

    let cancelled = false;
    setLoading(true);
    setError(null);
    setResolutionRetryable(false);

    api.get<PatientProfile>(`/api/patients/${patientIdentifier}`)
      .then(r => { if (!cancelled) setPatient(r.data); })
      .catch(err => {
        if (!cancelled) {
          setPatient(null);
          setError(extractErrorMessage(err, "فشل تحميل بيانات المريض"));
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });

    setSummaryError(null);
    api.get<PatientSummary>(`/api/patients/${patientIdentifier}/summary`)
      .then(r => {
        if (!cancelled) {
          setSummary(r.data);
          setSummaryError(null);
        }
      })
      .catch(err => {
        if (!cancelled) {
          setSummary(null);
          setSummaryError(extractErrorMessage(err, "تعذر تحميل ملخص المريض"));
        }
      });

    setRelatedCasesError(null);
    api.get<OrthoCase[]>(`/api/ortho-cases?patientId=${patientIdentifier}&pageSize=10`)
      .then(r => { if (!cancelled) setOrthoCases(r.data); })
      .catch(err => {
        if (!cancelled) setRelatedCasesError(extractErrorMessage(err, "تعذر تحميل الحالات المرتبطة"));
      });
    api.get<{ data: SurgeryCase[] }>(`/api/surgery-cases?patientId=${patientIdentifier}&pageSize=10`)
      .then(r => { if (!cancelled) setSurgeryCases(r.data.data ?? []); })
      .catch(err => {
        if (!cancelled) setRelatedCasesError(extractErrorMessage(err, "تعذر تحميل الحالات المرتبطة"));
      });

    return () => { cancelled = true; };
  }, [patientIdentifier, hasGuidPatientId, financeRefreshKey, profileRetryKey]);
'''
if old not in text:
    raise SystemExit("data fetching effect target not found")
text = text.replace(old, new, 1)

old = '''  if (error || !patient) return (
    <div className="h-full flex items-center justify-center">
      <div className="text-center space-y-3">
        <div className="w-16 h-16 bg-red-50 rounded-full flex items-center justify-center mx-auto">
          <AlertTriangle className="w-8 h-8 text-red-400" />
        </div>
        <p className="text-slate-600 font-medium">{error ?? "المريض غير موجود"}</p>
        {/* Sprint 17 — RTL icon: back arrow points RIGHT in Arabic RTL. The old
            `←` Unicode glyph pointed left (LTR convention) — wrong direction. */}
        <Link href="/patients" className="inline-flex items-center gap-1 text-sm text-blue-600 hover:underline">
          <RtlArrowBack className="w-4 h-4" /> العودة للمرضى
        </Link>
      </div>
    </div>
  );
'''
new = '''  if (error || !patient) return (
    <div className="h-full flex items-center justify-center">
      <div role="alert" className="text-center space-y-3">
        <div className="w-16 h-16 bg-red-50 rounded-full flex items-center justify-center mx-auto">
          <AlertTriangle className="w-8 h-8 text-red-400" />
        </div>
        <p className="text-slate-600 font-medium">{error ?? "المريض غير موجود"}</p>
        <div className="flex items-center justify-center gap-3 flex-wrap">
          {(hasGuidPatientId || resolutionRetryable) && (
            <button
              type="button"
              onClick={() => {
                if (hasGuidPatientId) setProfileRetryKey(k => k + 1);
                else setResolutionRetryKey(k => k + 1);
              }}
              className="text-sm font-semibold text-blue-600 underline decoration-dotted"
            >
              إعادة المحاولة
            </button>
          )}
          {/* Sprint 17 — RTL icon: back arrow points RIGHT in Arabic RTL. The old
              `←` Unicode glyph pointed left (LTR convention) — wrong direction. */}
          <Link href="/patients" className="inline-flex items-center gap-1 text-sm text-blue-600 hover:underline">
            <RtlArrowBack className="w-4 h-4" /> العودة للمرضى
          </Link>
        </div>
      </div>
    </div>
  );
'''
if old not in text:
    raise SystemExit("error UI target not found")
text = text.replace(old, new, 1)
page.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/PatientProfileIdentifierRouting.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import PatientProfilePage from "@/app/(dashboard)/patients/[id]/page";

const navigation = vi.hoisted(() => ({
  id: "",
  replace: vi.fn(),
  push: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: navigation.id }),
  useRouter: () => ({ replace: navigation.replace, push: navigation.push }),
  useSearchParams: () => ({ get: () => null }),
}));

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { role: "Admin" } }),
}));

describe("Patient profile identifier routing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    navigation.id = "GM-2026-025";
  });

  it("resolves a patient number without calling GUID-only endpoints", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { id: "11111111-1111-1111-1111-111111111111" },
    });

    render(<PatientProfilePage />);

    await waitFor(() => {
      expect(navigation.replace).toHaveBeenCalledWith(
        "/patients/11111111-1111-1111-1111-111111111111"
      );
    });

    expect(vi.mocked(api.get).mock.calls.map(([url]) => url)).toEqual([
      "/api/patients/by-number/GM-2026-025",
    ]);
  });

  it("shows a retryable server error instead of claiming the patient does not exist", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { status: 503, data: { message: "خدمة البحث عن المرضى غير متاحة" } },
    });

    render(<PatientProfilePage />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة البحث عن المرضى غير متاحة");
    expect(screen.queryByText(/لا يوجد مريض برقم الملف/)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
    expect(vi.mocked(api.get).mock.calls.every(([url]) =>
      url === "/api/patients/by-number/GM-2026-025"
    )).toBe(true);
  });

  it("keeps a real 404 as a non-retryable not-found result", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { status: 404, data: { message: "المريض غير موجود" } },
    });

    render(<PatientProfilePage />);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "لا يوجد مريض برقم الملف GM-2026-025"
    );
    expect(screen.queryByRole("button", { name: "إعادة المحاولة" })).not.toBeInTheDocument();
  });

  it("uses the normal patient requests only when the route identifier is a GUID", async () => {
    navigation.id = "11111111-1111-1111-1111-111111111111";
    vi.mocked(api.get).mockImplementation(() => new Promise(() => undefined));

    render(<PatientProfilePage />);

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(4));
    const urls = vi.mocked(api.get).mock.calls.map(([url]) => url);
    expect(urls).toContain("/api/patients/11111111-1111-1111-1111-111111111111");
    expect(urls).toContain("/api/patients/11111111-1111-1111-1111-111111111111/summary");
    expect(urls.some((url) => String(url).includes("/by-number/"))).toBe(false);
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_032_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-032-bootstrap.yml").unlink(missing_ok=True)
