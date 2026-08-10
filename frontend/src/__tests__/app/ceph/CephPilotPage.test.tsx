import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import CephPilotPage from "@/app/(dashboard)/ceph/pilot/page";
import api from "@/lib/api";

let role = "Admin";

vi.mock("@/lib/api", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    patch: vi.fn(),
    put: vi.fn(),
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
            status: role === "Orthodontist" ? "ReadyForAnnotation" : "Draft",
            revision: 1,
            caseCount: 0,
            readyCaseCount: 0,
            myReviewerSlot: role === "Orthodontist" ? "A" : "Admin",
          }],
        } as never;
      }
      if (url === "/api/users") return { data: [] } as never;
      if (url.endsWith("/review")) throw { response: { status: 404 } };
      if (url.includes("/cases")) return {
        data: role === "Orthodontist" ? [{
          id: "case-1",
          projectId: "project-1",
          caseCode: "PILOT-001",
          imageWidth: 1000,
          imageHeight: 800,
          mmPerPixel: 0.2,
          calibrationSource: "CalibrationRuler",
          siteCode: "SITE-01",
          deviceCode: "DEVICE-01",
          qualityCategory: "Normal",
          status: "Ready",
          revision: 1,
        }] : [],
      } as never;
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

  it("opens the blinded assigned-case workspace for an orthodontist", async () => {
    role = "Orthodontist";
    render(<CephPilotPage />);

    expect(await screen.findByRole("heading", { name: "المراجعة السيفالومترية المعمّاة" })).toBeInTheDocument();
    expect(await screen.findByText("PILOT-001")).toBeInTheDocument();

    // The case list and the button's enabled state settle from two different loads, so a
    // synchronous assertion here raced the second one and failed roughly one run in three.
    // Waiting for the state is the fix; asserting the button merely exists would have made
    // the test pass without checking the thing it is named after.
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "بدء المراجعة المعمّاة" })).toBeEnabled());
  });
});
