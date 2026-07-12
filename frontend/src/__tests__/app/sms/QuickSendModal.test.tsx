import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { QuickSendModal } from "@/app/(dashboard)/sms/_components/QuickSendModal";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

vi.mock("@/stores/toastStore", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

interface PatientResponse {
  data: {
    data: { id: string; fullName: string; phone?: string }[];
  };
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

function patientResponse(id: string, fullName: string, phone = "777000000"): PatientResponse {
  return {
    data: {
      data: [{ id, fullName, phone }],
    },
  };
}

function installPatientSearchRequests(
  requests: Array<ReturnType<typeof deferred<PatientResponse>>>,
) {
  const patientCalls: Array<{ url: string; signal?: AbortSignal }> = [];

  vi.mocked(api.get).mockImplementation((url, config) => {
    if (url === "/api/sms/templates") {
      return Promise.resolve({ data: [] }) as never;
    }

    if (typeof url === "string" && url.startsWith("/api/patients?search=")) {
      const request = requests[patientCalls.length];
      if (!request) throw new Error(`Unexpected patient search ${url}`);
      patientCalls.push({
        url,
        signal: config?.signal as AbortSignal | undefined,
      });
      return request.promise as never;
    }

    throw new Error(`Unexpected GET ${String(url)}`);
  });

  return patientCalls;
}

async function searchFor(value: string) {
  fireEvent.change(screen.getByPlaceholderText("ابحث عن مريض..."), {
    target: { value },
  });
}

describe("SEQ-34 QuickSendModal patient search ordering", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.post).mockResolvedValue({ data: {} } as never);
  });

  it("keeps the newest patient results when an older success resolves last", async () => {
    const oldRequest = deferred<PatientResponse>();
    const newRequest = deferred<PatientResponse>();
    const patientCalls = installPatientSearchRequests([oldRequest, newRequest]);

    render(<QuickSendModal onClose={vi.fn()} />);

    await searchFor("محمد");
    await waitFor(() => expect(patientCalls).toHaveLength(1), { timeout: 2_000 });

    await searchFor("أحمد");
    await waitFor(() => expect(patientCalls).toHaveLength(2), { timeout: 2_000 });
    expect(patientCalls[0].signal?.aborted).toBe(true);

    newRequest.resolve(patientResponse("patient-new", "أحمد الجديد"));
    expect(await screen.findByText("أحمد الجديد")).toBeInTheDocument();

    oldRequest.resolve(patientResponse("patient-old", "محمد القديم"));
    await waitFor(() => {
      expect(screen.queryByText("محمد القديم")).not.toBeInTheDocument();
    });
    expect(screen.getByText("أحمد الجديد")).toBeInTheDocument();
  });

  it("ignores an older failure after the newest search succeeds", async () => {
    const oldRequest = deferred<PatientResponse>();
    const newRequest = deferred<PatientResponse>();
    const patientCalls = installPatientSearchRequests([oldRequest, newRequest]);

    render(<QuickSendModal onClose={vi.fn()} />);

    await searchFor("محمد");
    await waitFor(() => expect(patientCalls).toHaveLength(1), { timeout: 2_000 });
    await searchFor("سارة");
    await waitFor(() => expect(patientCalls).toHaveLength(2), { timeout: 2_000 });

    newRequest.resolve(patientResponse("patient-new", "سارة الحديثة"));
    expect(await screen.findByText("سارة الحديثة")).toBeInTheDocument();

    oldRequest.reject(new Error("Network Error"));
    await waitFor(() => {
      expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });
    expect(screen.getByText("سارة الحديثة")).toBeInTheDocument();
  });

  it("clears and invalidates a pending search when the text becomes too short", async () => {
    const pendingRequest = deferred<PatientResponse>();
    const patientCalls = installPatientSearchRequests([pendingRequest]);

    render(<QuickSendModal onClose={vi.fn()} />);

    await searchFor("محمد");
    await waitFor(() => expect(patientCalls).toHaveLength(1), { timeout: 2_000 });

    await searchFor("م");
    expect(patientCalls[0].signal?.aborted).toBe(true);

    pendingRequest.resolve(patientResponse("patient-old", "محمد القديم"));
    await waitFor(() => {
      expect(screen.queryByText("محمد القديم")).not.toBeInTheDocument();
    });
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("shows the current failure and retries the same search safely", async () => {
    const failedRequest = deferred<PatientResponse>();
    const retryRequest = deferred<PatientResponse>();
    const patientCalls = installPatientSearchRequests([failedRequest, retryRequest]);

    render(<QuickSendModal onClose={vi.fn()} />);

    await searchFor("ليلى");
    await waitFor(() => expect(patientCalls).toHaveLength(1), { timeout: 2_000 });
    failedRequest.reject(new Error("Network Error"));

    expect(await screen.findByRole("alert")).toHaveTextContent("تعذر البحث عن المرضى");
    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));

    await waitFor(() => expect(patientCalls).toHaveLength(2), { timeout: 2_000 });
    retryRequest.resolve(patientResponse("patient-retry", "ليلى الصحيحة"));

    expect(await screen.findByText("ليلى الصحيحة")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
