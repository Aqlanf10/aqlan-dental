import React from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import api from "@/lib/api";
import { useDoctorPatientsToday } from "@/app/(dashboard)/doctor-clinic/_lib/hooks";
import DoctorClinicError from "@/app/(dashboard)/doctor-clinic/error";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}));

class TestErrorBoundary extends React.Component<
  { children: React.ReactNode },
  { hasError: boolean }
> {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch() {
    // Expected in the failure-path test.
  }

  render() {
    if (this.state.hasError) {
      return <div>doctor-clinic-query-error</div>;
    }
    return this.props.children;
  }
}

function PatientsProbe() {
  const { data = [] } = useDoctorPatientsToday({ includeAll: true });
  return <div>{data.length === 0 ? "لا يوجد مرضى" : `${data.length} مرضى`}</div>;
}

function renderProbe() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });

  const view = render(
    <QueryClientProvider client={queryClient}>
      <TestErrorBoundary>
        <PatientsProbe />
      </TestErrorBoundary>
    </QueryClientProvider>,
  );

  return { queryClient, ...view };
}

const ONE_PATIENT = {
  appointmentId: "appointment-1",
  patientId: "patient-1",
  patientName: "مريض تجريبي",
  appointmentTime: "09:00",
  appointmentStatus: "InRoom",
  doctorId: "doctor-1",
  doctorName: "طبيب تجريبي",
  nextAction: "StartVisit",
};

describe("SEQ-16 doctor clinic honest load failures", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.clearAllMocks();
    vi.spyOn(console, "error").mockImplementation(() => undefined);
  });

  it("keeps the genuine empty state when the server successfully returns an empty list", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });

    renderProbe();

    expect(await screen.findByText("لا يوجد مرضى")).toBeInTheDocument();
    expect(screen.queryByText("doctor-clinic-query-error")).not.toBeInTheDocument();
  });

  it("throws an initial patient-list failure instead of rendering a false empty list", async () => {
    vi.mocked(api.get).mockRejectedValue({
      isAxiosError: true,
      response: { status: 500, data: { message: "تعذر تحميل رحلة المرضى" } },
    });

    renderProbe();

    await waitFor(() =>
      expect(screen.getByText("doctor-clinic-query-error")).toBeInTheDocument(),
    );
    expect(screen.queryByText("لا يوجد مرضى")).not.toBeInTheDocument();
  });

  it("keeps cached patients and does not tear down the workspace on a background refresh failure", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce({ data: [ONE_PATIENT] })
      .mockRejectedValueOnce({
        isAxiosError: true,
        response: { status: 503, data: { message: "انقطاع مؤقت" } },
      });

    const { queryClient } = renderProbe();
    expect(await screen.findByText("1 مرضى")).toBeInTheDocument();

    await queryClient.refetchQueries({
      queryKey: ["doctor-clinic", "patients", "all"],
    });

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
    expect(screen.getByText("1 مرضى")).toBeInTheDocument();
    expect(screen.queryByText("doctor-clinic-query-error")).not.toBeInTheDocument();
  });

  it("provides a specific Arabic error screen with an explicit retry action", () => {
    render(
      <DoctorClinicError
        error={new Error("server unavailable")}
        reset={vi.fn()}
      />,
    );

    expect(screen.getByRole("alert")).toHaveTextContent(
      "تعذر تحميل مرضى عيادة الطبيب",
    );
    expect(screen.getByText(/هذه ليست قائمة فارغة/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /إعادة المحاولة/ })).toBeInTheDocument();
  });
});
