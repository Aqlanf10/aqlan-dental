import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
