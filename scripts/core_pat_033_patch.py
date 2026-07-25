from pathlib import Path

for filename, type_name, endpoint, fallback_load, fallback_save in [
    ("MedicalHistoryTab.tsx", "MedicalHistory", "medical-history", "فشل تحميل التاريخ الطبي", "فشل حفظ التاريخ الطبي"),
    ("DentalHistoryTab.tsx", "DentalHistory", "dental-history", "فشل تحميل التاريخ السني", "فشل حفظ التاريخ السني"),
]:
    path = Path("frontend/src/components/patient/tabs") / filename
    text = path.read_text(encoding="utf-8")

    old = 'import api from "@/lib/api";\nimport { toast } from "@/stores/toastStore";'
    new = 'import api from "@/lib/api";\nimport { extractErrorMessage } from "@/lib/errors";\nimport { toast } from "@/stores/toastStore";'
    if old not in text:
        raise SystemExit(f"imports target not found in {filename}")
    text = text.replace(old, new, 1)

    old = '  const [fetchError, setFetchError] = useState(false);'
    new = '  const [fetchError, setFetchError] = useState("");'
    if old not in text:
        raise SystemExit(f"fetchError target not found in {filename}")
    text = text.replace(old, new, 1)

    old = f'''  useEffect(() => {{
    if (!initialData) {{
      setFetchError(false);
      api.get<{type_name}>(`/api/patients/${{patientId}}/{endpoint}`)
        .then((r) => setData(r.data))
        .catch(() => {{ setFetchError(true); }})
        .finally(() => setLoading(false));
    }}
  }}, [patientId, initialData, retryKey]);
'''
    new = f'''  useEffect(() => {{
    if (initialData) {{
      setData(initialData);
      setLoading(false);
      setFetchError("");
      return;
    }}

    let cancelled = false;
    setLoading(true);
    setFetchError("");
    api.get<{type_name}>(`/api/patients/${{patientId}}/{endpoint}`)
      .then((r) => {{ if (!cancelled) setData(r.data); }})
      .catch((error: unknown) => {{
        if (!cancelled) {{
          setData(null);
          setFetchError(extractErrorMessage(error, "{fallback_load}"));
        }}
      }})
      .finally(() => {{ if (!cancelled) setLoading(false); }});

    return () => {{ cancelled = true; }};
  }}, [patientId, initialData, retryKey]);
'''
    if old not in text:
        raise SystemExit(f"fetch effect target not found in {filename}")
    text = text.replace(old, new, 1)

    old = f'''    try {{
      await api.put(`/api/patients/${{patientId}}/{endpoint}`, form);
      setData({{ ...form }} as {type_name});
      setEditing(false);
      setForm({{}});
      toast.success("تم حفظ التاريخ {'الطبي' if type_name == 'MedicalHistory' else 'السني'} بنجاح");
    }} catch {{
      toast.error("{fallback_save}");
    }} finally {{
'''
    new = f'''    try {{
      const response = await api.put<{type_name}>(`/api/patients/${{patientId}}/{endpoint}`, form);
      // CORE-PAT-033: the server is the source of truth after validation and
      // normalization; do not display an optimistic local copy as persisted data.
      setData(response.data);
      setEditing(false);
      setForm({{}});
      toast.success("تم حفظ التاريخ {'الطبي' if type_name == 'MedicalHistory' else 'السني'} بنجاح");
    }} catch (error: unknown) {{
      toast.error(extractErrorMessage(error, "{fallback_save}"));
    }} finally {{
'''
    if old not in text:
        raise SystemExit(f"save target not found in {filename}")
    text = text.replace(old, new, 1)

    old = '''        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>'''
    new = '''        <p role="alert" className="text-sm text-red-600 mb-2">{fetchError}</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>'''
    if old not in text:
        raise SystemExit(f"error UI target not found in {filename}")
    text = text.replace(old, new, 1)

    path.write_text(text, encoding="utf-8")

test = Path("frontend/src/__tests__/components/patient/PatientHistorySaveReliability.test.tsx")
test.parent.mkdir(parents=True, exist_ok=True)
test.write_text('''import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { MedicalHistoryTab } from "@/components/patient/tabs/MedicalHistoryTab";
import { DentalHistoryTab } from "@/components/patient/tabs/DentalHistoryTab";

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), put: vi.fn() },
}));

vi.mock("@/stores/toastStore", () => ({ toast }));

const medicalHistory = {
  chronicDiseases: "",
  currentMedications: "",
  drugAllergies: "",
  bleedingDisorders: false,
  isPregnant: "N/A",
  tmjProblems: false,
  previousSurgeries: "",
  notes: "قبل الحفظ",
};

const dentalHistory = {
  chiefComplaint: "ألم",
  previousTreatments: "",
  mouthBreathing: false,
  bruxism: false,
  thumbSucking: false,
  tongueThrusing: false,
  notes: "قبل الحفظ",
};

describe("Patient history save reliability", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the normalized medical history returned by the server", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: medicalHistory });
    vi.mocked(api.put).mockResolvedValue({
      data: { ...medicalHistory, notes: "القيمة المعتمدة من الخادم" },
    });

    render(<MedicalHistoryTab patientId="patient-1" />);
    fireEvent.click(await screen.findByRole("button", { name: "تعديل" }));
    fireEvent.change(screen.getByLabelText("ملاحظات"), { target: { value: "نسخة محلية" } });
    fireEvent.click(screen.getByRole("button", { name: "حفظ" }));

    expect(await screen.findByText("القيمة المعتمدة من الخادم")).toBeInTheDocument();
    expect(screen.queryByText("نسخة محلية")).not.toBeInTheDocument();
  });

  it("renders the normalized dental history returned by the server", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: dentalHistory });
    vi.mocked(api.put).mockResolvedValue({
      data: { ...dentalHistory, notes: "السجل السني المعتمد" },
    });

    render(<DentalHistoryTab patientId="patient-1" />);
    fireEvent.click(await screen.findByRole("button", { name: "تعديل" }));
    fireEvent.change(screen.getByLabelText("ملاحظات"), { target: { value: "نسخة محلية" } });
    fireEvent.click(screen.getByRole("button", { name: "حفظ" }));

    expect(await screen.findByText("السجل السني المعتمد")).toBeInTheDocument();
    expect(screen.queryByText("نسخة محلية")).not.toBeInTheDocument();
  });

  it("shows the backend save conflict instead of a generic message", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: medicalHistory });
    vi.mocked(api.put).mockRejectedValue({
      response: { status: 409, data: { message: "تعارض في تحديث السجل الطبي — حاول مرة أخرى" } },
    });

    render(<MedicalHistoryTab patientId="patient-1" />);
    fireEvent.click(await screen.findByRole("button", { name: "تعديل" }));
    fireEvent.click(screen.getByRole("button", { name: "حفظ" }));

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalledWith("تعارض في تحديث السجل الطبي — حاول مرة أخرى");
    });
  });

  it("keeps the loading state during retry and does not show a false empty history", async () => {
    let attempt = 0;
    vi.mocked(api.get).mockImplementation(() => {
      if (attempt == 0) {
        return Promise.reject({ response: { status: 503, data: { message: "الخدمة غير متاحة" } } });
      }
      return Promise.resolve({ data: medicalHistory });
    });

    render(<MedicalHistoryTab patientId="patient-1" />);
    expect(await screen.findByRole("alert")).toHaveTextContent("الخدمة غير متاحة");

    attempt = 1;
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));
    expect(screen.queryByText("لا يوجد تاريخ طبي مسجّل")).not.toBeInTheDocument();
    expect(await screen.findByText("قبل الحفظ")).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledTimes(2);
  });
});
''', encoding="utf-8")

Path("scripts/core_pat_033_patch.py").unlink(missing_ok=True)
Path(".github/workflows/core-pat-033-bootstrap.yml").unlink(missing_ok=True)
