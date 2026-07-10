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
