import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import api from "@/lib/api";
import PatientProfilePage from "@/app/(dashboard)/patients/[id]/page";

const navigation = vi.hoisted(() => ({
  id: "",
  replace: vi.fn(),
  push: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: navigation.id }),
  useRouter: () => ({ replace: navigation.replace, push: navigation.push }),
  useSearchParams: () => ({ get: () => null }),
}));

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: () => ({ user: { role: "Admin" } }),
}));

describe("Patient profile identifier routing", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    navigation.id = "GM-2026-025";
  });

  it("resolves a patient number without calling GUID-only endpoints", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: { id: "11111111-1111-1111-1111-111111111111" },
    });

    render(<PatientProfilePage />);

    await waitFor(() => {
      expect(navigation.replace).toHaveBeenCalledWith(
        "/patients/11111111-1111-1111-1111-111111111111"
      );
    });

    expect(vi.mocked(api.get).mock.calls.map(([url]) => url)).toEqual([
      "/api/patients/by-number/GM-2026-025",
    ]);
  });

  it("shows a retryable server error instead of claiming the patient does not exist", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { status: 503, data: { message: "خدمة البحث عن المرضى غير متاحة" } },
    });

    render(<PatientProfilePage />);

    expect(await screen.findByRole("alert")).toHaveTextContent("خدمة البحث عن المرضى غير متاحة");
    expect(screen.queryByText(/لا يوجد مريض برقم الملف/)).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "إعادة المحاولة" }));
    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
    expect(vi.mocked(api.get).mock.calls.every(([url]) =>
      url === "/api/patients/by-number/GM-2026-025"
    )).toBe(true);
  });

  it("keeps a real 404 as a non-retryable not-found result", async () => {
    vi.mocked(api.get).mockRejectedValue({
      response: { status: 404, data: { message: "المريض غير موجود" } },
    });

    render(<PatientProfilePage />);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "لا يوجد مريض برقم الملف GM-2026-025"
    );
    expect(screen.queryByRole("button", { name: "إعادة المحاولة" })).not.toBeInTheDocument();
  });

  it("uses the normal patient requests only when the route identifier is a GUID", async () => {
    navigation.id = "11111111-1111-1111-1111-111111111111";
    vi.mocked(api.get).mockImplementation(() => new Promise(() => undefined));

    render(<PatientProfilePage />);

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(4));
    const urls = vi.mocked(api.get).mock.calls.map(([url]) => url);
    expect(urls).toContain("/api/patients/11111111-1111-1111-1111-111111111111");
    expect(urls).toContain("/api/patients/11111111-1111-1111-1111-111111111111/summary");
    expect(urls.some((url) => String(url).includes("/by-number/"))).toBe(false);
  });
});
