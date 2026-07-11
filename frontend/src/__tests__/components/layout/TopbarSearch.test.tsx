import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TopbarSearch } from "@/components/layout/TopbarSearch";
import api from "@/lib/api";

const push = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
}));

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn() },
}));

interface SearchResponse {
  data: {
    patients: { id: string; fullName: string; patientNumber: string }[];
    appointments: { id: string; patientName: string; appointmentDate: string }[];
    orthoCases: { id: string; caseNumber: string; patientName: string; status: string }[];
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

function response(patientName: string, id: string): SearchResponse {
  return {
    data: {
      patients: [{ id, fullName: patientName, patientNumber: `P-${id}` }],
      appointments: [],
      orthoCases: [],
    },
  };
}

describe("SEQ-27 TopbarSearch request ordering", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("keeps the newest results when an older request resolves last", async () => {
    const oldRequest = deferred<SearchResponse>();
    const newRequest = deferred<SearchResponse>();
    vi.mocked(api.get)
      .mockReturnValueOnce(oldRequest.promise as never)
      .mockReturnValueOnce(newRequest.promise as never);

    render(<TopbarSearch />);
    const input = screen.getByLabelText("البحث السريع");

    fireEvent.change(input, { target: { value: "أحمد" } });
    await act(async () => { vi.advanceTimersByTime(300); });
    const oldSignal = vi.mocked(api.get).mock.calls[0]?.[1]?.signal;

    fireEvent.change(input, { target: { value: "محمد" } });
    await act(async () => { vi.advanceTimersByTime(300); });

    expect(oldSignal?.aborted).toBe(true);

    await act(async () => {
      newRequest.resolve(response("محمد علي", "2"));
      await Promise.resolve();
    });
    expect(screen.getByText("محمد علي")).toBeInTheDocument();

    await act(async () => {
      oldRequest.resolve(response("أحمد سالم", "1"));
      await Promise.resolve();
    });

    expect(screen.getByText("محمد علي")).toBeInTheDocument();
    expect(screen.queryByText("أحمد سالم")).not.toBeInTheDocument();
  });

  it("ignores a stale failure after a newer search succeeds", async () => {
    const oldRequest = deferred<SearchResponse>();
    const newRequest = deferred<SearchResponse>();
    vi.mocked(api.get)
      .mockReturnValueOnce(oldRequest.promise as never)
      .mockReturnValueOnce(newRequest.promise as never);

    render(<TopbarSearch />);
    const input = screen.getByLabelText("البحث السريع");

    fireEvent.change(input, { target: { value: "سالم" } });
    await act(async () => { vi.advanceTimersByTime(300); });
    fireEvent.change(input, { target: { value: "سليم" } });
    await act(async () => { vi.advanceTimersByTime(300); });

    await act(async () => {
      newRequest.resolve(response("سليم أحمد", "3"));
      await Promise.resolve();
    });
    await act(async () => {
      oldRequest.reject(new Error("stale failure"));
      await Promise.resolve();
    });

    expect(screen.getByText("سليم أحمد")).toBeInTheDocument();
    expect(screen.queryByText("تعذّر البحث، تحقق من الاتصال")).not.toBeInTheDocument();
  });

  it("shows an honest error and removes previous results when the current search fails", async () => {
    vi.mocked(api.get)
      .mockResolvedValueOnce(response("أحمد منصور", "4") as never)
      .mockRejectedValueOnce(new Error("network failure"));

    render(<TopbarSearch />);
    const input = screen.getByLabelText("البحث السريع");

    fireEvent.change(input, { target: { value: "أحمد" } });
    await act(async () => { vi.advanceTimersByTime(300); await Promise.resolve(); });
    expect(screen.getByText("أحمد منصور")).toBeInTheDocument();

    fireEvent.change(input, { target: { value: "محمد" } });
    await act(async () => { vi.advanceTimersByTime(300); await Promise.resolve(); });

    expect(screen.getByText("تعذّر البحث، تحقق من الاتصال")).toBeInTheDocument();
    expect(screen.queryByText("أحمد منصور")).not.toBeInTheDocument();
    expect(screen.queryByText(/لا توجد نتائج/)).not.toBeInTheDocument();
  });

  it("cancels the active request and clears the dropdown when the clear button is used", async () => {
    const pending = deferred<SearchResponse>();
    vi.mocked(api.get).mockReturnValueOnce(pending.promise as never);

    render(<TopbarSearch />);
    const input = screen.getByLabelText("البحث السريع");

    fireEvent.change(input, { target: { value: "نورا" } });
    await act(async () => { vi.advanceTimersByTime(300); });
    const signal = vi.mocked(api.get).mock.calls[0]?.[1]?.signal;

    fireEvent.click(screen.getByLabelText("مسح البحث"));

    expect(signal?.aborted).toBe(true);
    expect(input).toHaveValue("");
    expect(screen.queryByText("جارٍ البحث...")).not.toBeInTheDocument();

    await act(async () => {
      pending.resolve(response("نورا حسن", "5"));
      await Promise.resolve();
    });
    expect(screen.queryByText("نورا حسن")).not.toBeInTheDocument();
  });

  it("navigates to the selected patient and resets the search", async () => {
    vi.mocked(api.get).mockResolvedValueOnce(response("مريم علي", "6") as never);

    render(<TopbarSearch />);
    const input = screen.getByLabelText("البحث السريع");
    fireEvent.change(input, { target: { value: "مريم" } });
    await act(async () => { vi.advanceTimersByTime(300); await Promise.resolve(); });

    fireEvent.click(screen.getByText("مريم علي"));

    expect(push).toHaveBeenCalledWith("/patients/6");
    expect(input).toHaveValue("");
  });
});
