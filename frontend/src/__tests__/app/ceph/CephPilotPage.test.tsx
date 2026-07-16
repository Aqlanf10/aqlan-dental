import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import CephPilotPage from "@/app/(dashboard)/ceph/pilot/page";
import api from "@/lib/api";

let role = "Admin";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
  },
}));

vi.mock("@/stores/authStore", () => ({
  useAuthStore: (selector: (state: { user: { id: string; role: string } }) => unknown) =>
    selector({ user: { id: "admin-1", role } }),
}));

describe("CephPilotPage", () => {
  beforeEach(() => {
    role = "Admin";
    vi.clearAllMocks();
    vi.mocked(api.get).mockImplementation(async url => {
      if (url === "/api/ceph-pilot/projects") {
        return {
          data: [{
            id: "project-1",
            name: "Pilot",
            code: "ADP-CEPH-WEBCEPH-PILOT-001",
            landmarkDefinitionVersion: "ADP-LM-LAT-v1.0",
            status: "Draft",
            revision: 1,
            caseCount: 0,
            readyCaseCount: 0,
          }],
        } as never;
      }
      if (url === "/api/users") return { data: [] } as never;
      if (url.includes("/cases")) return { data: [] } as never;
      throw new Error(`Unexpected URL: ${url}`);
    });
  });

  it("loads the admin-only de-identification and calibration workspace", async () => {
    render(<CephPilotPage />);

    expect(await screen.findByRole("heading", { name: "Pilot السيفالومتري" })).toBeInTheDocument();
    expect(screen.getByText("1. إزالة الهوية داخل المتصفح")).toBeInTheDocument();
    expect(screen.getByText("3. المعايرة")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "إدخال النسخة المنزوعة الهوية" })).toBeDisabled();
    expect(screen.getByLabelText("فحصت المعاينة النهائية وأؤكد إزالة الهوية بصريًا")).toBeDisabled();
    expect(screen.queryByText(/PatientId/)).not.toBeInTheDocument();
  });

  it("does not load Pilot data for a non-admin user", async () => {
    role = "Orthodontist";
    render(<CephPilotPage />);

    expect(await screen.findByRole("heading", { name: "مساحة الإدخال محصورة بالمدير" })).toBeInTheDocument();
    expect(api.get).not.toHaveBeenCalled();
  });
});
