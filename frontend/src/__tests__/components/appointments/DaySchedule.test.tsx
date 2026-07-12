import { describe, it, expect, vi, beforeEach } from "vitest";
import { render } from "@testing-library/react";
import { screen, waitFor, fireEvent } from "@testing-library/dom";
import { DaySchedule } from "@/components/appointments/DaySchedule";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), put: vi.fn(), post: vi.fn(), delete: vi.fn() },
}));
vi.mock("@/stores/toastStore", () => ({
  toast: { error: vi.fn(), success: vi.fn(), info: vi.fn() },
}));
vi.mock("@/hooks/usePermissions", () => ({
  hasPermission: () => true,
  PERMISSION_KEYS: {},
}));
vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { role: "Admin" } }),
}));

const SERVER_ERROR = { isAxiosError: true, response: { status: 500 } };

const APPOINTMENT = {
  id: "appt-1",
  patientId: "p-1",
  patientName: "مريض تجريبي",
  patientNumber: "P-001",
  appointmentDate: "2026-07-10",
  startTime: "09:00",
  endTime: "09:30",
  status: "InProgress",
  type: "Checkup",
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

function mockGets({ appointments }: { appointments: unknown }) {
  vi.mocked(api.get).mockImplementation(async (url: string) => {
    if (url.includes("/api/appointments?from=")) {
      if (appointments && typeof appointments === "object" && "isAxiosError" in (appointments as object)) {
        throw appointments;
      }
      return { data: appointments };
    }
    if (url.includes("/api/visits")) return { data: { data: [] } };
    if (url.includes("/email-available")) return { data: { hasEmail: false } };
    throw new Error(`unmocked GET ${url}`);
  });
}

function mockAppointmentAndEmail(url: string) {
  if (url.includes("/api/appointments?from=")) {
    return Promise.resolve({ data: [APPOINTMENT] });
  }
  if (url.includes("/email-available")) {
    return Promise.resolve({ data: { hasEmail: false } });
  }
  return null;
}

// SEQ-11 (#646): the day schedule must (a) show a visible error instead of an
// empty day when the fetch fails, and (b) never flip a card's status locally
// when the server rejected the update (false "مكتمل" until reload).
describe("DaySchedule", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows an error banner with retry — not the empty-day state — when the fetch fails", async () => {
    mockGets({ appointments: SERVER_ERROR });
    render(<DaySchedule date="2026-07-10" />);
    await waitFor(() =>
      expect(screen.getByText(/تعذر تحميل مواعيد هذا اليوم/)).toBeInTheDocument(),
    );
    expect(screen.queryByText("لا توجد مواعيد في هذا اليوم")).not.toBeInTheDocument();
    expect(screen.getByText("إعادة المحاولة")).toBeInTheDocument();
  });

  it("keeps the card status and shows the server message when a status update fails", async () => {
    mockGets({ appointments: [APPOINTMENT] });
    vi.mocked(api.put).mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { message: "لا يمكن إكمال الموعد" } },
    });

    render(<DaySchedule date="2026-07-10" />);
    await waitFor(() => expect(screen.getByText("مريض تجريبي")).toBeInTheDocument());

    // InProgress quick transition "اكتمل" lives in the card kebab menu
    fireEvent.click(screen.getByRole("button", { name: "خيارات" }));
    fireEvent.click(await screen.findByText("اكتمل"));

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith("لا يمكن إكمال الموعد"),
    );
    // The card must still show the old status, not a false "مكتمل".
    expect(screen.getByText("جاري العلاج")).toBeInTheDocument();
    expect(screen.queryByText("مكتمل")).not.toBeInTheDocument();
  });

  it("flips the card only after the server accepts the update", async () => {
    mockGets({ appointments: [APPOINTMENT] });
    vi.mocked(api.put).mockResolvedValue({ data: {} });

    render(<DaySchedule date="2026-07-10" />);
    await waitFor(() => expect(screen.getByText("مريض تجريبي")).toBeInTheDocument());

    fireEvent.click(screen.getByRole("button", { name: "خيارات" }));
    fireEvent.click(await screen.findByText("اكتمل"));

    await waitFor(() => expect(screen.getByText("مكتمل")).toBeInTheDocument());
    expect(toast.error).not.toHaveBeenCalled();
  });
});

describe("SEQ-33 DaySchedule visit existence guard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.post).mockResolvedValue({ data: {} });
  });

  it("does not expose start visit while the existence check is pending", async () => {
    const visitCheck = deferred<{ data: { data: { appointmentId?: string }[] } }>();
    vi.mocked(api.get).mockImplementation((url: string) => {
      const common = mockAppointmentAndEmail(url);
      if (common) return common as never;
      if (url.includes("/api/visits")) return visitCheck.promise as never;
      throw new Error(`unmocked GET ${url}`);
    });

    render(<DaySchedule date="2026-07-10" />);
    await screen.findByText("مريض تجريبي");
    fireEvent.click(screen.getByRole("button", { name: "خيارات" }));

    expect(screen.getByText("جارٍ التحقق من وجود زيارة...")).toBeDisabled();
    expect(screen.queryByRole("button", { name: "بدء الزيارة" })).not.toBeInTheDocument();
    expect(api.post).not.toHaveBeenCalled();

    visitCheck.resolve({ data: { data: [] } });

    expect(await screen.findByRole("button", { name: "بدء الزيارة" })).toBeInTheDocument();
    expect(screen.queryByText("جارٍ التحقق من وجود زيارة...")).not.toBeInTheDocument();
  });

  it("keeps start visit blocked after failure and allows retry", async () => {
    const retryCheck = deferred<{ data: { data: { appointmentId?: string }[] } }>();
    let visitCalls = 0;
    vi.mocked(api.get).mockImplementation((url: string) => {
      const common = mockAppointmentAndEmail(url);
      if (common) return common as never;
      if (url.includes("/api/visits")) {
        visitCalls += 1;
        if (visitCalls === 1) return Promise.reject(SERVER_ERROR) as never;
        return retryCheck.promise as never;
      }
      throw new Error(`unmocked GET ${url}`);
    });

    render(<DaySchedule date="2026-07-10" />);
    await screen.findByText("مريض تجريبي");
    fireEvent.click(screen.getByRole("button", { name: "خيارات" }));

    const retryButton = await screen.findByRole("button", {
      name: "تعذر التحقق من الزيارة — إعادة المحاولة",
    });
    expect(screen.queryByRole("button", { name: "بدء الزيارة" })).not.toBeInTheDocument();

    fireEvent.click(retryButton);

    expect(await screen.findByText("جارٍ التحقق من وجود زيارة...")).toBeDisabled();
    expect(screen.queryByRole("button", { name: "بدء الزيارة" })).not.toBeInTheDocument();
    expect(visitCalls).toBe(2);

    retryCheck.resolve({ data: { data: [] } });

    expect(await screen.findByRole("button", { name: "بدء الزيارة" })).toBeInTheDocument();
  });

  it("does not offer start visit when the server reports an existing visit", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      const common = mockAppointmentAndEmail(url);
      if (common) return common as never;
      if (url.includes("/api/visits")) {
        return Promise.resolve({
          data: { data: [{ appointmentId: APPOINTMENT.id }] },
        }) as never;
      }
      throw new Error(`unmocked GET ${url}`);
    });

    render(<DaySchedule date="2026-07-10" />);
    await screen.findByText("مريض تجريبي");
    fireEvent.click(screen.getByRole("button", { name: "خيارات" }));

    expect(await screen.findByText("فتح ملف المريض")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "بدء الزيارة" })).not.toBeInTheDocument();
    expect(screen.queryByText("تعذر التحقق من الزيارة — إعادة المحاولة")).not.toBeInTheDocument();
  });
});
