import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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

function changeLastTextarea(value: string) {
  const textareas = screen.getAllByRole("textbox");
  const notes = textareas.at(-1);
  expect(notes).toBeDefined();
  fireEvent.change(notes!, { target: { value } });
}

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
    changeLastTextarea("نسخة محلية");
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
    changeLastTextarea("نسخة محلية");
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
      if (attempt === 0) {
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
