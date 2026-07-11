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

interface TestNotification {
  id: string;
  title: string;
  body: string;
  isRead: boolean;
  createdAt: string;
  relatedEntity?: string;
  relatedId?: string;
}

const notification: TestNotification = {
  id: "notification-1",
  title: "موعد يحتاج تأكيدًا",
  body: "راجع موعد المريض قبل نهاية اليوم",
  isRead: false,
  createdAt: "2026-07-12T00:00:00Z",
  relatedEntity: "Patient",
  relatedId: "patient-1",
};

const notificationWithoutRoute: TestNotification = {
  ...notification,
  id: "notification-no-route",
  title: "تنبيه إداري",
  relatedEntity: undefined,
  relatedId: undefined,
};

function listResponse(items: TestNotification[] = [notification], unreadCount = items.length) {
  return { data: { data: items, unreadCount } };
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

async function openNotifications() {
  fireEvent.click(screen.getByLabelText("الإشعارات"));
  await screen.findByText("موعد يحتاج تأكيدًا");
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
    await openNotifications();
    fireEvent.click(screen.getByText("موعد يحتاج تأكيدًا"));

    await waitFor(() => {
      expect(push).toHaveBeenCalledWith("/patients/patient-1");
    });
    expect(api.put).toHaveBeenCalledWith("/api/notifications/notification-1/read");
  });
});

describe("SEQ-29 TopbarNotifications mutation truth", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockResolvedValue(listResponse() as never);
    vi.mocked(api.put).mockResolvedValue({} as never);
    vi.mocked(api.delete).mockResolvedValue({} as never);
  });

  it("keeps notifications unread when mark-all fails", async () => {
    vi.mocked(api.put).mockRejectedValueOnce(new Error("Network Error"));

    render(<TopbarNotifications />);
    await openNotifications();
    fireEvent.click(screen.getByRole("button", { name: "تحديد الكل كمقروء" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذّر تعليم الإشعارات كمقروءة",
    );
    expect(screen.getByText("موعد يحتاج تأكيدًا")).toBeInTheDocument();
    expect(screen.getByTestId("notification-unread-indicator")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "تحديد الكل كمقروء" })).toBeInTheDocument();
    expect(invalidateQueries).not.toHaveBeenCalled();
  });

  it("updates the unread state only after mark-all succeeds", async () => {
    render(<TopbarNotifications />);
    await openNotifications();
    fireEvent.click(screen.getByRole("button", { name: "تحديد الكل كمقروء" }));

    await waitFor(() => {
      expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: "تحديد الكل كمقروء" })).not.toBeInTheDocument();
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["notificationUnreadCount"] });
  });

  it("does not remove a notification when deletion fails", async () => {
    vi.mocked(api.delete).mockRejectedValueOnce(new Error("Failed to fetch"));

    render(<TopbarNotifications />);
    await openNotifications();
    fireEvent.click(screen.getByRole("button", { name: "حذف الإشعار موعد يحتاج تأكيدًا" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("تعذّر حذف الإشعار");
    expect(screen.getByText("موعد يحتاج تأكيدًا")).toBeInTheDocument();
    expect(screen.getByTestId("notification-unread-indicator")).toBeInTheDocument();
    expect(invalidateQueries).not.toHaveBeenCalled();
  });

  it("removes the notification and updates unread count only after deletion succeeds", async () => {
    render(<TopbarNotifications />);
    await openNotifications();
    fireEvent.click(screen.getByRole("button", { name: "حذف الإشعار موعد يحتاج تأكيدًا" }));

    expect(await screen.findByText("لا توجد إشعارات")).toBeInTheDocument();
    expect(screen.queryByText("موعد يحتاج تأكيدًا")).not.toBeInTheDocument();
    expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    expect(api.delete).toHaveBeenCalledWith("/api/notifications/notification-1");
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["notificationUnreadCount"] });
  });

  it("keeps a notification unread when marking it read fails", async () => {
    vi.mocked(api.get).mockResolvedValueOnce(
      listResponse([notificationWithoutRoute], 1) as never,
    );
    vi.mocked(api.put).mockRejectedValueOnce(new Error("Network Error"));

    render(<TopbarNotifications />);
    fireEvent.click(screen.getByLabelText("الإشعارات"));
    fireEvent.click(await screen.findByText("تنبيه إداري"));

    expect(await screen.findByRole("alert")).toHaveTextContent("تعذّر تعليم الإشعار كمقروء");
    expect(screen.getByText("تنبيه إداري")).toBeInTheDocument();
    expect(screen.getByTestId("notification-unread-indicator")).toBeInTheDocument();
    expect(invalidateQueries).not.toHaveBeenCalled();
  });

  it("prevents duplicate mark-all requests while the first request is pending", async () => {
    const pending = deferred<Record<string, never>>();
    vi.mocked(api.put).mockReturnValueOnce(pending.promise as never);

    render(<TopbarNotifications />);
    await openNotifications();
    const markAllButton = screen.getByRole("button", { name: "تحديد الكل كمقروء" });

    fireEvent.click(markAllButton);
    fireEvent.click(markAllButton);

    expect(api.put).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: "جارٍ التحديث..." })).toBeDisabled();

    pending.resolve({});
    await waitFor(() => {
      expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    });
  });
});
