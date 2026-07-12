import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TopbarNotifications } from "@/components/layout/TopbarNotifications";
import api from "@/lib/api";

const push = vi.fn();
const UNREAD_COUNT_QUERY_KEY = ["notificationUnreadCount"] as const;

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
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
}

interface CountResponse {
  data: { count: number };
}

const notification: TestNotification = {
  id: "notification-1",
  title: "إشعار غير مقروء",
  body: "يجب ألا يعيد الطلب القديم عداده",
  isRead: false,
  createdAt: "2026-07-12T00:00:00Z",
};

const readNotification: TestNotification = {
  ...notification,
  id: "notification-read",
  title: "إشعار مقروء",
  isRead: true,
};

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function createTestClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: Infinity,
      },
    },
  });
}

function renderNotifications(queryClient: QueryClient) {
  return render(
    <QueryClientProvider client={queryClient}>
      <TopbarNotifications />
    </QueryClientProvider>,
  );
}

function installUnreadCountRace(listNotification: TestNotification = notification) {
  const staleCountRequest = deferred<CountResponse>();
  let unreadCountCalls = 0;
  let staleSignal: AbortSignal | undefined;

  vi.mocked(api.get).mockImplementation((url, config) => {
    if (url === "/api/notifications/unread-count") {
      unreadCountCalls += 1;

      if (unreadCountCalls === 1) {
        return Promise.resolve({ data: { count: 1 } }) as never;
      }

      if (unreadCountCalls === 2) {
        staleSignal = config?.signal as AbortSignal | undefined;
        return staleCountRequest.promise as never;
      }

      return Promise.resolve({ data: { count: 0 } }) as never;
    }

    if (url === "/api/notifications?page=1&pageSize=15") {
      return Promise.resolve({
        data: {
          data: [listNotification],
          unreadCount: 1,
        },
      }) as never;
    }

    throw new Error(`Unexpected GET ${url}`);
  });

  return {
    staleCountRequest,
    getUnreadCountCalls: () => unreadCountCalls,
    getStaleSignal: () => staleSignal,
  };
}

async function openNotifications(title = notification.title) {
  fireEvent.click(screen.getByLabelText("الإشعارات"));
  expect(await screen.findByText(title)).toBeInTheDocument();
}

describe("SEQ-32 notification unread-count ordering", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.put).mockResolvedValue({} as never);
    vi.mocked(api.delete).mockResolvedValue({} as never);
  });

  it("does not restore the unread indicator from a count request older than mark-all", async () => {
    const queryClient = createTestClient();
    const race = installUnreadCountRace();
    renderNotifications(queryClient);

    expect(await screen.findByTestId("notification-unread-indicator")).toBeInTheDocument();
    await openNotifications();

    const staleRefetch = queryClient
      .refetchQueries({ queryKey: UNREAD_COUNT_QUERY_KEY })
      .catch(() => undefined);
    await waitFor(() => expect(race.getUnreadCountCalls()).toBe(2));
    expect(race.getStaleSignal()?.aborted).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "تحديد الكل كمقروء" }));

    await waitFor(() => expect(race.getStaleSignal()?.aborted).toBe(true));
    await waitFor(() => {
      expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    });
    await waitFor(() => expect(race.getUnreadCountCalls()).toBeGreaterThanOrEqual(3));

    race.staleCountRequest.resolve({ data: { count: 1 } });
    await staleRefetch;

    expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    queryClient.clear();
  });

  it("does not restore the unread indicator from a count request older than deletion", async () => {
    const queryClient = createTestClient();
    const race = installUnreadCountRace();
    renderNotifications(queryClient);

    expect(await screen.findByTestId("notification-unread-indicator")).toBeInTheDocument();
    await openNotifications();

    const staleRefetch = queryClient
      .refetchQueries({ queryKey: UNREAD_COUNT_QUERY_KEY })
      .catch(() => undefined);
    await waitFor(() => expect(race.getUnreadCountCalls()).toBe(2));
    expect(race.getStaleSignal()?.aborted).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "حذف الإشعار إشعار غير مقروء" }));

    expect(await screen.findByText("لا توجد إشعارات")).toBeInTheDocument();
    await waitFor(() => expect(race.getStaleSignal()?.aborted).toBe(true));
    expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    await waitFor(() => expect(race.getUnreadCountCalls()).toBeGreaterThanOrEqual(3));

    race.staleCountRequest.resolve({ data: { count: 1 } });
    await staleRefetch;

    expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    queryClient.clear();
  });

  it("keeps an in-flight count refresh alive when deleting an already-read notification", async () => {
    const queryClient = createTestClient();
    const race = installUnreadCountRace(readNotification);
    renderNotifications(queryClient);

    expect(await screen.findByTestId("notification-unread-indicator")).toBeInTheDocument();
    await openNotifications(readNotification.title);

    const staleRefetch = queryClient
      .refetchQueries({ queryKey: UNREAD_COUNT_QUERY_KEY })
      .catch(() => undefined);
    await waitFor(() => expect(race.getUnreadCountCalls()).toBe(2));
    expect(race.getStaleSignal()?.aborted).toBe(false);

    fireEvent.click(screen.getByRole("button", { name: "حذف الإشعار إشعار مقروء" }));

    expect(await screen.findByText("لا توجد إشعارات")).toBeInTheDocument();
    expect(race.getStaleSignal()?.aborted).toBe(false);
    expect(race.getUnreadCountCalls()).toBe(2);

    race.staleCountRequest.resolve({ data: { count: 2 } });
    await staleRefetch;

    expect(screen.getByTestId("notification-unread-indicator")).toBeInTheDocument();
    queryClient.clear();
  });
});
