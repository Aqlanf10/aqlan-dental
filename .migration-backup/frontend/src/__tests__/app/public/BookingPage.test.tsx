import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "@testing-library/react";
import { fireEvent, screen, waitFor } from "@testing-library/dom";
import BookPage from "@/app/(public)/home/book/page";

vi.mock("@/hooks/useClinicBranding", () => ({
  useClinicBranding: () => ({
    whatsApp: "967770245745",
    phone: "04-253028",
    address: "تعز — شارع التحرير الأعلى",
  }),
}));

vi.mock("react-google-recaptcha-v3", () => ({
  useGoogleReCaptcha: () => ({ executeRecaptcha: undefined }),
}));

function jsonResponse(data: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => data,
  } as Response;
}

function futureClinicDate(): string {
  const date = new Date();
  date.setDate(date.getDate() + 7);
  while (date.getDay() === 5) date.setDate(date.getDate() + 1);
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, "0"),
    String(date.getDate()).padStart(2, "0"),
  ].join("-");
}

describe("public booking form", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input);
        if (url.includes("/api/public/booking-services")) return jsonResponse([]);
        if (url.includes("/api/public/doctors")) return jsonResponse([]);
        if (url.includes("/api/public/booking-availability")) {
          return jsonResponse({
            date: futureClinicDate(),
            serviceType: "تقويم الأسنان",
            isClosed: false,
            slots: [{ time: "09:00", available: true }],
          });
        }
        throw new Error(`Unmocked request: ${url}`);
      }),
    );
  });

  it("shows the complete essential booking flow on one screen", async () => {
    render(<BookPage />);

    expect(screen.getByLabelText("الاسم الكامل *")).toBeInTheDocument();
    expect(screen.getByLabelText("رقم الهاتف *")).toBeInTheDocument();
    expect(screen.getByLabelText("البريد الإلكتروني (اختياري)")).toBeInTheDocument();
    expect(screen.getByLabelText("نوع الخدمة *")).toBeInTheDocument();
    expect(screen.getByLabelText("التاريخ المفضل *")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إرسال طلب الحجز" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: "التالي" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "السابق" })).not.toBeInTheDocument();

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining("/api/public/doctors"),
        expect.objectContaining({ signal: expect.anything() }),
      ),
    );
  });

  it("reports all immediately actionable missing fields together", async () => {
    render(<BookPage />);

    await waitFor(() =>
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining("/api/public/doctors"),
        expect.objectContaining({ signal: expect.anything() }),
      ),
    );
    fireEvent.click(screen.getByRole("button", { name: "إرسال طلب الحجز" }));

    expect(screen.getByText("الاسم مطلوب")).toBeInTheDocument();
    expect(screen.getByText("رقم الهاتف مطلوب")).toBeInTheDocument();
    expect(screen.getByText("اختر نوع الخدمة")).toBeInTheDocument();
    expect(screen.getByText("التاريخ المفضل مطلوب")).toBeInTheDocument();
  });

  it("submits only once when the button is clicked repeatedly", async () => {
    let resolvePost!: (response: Response) => void;
    const postResponse = new Promise<Response>((resolve) => {
      resolvePost = resolve;
    });
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input);
      if (init?.method === "POST") return postResponse;
      if (url.includes("/api/public/booking-services")) return jsonResponse([]);
      if (url.includes("/api/public/doctors")) return jsonResponse([]);
      if (url.includes("/api/public/booking-availability")) {
        return jsonResponse({
          date: futureClinicDate(),
          serviceType: "تقويم الأسنان",
          isClosed: false,
          slots: [{ time: "09:00", available: true }],
        });
      }
      throw new Error(`Unmocked request: ${url}`);
    });

    render(<BookPage />);
    fireEvent.change(screen.getByLabelText("الاسم الكامل *"), {
      target: { value: "مريض اختبار الإطلاق" },
    });
    fireEvent.change(screen.getByLabelText("رقم الهاتف *"), {
      target: { value: "770000927" },
    });
    fireEvent.change(screen.getByLabelText("نوع الخدمة *"), {
      target: { value: "تقويم الأسنان" },
    });
    fireEvent.change(screen.getByLabelText("التاريخ المفضل *"), {
      target: { value: futureClinicDate() },
    });

    const timeButton = await screen.findByRole("button", { name: "9:00 ص" });
    fireEvent.click(timeButton);

    const submitButton = screen.getByRole("button", { name: "إرسال طلب الحجز" });
    fireEvent.click(submitButton);
    fireEvent.click(submitButton);

    await waitFor(() => {
      const postCalls = fetchMock.mock.calls.filter(([, init]) => init?.method === "POST");
      expect(postCalls).toHaveLength(1);
    });

    resolvePost(jsonResponse({}, 201));
    await screen.findByText("تم إرسال طلب الحجز بنجاح");
  });

  it("stops a stalled availability request and offers retry", async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.includes("/api/public/booking-services")) return jsonResponse([]);
      if (url.includes("/api/public/doctors")) return jsonResponse([]);
      if (url.includes("/api/public/booking-availability")) {
        expect(init?.signal).toBeDefined();
        throw new DOMException("Timed out", "AbortError");
      }
      throw new Error(`Unmocked request: ${url}`);
    });

    render(<BookPage />);
    fireEvent.change(screen.getByLabelText("التاريخ المفضل *"), {
      target: { value: futureClinicDate() },
    });

    expect(
      await screen.findByText("استغرق تحميل الأوقات وقتاً أطول من المتوقع"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إعادة المحاولة" })).toBeEnabled();
    expect(
      screen.queryByText("جاري تحميل الأوقات المتاحة..."),
    ).not.toBeInTheDocument();
  });
});
