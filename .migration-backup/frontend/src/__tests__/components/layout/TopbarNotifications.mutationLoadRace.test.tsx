import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TopbarNotifications } from "@/components/layout/TopbarNotifications";
import api from "@/lib/api";

const push = vi.fn();
const invalidateQueries = vi.fn();
const { unreadQueryData } = vi.hoisted(() => ({
  unreadQueryData: { count: 1 },
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@tanstack/react-query", () => ({
  useQuery: () => ({ data: unreadQueryData }),
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

interface ListResponse {
  data: {
    data: TestNotification[];
    unreadCount: number;
  };
}

const notification: TestNotification = {
  id: "notification-1",
  title: "إشعار قبل العملية",
  body: "بيانات قديمة يجب ألا تعود",
  isRead: false,
  createdAt: "2026-07-12T00:00:00Z",
};

function listResponse(items = [notification], unreadCount = items.length): ListResponse {
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

async function loadInitialAndStartRefresh(refresh: ReturnType<typeof deferred<ListResponse>>) {
  vi.mocked(api.get)
    .mockResolvedValueOnce(listResponse() as never)
    .mockReturnValueOnce(refresh.promise as never);

  render(<TopbarNotifications />);
  const bell = screen.getByLabelText("الإشعارات");

  fireEvent.click(bell);
  expect(await screen.findByText("إشعار قبل العملية")).toBeInTheDocument();

  fireEvent.click(bell);
  fireEvent.click(bell);

  await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  const signal = vi.mocked(api.get).mock.calls[1]?.[1]?.signal;
  expect(signal?.aborted).toBe(false);
  return signal;
}

describe("SEQ-31 notification mutation/load ordering", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    unreadQueryData.count = 1;
    vi.mocked(api.put).mockResolvedValue({} as never);
    vi.mocked(api.delete).mockResolvedValue({} as never);
  });

  it("does not resurrect a deleted notification when an older refresh resolves", async () => {
    const refresh = deferred<ListResponse>();
    const signal = await loadInitialAndStartRefresh(refresh);

    fireEvent.click(screen.getByRole("button", { name: "حذف الإشعار إشعار قبل العملية" }));

    expect(await screen.findByText("لا توجد إشعارات")).toBeInTheDocument();
    expect(signal?.aborted).toBe(true);
    expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();

    refresh.resolve(listResponse());
    await waitFor(() => {
      expect(screen.queryByText("إشعار قبل العملية")).not.toBeInTheDocument();
    });
    expect(screen.getByText("لا توجد إشعارات")).toBeInTheDocument();
  });

  it("does not restore unread state when an older refresh resolves after mark-all", async () => {
    const refresh = deferred<ListResponse>();
    const signal = await loadInitialAndStartRefresh(refresh);

    fireEvent.click(screen.getByRole("button", { name: "تحديد الكل كمقروء" }));

    await waitFor(() => {
      expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    });
    expect(signal?.aborted).toBe(true);
    expect(screen.queryByRole("button", { name: "تحديد الكل كمقروء" })).not.toBeInTheDocument();

    refresh.resolve(listResponse());
    await waitFor(() => {
      expect(screen.queryByTestId("notification-unread-indicator")).not.toBeInTheDocument();
    });
    expect(screen.queryByRole("button", { name: "تحديد الكل كمقروء" })).not.toBeInTheDocument();
  });
});
