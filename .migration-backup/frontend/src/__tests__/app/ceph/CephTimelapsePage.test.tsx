import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CephTimelapsePage from "@/app/(dashboard)/ceph/timelapse/[caseId]/page";
import api from "@/lib/api";

vi.mock("next/navigation", () => ({ useParams: () => ({ caseId: "case-1" }) }));
vi.mock("@/lib/api", () => ({ default: { get: vi.fn() } }));

const analysis = (id: string, date: string) => ({
  id,
  orthoCaseId: "case-1",
  caseNumber: "ORTHO-1",
  patientName: "مريض تجريبي",
  analysisType: "steiner",
  analysisDate: date,
  xrayFileUrl: `/uploads/${id}.jpg`,
  aiAssisted: false,
  landmarkCount: 24,
  hasMeasurements: true,
  isApproved: true,
  clinicalTags: [],
  createdAt: `${date}T12:00:00Z`,
});

describe("CephTimelapsePage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.get).mockImplementation(async (url: string) => {
      if (url.startsWith("/api/ceph")) return { data: [analysis("a2", "2026-03-01"), analysis("a1", "2026-01-01")] } as never;
      return { data: [] } as never;
    });
  });

  it("plays only the ordered persisted records", async () => {
    render(<CephTimelapsePage />);

    expect(await screen.findByRole("heading", { name: "Timelapse السجلات السريرية" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /سيفالو جانبي/ })).toHaveAttribute("src", "/uploads/a1.jpg");

    await userEvent.click(screen.getByRole("button", { name: "السجل التالي" }));
    await waitFor(() => expect(screen.getByRole("img", { name: /سيفالو جانبي/ })).toHaveAttribute("src", "/uploads/a2.jpg"));
    expect(screen.getByText(/سجل 2 من 2/)).toBeInTheDocument();
  }, 15_000);
});
