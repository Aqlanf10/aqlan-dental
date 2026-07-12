import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import ClinicQueueView from "@/app/(dashboard)/daily-operations/_modules/ClinicQueueView";
import api from "@/lib/api";

const invalidateQueries = vi.fn();

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
  },
}));

vi.mock("@/hooks/useDoctors", () => ({
  useDoctors: () => ({
    data: [
      { id: "doctor-1", name: "د. أحمد" },
      { id: "doctor-2", name: "د. سارة" },
    ],
  }),
}));

vi.mock("@tanstack/react-query", () => ({
  useQueryClient: () => ({ invalidateQueries }),
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: null }),
}));

vi.mock("@microsoft/signalr", () => ({
  HubConnectionBuilder: vi.fn(),
  LogLevel: { Warning: 3 },
}));

const queueItem = {
  id: "queue-1",
  patientId: "patient-1",
  patientName: "مريض الانتظار",
  patientNumber: "P-001",
  appointmentId: "appointment-1",
  appointmentTime: "09:00",
  doctorName: "د. أحمد",
  doctorId: "doctor-1",
  roomName: "غرفة 1",
  serviceName: "فحص",
  status: "Waiting",
  statusArabic: "في الانتظار",
  priority: "Normal",
  priorityArabic: "عادي",
  sortOrder: 1,
  recallCount: 0,
  position: 1,
  estimatedWaitMinutes: 10,
  createdAt: "2026-07-12T00:00:00Z",
};

const newestQueueItem = {
  ...queueItem,
  id: "queue-newest",
  patientId: "patient-newest",
  patientName: "المريض الأحدث",
  patientNumber: "P-NEW",
};

const staleQueueItem = {
  ...queueItem,
  id: "queue-stale",
  patientId: "patient-stale",
  patientName: "المريض القديم",
  patientNumber: "P-OLD",
};

const calledQueueItem = {
  ...queueItem,
  status: "Called",
  statusArabic: "تم النداء",
  calledAt: "2026-07-12T00:01:00Z",
};

const noShowQueueItem = {
  ...queueItem,
  status: "NoShow",
  statusArabic: "تم تسجيل عدم الحضور",
  noShowAt: "2026-07-12T00:02:00Z",
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

type QueueResponseFactory = () => Promise<unknown>;

function installQueueResponses(queueResponses: QueueResponseFactory[]) {
  const queueCalls: Array<{ signal?: { aborted: boolean }; url: string }> = [];

  vi.mocked(api.get).mockImplementation((url, config) => {
    if (url.startsWith("/api/clinic-queue/today")) {
      const responseFactory = queueResponses[queueCalls.length];
      if (!responseFactory) throw new Error(`Unexpected queue request ${url}`);
      queueCalls.push({ signal: config?.signal, url });
      return responseFactory() as never;
    }

    if (url.startsWith("/api/clinic-queue/analytics")) {
      return Promise.resolve({ data: {} }) as never;
    }

    if (url === "/api/clinic-queue/rooms") {
      return Promise.resolve({ data: [] }) as never;
    }

    throw new Error(`Unexpected GET ${url}`);
  });

  return queueCalls;
}

async function startTwoFilteredLoads(queueCalls: Array<{ signal?: { aborted: boolean }; url: string }>) {
  const doctorFilter = screen.getByRole("combobox");

  fireEvent.change(doctorFilter, { target: { value: "doctor-1" } });
  await waitFor(() => expect(queueCalls).toHaveLength(2));

  fireEvent.change(doctorFilter, { target: { value: "doctor-2" } });
  await waitFor(() => expect(queueCalls).toHaveLength(3));

  expect(queueCalls[1].url).toContain("doctorId=doctor-1");
  expect(queueCalls[2].url).toContain("doctorId=doctor-2");
  expect(queueCalls[1].signal?.aborted).toBe(true);
}

describe("SEQ-35/37/38 ClinicQueueView truth and synchronization", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("shows an Arabic error instead of a false empty queue on initial failure", async () => {
    installQueueResponses([() => Promise.reject(new Error("Network Error"))]);

    render(<ClinicQueueView searchQuery="" />);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذر تحميل قائمة الانتظار",
    );
    expect(screen.queryByText("لا يوجد مرضى في الانتظار")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("shows the genuine empty state only after a successful retry", async () => {
    const queueCalls = installQueueResponses([
      () => Promise.reject(new Error("Failed to fetch")),
      () => Promise.resolve({ data: [] }),
    ]);

    render(<ClinicQueueView searchQuery="" />);

    await screen.findByRole("alert");
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("لا يوجد مرضى في الانتظار")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(queueCalls).toHaveLength(2);
  });

  it("keeps previously loaded patients visible when a later refresh fails", async () => {
    const refreshRequest = deferred<unknown>();
    installQueueResponses([
      () => Promise.resolve({ data: [queueItem] }),
      () => refreshRequest.promise,
    ]);

    render(<ClinicQueueView searchQuery="" />);

    expect(await screen.findByText("مريض الانتظار")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "تحديث قائمة الانتظار" }));

    expect(await screen.findByText("جارٍ تحديث قائمة الانتظار...")).toBeInTheDocument();
    expect(screen.getByText("مريض الانتظار")).toBeInTheDocument();

    refreshRequest.reject(new Error("Network Error"));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذر تحميل قائمة الانتظار",
    );
    expect(screen.getByText("مريض الانتظار")).toBeInTheDocument();
    expect(screen.queryByText("لا يوجد مرضى في الانتظار")).not.toBeInTheDocument();

    await waitFor(() => {
      expect(screen.queryByText("جارٍ تحديث قائمة الانتظار...")).not.toBeInTheDocument();
    });
  });

  it("keeps the newest doctor-filter queue data when an older success resolves last", async () => {
    const olderRefresh = deferred<unknown>();
    const newerRefresh = deferred<unknown>();
    const queueCalls = installQueueResponses([
      () => Promise.resolve({ data: [queueItem] }),
      () => olderRefresh.promise,
      () => newerRefresh.promise,
    ]);

    render(<ClinicQueueView searchQuery="" />);
    expect(await screen.findByText("مريض الانتظار")).toBeInTheDocument();

    await startTwoFilteredLoads(queueCalls);

    newerRefresh.resolve({ data: [newestQueueItem] });
    expect(await screen.findByText("المريض الأحدث")).toBeInTheDocument();

    olderRefresh.resolve({ data: [staleQueueItem] });
    await waitFor(() => {
      expect(screen.queryByText("المريض القديم")).not.toBeInTheDocument();
    });
    expect(screen.getByText("المريض الأحدث")).toBeInTheDocument();
  });

  it("ignores an older doctor-filter failure after a newer queue load succeeds", async () => {
    const olderRefresh = deferred<unknown>();
    const newerRefresh = deferred<unknown>();
    const queueCalls = installQueueResponses([
      () => Promise.resolve({ data: [queueItem] }),
      () => olderRefresh.promise,
      () => newerRefresh.promise,
    ]);

    render(<ClinicQueueView searchQuery="" />);
    expect(await screen.findByText("مريض الانتظار")).toBeInTheDocument();

    await startTwoFilteredLoads(queueCalls);

    newerRefresh.resolve({ data: [newestQueueItem] });
    expect(await screen.findByText("المريض الأحدث")).toBeInTheDocument();

    olderRefresh.reject(new Error("Network Error"));
    await waitFor(() => {
      expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });
    expect(screen.getByText("المريض الأحدث")).toBeInTheDocument();
  });

  it("refreshes all queue views immediately after a successful call without waiting for SignalR", async () => {
    const queueCalls = installQueueResponses([
      () => Promise.resolve({ data: [queueItem] }),
      () => Promise.resolve({ data: [calledQueueItem] }),
    ]);
    vi.mocked(api.post).mockResolvedValue({ data: {} } as never);

    render(<ClinicQueueView searchQuery="" />);
    expect(await screen.findByText("مريض الانتظار")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "نداء" }));

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        "/api/clinic-queue/queue-1/call",
        { roomName: undefined },
      );
    });
    expect(await screen.findByText("تم النداء")).toBeInTheDocument();
    expect(queueCalls).toHaveLength(2);
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["daily-ops"] });
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["clinic-queue"] });
  });

  it("keeps a generic queue action locked until refreshed server truth arrives", async () => {
    const refreshedQueue = deferred<unknown>();
    const queueCalls = installQueueResponses([
      () => Promise.resolve({ data: [queueItem] }),
      () => refreshedQueue.promise,
    ]);
    vi.mocked(api.post).mockResolvedValue({ data: {} } as never);

    render(<ClinicQueueView searchQuery="" />);
    expect(await screen.findByText("مريض الانتظار")).toBeInTheDocument();

    const noShowButton = screen.getByRole("button", { name: "لم يحضر" });
    fireEvent.click(noShowButton);

    await waitFor(() => {
      expect(api.post).toHaveBeenCalledWith(
        "/api/clinic-queue/queue-1/no-show",
        undefined,
      );
    });
    expect(noShowButton).toBeDisabled();
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["daily-ops"] });
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ["clinic-queue"] });
    expect(queueCalls).toHaveLength(2);

    refreshedQueue.resolve({ data: [noShowQueueItem] });

    expect(await screen.findByText("تم تسجيل عدم الحضور")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "لم يحضر" })).not.toBeInTheDocument();
  });

  it("keeps the action locked after a failed resync until retry applies server truth", async () => {
    const queueCalls = installQueueResponses([
      () => Promise.resolve({ data: [queueItem] }),
      () => Promise.reject(new Error("Network Error")),
      () => Promise.resolve({ data: [noShowQueueItem] }),
    ]);
    vi.mocked(api.post).mockResolvedValue({ data: {} } as never);

    render(<ClinicQueueView searchQuery="" />);
    expect(await screen.findByText("مريض الانتظار")).toBeInTheDocument();

    const noShowButton = screen.getByRole("button", { name: "لم يحضر" });
    fireEvent.click(noShowButton);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذر تحميل قائمة الانتظار",
    );
    expect(noShowButton).toBeDisabled();
    fireEvent.click(noShowButton);
    expect(api.post).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("تم تسجيل عدم الحضور")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "لم يحضر" })).not.toBeInTheDocument();
    expect(queueCalls).toHaveLength(3);
  });
});
