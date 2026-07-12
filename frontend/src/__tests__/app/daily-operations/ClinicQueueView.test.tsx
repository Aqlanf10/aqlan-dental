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
  useDoctors: () => ({ data: [] }),
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

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function installQueueResponses(queueResponses: Array<Promise<unknown>>) {
  let queueCall = 0;

  vi.mocked(api.get).mockImplementation((url: string) => {
    if (url.startsWith("/api/clinic-queue/today")) {
      const response = queueResponses[queueCall];
      queueCall += 1;
      if (!response) throw new Error(`Unexpected queue request ${url}`);
      return response as never;
    }

    if (url.startsWith("/api/clinic-queue/analytics")) {
      return Promise.resolve({ data: {} }) as never;
    }

    if (url === "/api/clinic-queue/rooms") {
      return Promise.resolve({ data: [] }) as never;
    }

    throw new Error(`Unexpected GET ${url}`);
  });

  return () => queueCall;
}

describe("SEQ-35 ClinicQueueView load truth", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("shows an Arabic error instead of a false empty queue on initial failure", async () => {
    installQueueResponses([Promise.reject(new Error("Network Error"))]);

    render(<ClinicQueueView searchQuery="" />);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "تعذر تحميل قائمة الانتظار",
    );
    expect(screen.queryByText("لا يوجد مرضى في الانتظار")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeInTheDocument();
  });

  it("shows the genuine empty state only after a successful retry", async () => {
    const getQueueCalls = installQueueResponses([
      Promise.reject(new Error("Failed to fetch")),
      Promise.resolve({ data: [] }),
    ]);

    render(<ClinicQueueView searchQuery="" />);

    await screen.findByRole("alert");
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    expect(await screen.findByText("لا يوجد مرضى في الانتظار")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(getQueueCalls()).toBe(2);
  });

  it("keeps previously loaded patients visible when a later refresh fails", async () => {
    const refreshRequest = deferred<unknown>();
    installQueueResponses([
      Promise.resolve({ data: [queueItem] }),
      refreshRequest.promise,
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
});
