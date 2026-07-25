import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
