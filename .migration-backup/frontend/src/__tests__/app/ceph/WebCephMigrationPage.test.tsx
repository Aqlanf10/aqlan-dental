import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import WebCephMigrationPage from "@/app/(dashboard)/ceph/webceph-migration/page";
import api from "@/lib/api";

vi.mock("@/lib/api", () => ({ default: { get: vi.fn() } }));

describe("WebCephMigrationPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows the official connector gate and export-only clinical path", async () => {
    vi.mocked(api.get).mockResolvedValue({
      data: {
        configured: false,
        partnerAgreementRequired: true,
        premiumRequired: true,
        requestLimitPerMinute: 30,
        apiTransferable: ["patients", "records", "images"],
        exportRequired: ["landmarks", "analysis-results", "clinical-diagnostic-data"],
        notice: "يلزم اتفاق Partner API ومفتاح خادمي من WebCeph.",
      },
    });

    render(<WebCephMigrationPage />);

    expect(await screen.findByRole("heading", { name: "Partner API غير مهيأ بعد" })).toBeInTheDocument();
    expect(screen.getByText(/استيراد Landmark Table متاح الآن/)).toBeInTheDocument();
    expect(screen.getByText(/30 طلبًا في الدقيقة/)).toBeInTheDocument();
  });
});
