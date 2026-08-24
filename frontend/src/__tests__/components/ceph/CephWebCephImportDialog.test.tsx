import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CephWebCephImportDialog } from "@/components/ceph/CephWebCephImportDialog";
import api from "@/lib/api";
import type { CephAnalysis } from "@/types/ceph";

vi.mock("@/lib/api", () => ({
  default: { post: vi.fn() },
}));

const analysis = {
  id: "analysis-1",
  landmarks: [],
  measurements: [],
  pixelsPerMm: 2,
  isApproved: false,
} as unknown as CephAnalysis;

describe("CephWebCephImportDialog", () => {
  beforeEach(() => vi.clearAllMocks());

  it("blocks import until calibration and S are available", () => {
    render(
      <CephWebCephImportDialog
        analysisId="analysis-1"
        pixelsPerMm={null}
        imageWidth={1000}
        imageHeight={800}
        onImported={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "استيراد WebCeph" }));

    expect(screen.getByText(/احفظ معايرة الصورة وحدد نقطة S/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "استيراد النقاط" })).toBeDisabled();
  });

  it("uploads the account-owned export with calibration and S anchor", async () => {
    const onImported = vi.fn();
    vi.mocked(api.post).mockResolvedValue({
      data: {
        analysis,
        summary: {
          parsed: 58,
          imported: 58,
          replaced: 24,
          skippedExisting: 0,
          skippedOutOfBounds: 0,
          recordingDate: "2026-07-13",
          skippedNames: [],
        },
      },
    });

    render(
      <CephWebCephImportDialog
        analysisId="analysis-1"
        pixelsPerMm={2}
        imageWidth={1000}
        imageHeight={800}
        sLandmark={{
          key: "S",
          name: "Sella",
          nameAr: "السرج",
          x: 100,
          y: 80,
          isAiPlaced: false,
        }}
        onImported={onImported}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "استيراد WebCeph" }));
    const input = screen.getByLabelText("ملف Landmark Table");
    const file = new File(["<tr></tr>"], "landmark_table.xls", { type: "application/vnd.ms-excel" });
    fireEvent.change(input, { target: { files: [file] } });
    fireEvent.click(screen.getByRole("button", { name: "استيراد النقاط" }));

    await waitFor(() => expect(api.post).toHaveBeenCalledOnce());
    const [url, body] = vi.mocked(api.post).mock.calls[0];
    expect(url).toBe("/api/ceph/analysis-1/imports/webceph-landmarks");
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get("pixelsPerMm")).toBe("2");
    expect((body as FormData).get("anchorX")).toBe("100");
    expect((body as FormData).get("anchorY")).toBe("80");
    expect(onImported).toHaveBeenCalledWith(analysis, expect.objectContaining({ imported: 58 }));
    expect(await screen.findByText("تم استيراد 58 من 58 نقطة")).toBeInTheDocument();
  });
});
