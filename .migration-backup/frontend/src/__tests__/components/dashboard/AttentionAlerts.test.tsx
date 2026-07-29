import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AttentionAlerts } from "@/components/dashboard/AttentionAlerts";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

const EMPTY_ALERTS = {
  overdueLabOrdersCount: 0,
  maxLabDaysOverdue: 0,
  todayNoShowCount: 0,
  longWaitingCount: 0,
  unconfirmedTomorrowCount: 0,
  recallCandidatesCount: 0,
};

const ALERTS = {
  ...EMPTY_ALERTS,
  overdueLabOrdersCount: 2,
  maxLabDaysOverdue: 4,
};

function renderAlerts() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: 0 },
    },
  });

  const view = render(
    <QueryClientProvider client={queryClient}>
      <AttentionAlerts />
    </QueryClientProvider>,
  );

  return { queryClient, ...view };
}

describe("SEQ-20 dashboard attention alerts", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.clearAllMocks();
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
  });

  it("renders alerts after a successful response", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: ALERTS });

    renderAlerts();

    expect(await screen.findByText(/تراكيب متأخرة في المختبر/)).toHaveTextContent(
      "أقدمها متأخر 4 يوم",
    );
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("renders the genuine empty state after a successful empty response", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: EMPTY_ALERTS });

    renderAlerts();

    expect(
      await screen.findByText(/لا توجد تنبيهات — كل شيء تحت السيطرة/),
    ).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("shows a compact server error instead of disappearing on initial failure", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { data: { message: "تعذر تحميل تنبيهات العيادة" } },
    });

    renderAlerts();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذر تحميل تنبيهات العيادة",
    );
    expect(screen.queryByText(/لا توجد تنبيهات/)).not.toBeInTheDocument();
  });

  it("keeps cached alerts visible when a background refresh fails", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce({ data: ALERTS })
      .mockRejectedValueOnce({
        response: { data: { message: "تعذر تحديث التنبيهات مؤقتًا" } },
      });

    const { queryClient } = renderAlerts();
    expect(await screen.findByText(/تراكيب متأخرة في المختبر/)).toBeInTheDocument();

    await queryClient.refetchQueries({ queryKey: ["dashboard-alerts"] });

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذر تحديث التنبيهات مؤقتًا",
    );
    expect(screen.getByText(/تراكيب متأخرة في المختبر/)).toBeInTheDocument();
  });

  it("retries the request from the error action", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce({ response: { status: 503 } })
      .mockResolvedValueOnce({ data: EMPTY_ALERTS });

    renderAlerts();
    const retryButton = await screen.findByRole("button", {
      name: "إعادة المحاولة",
    });

    fireEvent.click(retryButton);

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
    expect(
      await screen.findByText(/لا توجد تنبيهات — كل شيء تحت السيطرة/),
    ).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
