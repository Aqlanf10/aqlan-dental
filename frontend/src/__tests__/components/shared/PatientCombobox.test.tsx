import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PatientCombobox } from "@/components/shared/PatientCombobox";
import api from "@/lib/api";
import type { PatientListItem } from "@/types/patient";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

function patient(id: string, fullName: string): PatientListItem {
  return {
    id,
    patientNumber: `P-${id}`,
    fullName,
    createdAt: "2026-07-12T00:00:00Z",
    isActive: true,
  };
}

function response(items: PatientListItem[]) {
  return {
    data: {
      data: items,
      page: 1,
      pageSize: 8,
      totalCount: items.length,
      totalPages: 1,
    },
  };
}

describe("SEQ-26 PatientCombobox request ordering", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("keeps the newest search results when an older request resolves last", async () => {
    const oldRequest = deferred<ReturnType<typeof response>>();
    const newRequest = deferred<ReturnType<typeof response>>();
    vi.mocked(api.get)
      .mockReturnValueOnce(oldRequest.promise)
      .mockReturnValueOnce(newRequest.promise);

    render(<PatientCombobox onSelect={vi.fn()} />);
    const input = screen.getByPlaceholderText("ابحث بالاسم أو رقم المريض...");

    fireEvent.change(input, { target: { value: "أحمد" } });
    await act(async () => { vi.advanceTimersByTime(300); });

    const firstSignal = vi.mocked(api.get).mock.calls[0]?.[1]?.signal;

    fireEvent.change(input, { target: { value: "محمد" } });
    await act(async () => { vi.advanceTimersByTime(300); });

    expect(firstSignal?.aborted).toBe(true);
    expect(api.get).toHaveBeenCalledTimes(2);

    await act(async () => {
      newRequest.resolve(response([patient("2", "محمد علي")]));
      await Promise.resolve();
    });
    expect(screen.getByText("محمد علي")).toBeInTheDocument();

    await act(async () => {
      oldRequest.resolve(response([patient("1", "أحمد سالم")]));
      await Promise.resolve();
    });

    expect(screen.getByText("محمد علي")).toBeInTheDocument();
    expect(screen.queryByText("أحمد سالم")).not.toBeInTheDocument();
  });

  it("ignores a stale failure after a newer search succeeds", async () => {
    const oldRequest = deferred<ReturnType<typeof response>>();
    const newRequest = deferred<ReturnType<typeof response>>();
    vi.mocked(api.get)
      .mockReturnValueOnce(oldRequest.promise)
      .mockReturnValueOnce(newRequest.promise);

    render(<PatientCombobox onSelect={vi.fn()} />);
    const input = screen.getByPlaceholderText("ابحث بالاسم أو رقم المريض...");

    fireEvent.change(input, { target: { value: "سالم" } });
    await act(async () => { vi.advanceTimersByTime(300); });
    fireEvent.change(input, { target: { value: "سليم" } });
    await act(async () => { vi.advanceTimersByTime(300); });

    await act(async () => {
      newRequest.resolve(response([patient("3", "سليم أحمد")]));
      await Promise.resolve();
    });
    await act(async () => {
      oldRequest.reject(new Error("stale network failure"));
      await Promise.resolve();
    });

    expect(screen.getByText("سليم أحمد")).toBeInTheDocument();
    expect(screen.queryByText("تعذّر البحث، تحقق من الاتصال")).not.toBeInTheDocument();
  });

  it("clears loading and ignores an in-flight response when the query becomes too short", async () => {
    const pendingRequest = deferred<ReturnType<typeof response>>();
    vi.mocked(api.get).mockReturnValueOnce(pendingRequest.promise);

    render(<PatientCombobox onSelect={vi.fn()} />);
    const input = screen.getByPlaceholderText("ابحث بالاسم أو رقم المريض...");

    fireEvent.change(input, { target: { value: "نورا" } });
    await act(async () => { vi.advanceTimersByTime(300); });
    fireEvent.change(input, { target: { value: "ن" } });

    await act(async () => {
      pendingRequest.resolve(response([patient("4", "نورا حسن")]));
      await Promise.resolve();
    });

    expect(screen.queryByText("نورا حسن")).not.toBeInTheDocument();
    expect(screen.queryByText("جارٍ البحث...")).not.toBeInTheDocument();
  });
});
