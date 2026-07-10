import { describe, it, expect, vi, beforeEach } from "vitest";
import { render } from "@testing-library/react";
import { screen, waitFor, fireEvent } from "@testing-library/dom";
import DashboardPage from "@/app/(dashboard)/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

// Heavy self-fetching children are covered by their own tests — stub them so
// this file only measures the page's own stats/recent-patients behavior.
vi.mock("@/components/dashboard/AttentionAlerts", () => ({
  AttentionAlerts: () => null,
}));
vi.mock("@/components/dashboard/DashboardCharts", () => ({
  DashboardCharts: () => null,
}));
vi.mock("@/components/dashboard/TodaySchedule", () => ({
  TodaySchedule: () => null,
}));

const SERVER_ERROR = { isAxiosError: true, response: { status: 500 } };

const OK_STATS = {
  data: {
    totalPatients: 120,
    newPatientsToday: 3,
    appointmentsToday: 14,
    activeOrthoCases: 22,
    totalRevenueMTD: 250000,
    overdueContractsCount: 1,
    pendingLabOrders: 4,
    queueWaitingCount: 5,
    todayArrivedCount: 7,
    pendingBookingRequestsCount: 2,
  },
};
const OK_RECENTS = { data: { data: [] } };

function mockGetByUrl(overrides: Record<string, unknown>) {
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    for (const [needle, result] of Object.entries(overrides)) {
      if (url.includes(needle)) {
        if (result && typeof result === "object" && "isAxiosError" in result) throw result;
        return result;
      }
    }
    throw new Error(`unmocked GET ${url}`);
  });
}

// SEQ-11 (#646): a server outage must never render the landing page as an
// operational clinic with zero patients / zero revenue.
describe("DashboardPage (landing stats)", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders the real numbers when stats load", async () => {
    mockGetByUrl({ "/api/dashboard/stats": OK_STATS, "/api/patients": OK_RECENTS });
    render(<DashboardPage />);
    await waitFor(() => expect(screen.getByText("120")).toBeInTheDocument());
    expect(screen.queryByText(/تعذر تحميل إحصائيات اللوحة/)).not.toBeInTheDocument();
    expect(screen.getByText("لا يوجد مرضى بعد")).toBeInTheDocument();
  });

  it("shows the error banner and dashes — not fake zeros — when stats fail", async () => {
    mockGetByUrl({ "/api/dashboard/stats": SERVER_ERROR, "/api/patients": OK_RECENTS });
    render(<DashboardPage />);
    await waitFor(() =>
      expect(screen.getByText(/تعذر تحميل إحصائيات اللوحة/)).toBeInTheDocument(),
    );
    // Every stat slot (4 cards + 2 extra cards + 3 queue tiles) shows an
    // unknown marker; no misleading "0" anywhere in the stats.
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(9);
    expect(screen.getByText("إعادة المحاولة")).toBeInTheDocument();
  });

  it("retry re-fetches and recovers to real numbers", async () => {
    mockGetByUrl({ "/api/dashboard/stats": SERVER_ERROR, "/api/patients": OK_RECENTS });
    render(<DashboardPage />);
    await waitFor(() =>
      expect(screen.getByText(/تعذر تحميل إحصائيات اللوحة/)).toBeInTheDocument(),
    );

    mockGetByUrl({ "/api/dashboard/stats": OK_STATS, "/api/patients": OK_RECENTS });
    fireEvent.click(screen.getByText("إعادة المحاولة"));

    await waitFor(() => expect(screen.getByText("120")).toBeInTheDocument());
    expect(screen.queryByText(/تعذر تحميل إحصائيات اللوحة/)).not.toBeInTheDocument();
  });

  it("distinguishes a recent-patients failure from a genuinely empty list", async () => {
    mockGetByUrl({ "/api/dashboard/stats": OK_STATS, "/api/patients": SERVER_ERROR });
    render(<DashboardPage />);
    await waitFor(() =>
      expect(screen.getByText(/تعذر تحميل أحدث المرضى/)).toBeInTheDocument(),
    );
    expect(screen.queryByText("لا يوجد مرضى بعد")).not.toBeInTheDocument();
  });
});
