import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TopbarNotifications } from "@/components/layout/TopbarNotifications";
import api from "@/lib/api";

const push = vi.fn();
const invalidateQueries = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@tanstack/react-query", () => ({
  useQuery: () => ({ data: { count: 2 } }),
  useQueryClient: () => ({ invalidateQueries }),
}));

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

const notification = {
  id: "notification-1",
  title: "موعد يحتاج تأكيدًا",
  body: "راجع موعد المريض قبل نهاية اليوم",
  isRead: false,
  createdAt: "2026-07-12T00:00:00Z",
  relatedEntity: "Patient",
  relatedId: "patient-1",
};

function listResponse(items = [notification], unreadCount = items.length) {
  return { data: { data: items, unreadCount } };
}

describe("SEQ-28 TopbarNotifications load states", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.put).mockResolvedValue({} as never);
    vi.mocked(api.delete).mockResolvedValue({} as never);
  });

  it("shows an honest Arabic error instead of a false empty state on initial failure", async () => {
    vi.mocked(api.get).mockRejectedValueOnce(new Error("Network Error"));

    render(<TopbarNotifications />);
    fireEvent.click(screen.getByLabelText("الإشعارات"));

    expect(await screen.findByRole("alert")).toHaveTextContent("تعذّر تحميل الإشعارات");
    expect(screen.queryByText("لا توجد إشعارات")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("retries the failed load and shows the genuine empty state only after success", async () => {
    vi.mocked(api.get)
      .mockRejectedValueOnce(new Error("Failed to fetch"))
      .mockResolvedValueOnce(listResponse([], 0) as never);

    render(<TopbarNotifications />);
    fireEvent.click(screen.getByLabelText("الإشعارات"));
    await screen.findByRole("alert");

    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("لا توجد إشعارات")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it("shows the empty message after a successful empty response", async () => {
    vi.mocked(api.get).mockResolvedValueOnce(listResponse([], 0) as never);

    render(<TopbarNotifications />);
    fireEvent.click(screen.getByLabelText("الإشعارات"));

    expect(await screen.findByText("لا توجد إشعارات")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("keeps previously loaded notifications visible when a later refresh fails", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce(listResponse() as never)
      .mockRejectedValueOnce(new Error("Network Error"));

    render(<TopbarNotifications />);
    const bell = screen.getByLabelText("الإشعارات");

    fireEvent.click(bell);
    expect(await screen.findByText("موعد يحتاج تأكيدًا")).toBeInTheDocument();

    fireEvent.click(bell);
    fireEvent.click(bell);

    expect(await screen.findByRole("alert")).toHaveTextContent("تعذّر تحميل الإشعارات");
    expect(screen.getByText("موعد يحتاج تأكيدًا")).toBeInTheDocument();
    expect(screen.queryByText("لا توجد إشعارات")).not.toBeInTheDocument();
  });

  it("preserves notification navigation after the dropdown extraction", async () => {
    vi.mocked(api.get).mockResolvedValueOnce(listResponse() as never);

    render(<TopbarNotifications />);
    fireEvent.click(screen.getByLabelText("الإشعارات"));
    fireEvent.click(await screen.findByText("موعد يحتاج تأكيدًا"));

    await waitFor(() => {
      expect(push).toHaveBeenCalledWith("/patients/patient-1");
    });
    expect(api.put).toHaveBeenCalledWith("/api/notifications/notification-1/read");
  });
});
