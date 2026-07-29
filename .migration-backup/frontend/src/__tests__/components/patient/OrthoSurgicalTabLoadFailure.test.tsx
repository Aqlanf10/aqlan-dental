import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import { OrthoSurgicalTab } from "@/components/patient/tabs/OrthoSurgicalTab";

vi.mock("@/lib/api", () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

vi.mock("next/link", () => ({
  default: ({ href, children, ...props }: { href: string; children: ReactNode }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

describe("OrthoSurgicalTab honest loading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows a retryable error instead of a false empty state when surgical cases fail", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.startsWith("/api/ortho-surgical-cases")) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "خدمة الحالات الجراحية غير متاحة" } },
        });
      }
      return Promise.resolve({ data: [] });
    });

    render(<OrthoSurgicalTab patientId="patient-1" />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة الحالات الجراحية غير متاحة");
    expect(screen.getByText(/لم نعتبر فشل الطلب/)).toBeInTheDocument();
    expect(screen.queryByText("لا توجد حالات تقويمية جراحية")).not.toBeInTheDocument();
  });

  it("keeps a genuine empty surgical list but disables creation when active ortho cases fail", async () => {
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (url.startsWith("/api/ortho-surgical-cases")) return Promise.resolve({ data: { data: [] } });
      return Promise.reject({
        isAxiosError: true,
        response: { status: 500, data: { message: "تعذر تحميل حالات التقويم النشطة" } },
      });
    });

    render(<OrthoSurgicalTab patientId="patient-1" />);

    expect(await screen.findByText("لا توجد حالات تقويمية جراحية")).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("تعذر تحميل حالات التقويم النشطة");
    expect(screen.queryByRole("button", { name: "إنشاء حالة تقويمية جراحية" })).not.toBeInTheDocument();
  });

  it("retries both requests without reloading the page", async () => {
    let attempt = 0;
    vi.mocked(api.get).mockImplementation((url: string) => {
      if (attempt === 0) {
        return Promise.reject({
          isAxiosError: true,
          response: { status: 503, data: { message: "انقطاع مؤقت" } },
        });
      }
      if (url.startsWith("/api/ortho-surgical-cases")) return Promise.resolve({ data: { data: [] } });
      return Promise.resolve({ data: [{ id: "ortho-1", caseNumber: "O-001", status: "Active" }] });
    });

    render(<OrthoSurgicalTab patientId="patient-1" />);
    const retry = await screen.findByRole("button", { name: "إعادة المحاولة" });
    attempt = 1;
    fireEvent.click(retry);

    expect(await screen.findByRole("button", { name: "إنشاء حالة تقويمية جراحية" })).toBeInTheDocument();
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(4));
    expect(screen.queryByText("انقطاع مؤقت")).not.toBeInTheDocument();
  });
});
