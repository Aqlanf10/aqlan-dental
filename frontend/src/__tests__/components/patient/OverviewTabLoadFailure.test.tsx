import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
