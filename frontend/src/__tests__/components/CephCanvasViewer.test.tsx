import { fireEvent, render, screen } from "@testing-library/react";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { CephCanvas } from "@/components/ceph/CephCanvas";

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    disconnect() {}
  }
  vi.stubGlobal("ResizeObserver", ResizeObserverMock);

  const context = new Proxy(
    { measureText: (text: string) => ({ width: text.length * 7 }) },
    {
      get(target, property) {
        if (property in target) return target[property as keyof typeof target];
        return () => undefined;
      },
    },
  );
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(
    context as unknown as CanvasRenderingContext2D,
  );
});

function renderCanvas(onResetImageAdjustments = vi.fn()) {
  const onLandmarksChange = vi.fn();
  render(
    <div style={{ width: 900, height: 700 }}>
      <CephCanvas
        imageUrl={null}
        imageWidth={800}
        imageHeight={600}
        landmarks={[]}
        onLandmarksChange={onLandmarksChange}
        selectedKey={null}
        onSelectKey={vi.fn()}
        showPlanes={false}
        showSimulation={false}
        simulationScenario="extraction_retraction"
        pixelsPerMm={5}
        onResetImageAdjustments={onResetImageAdjustments}
      />
    </div>,
  );
  return { onLandmarksChange, onResetImageAdjustments };
}

describe("CephCanvas viewer-only controls", () => {
  it("marks distance and angle tools as transient without changing landmarks", () => {
    const { onLandmarksChange } = renderCanvas();
    const distance = screen.getByRole("button", { name: "قياس مسافة مؤقتة" });
    const angle = screen.getByRole("button", { name: "قياس زاوية مؤقتة" });

    fireEvent.click(distance);
    expect(distance).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByText("عرض مؤقت · غير محفوظ")).toBeInTheDocument();

    fireEvent.click(angle);
    expect(angle).toHaveAttribute("aria-pressed", "true");
    expect(distance).toHaveAttribute("aria-pressed", "false");
    expect(onLandmarksChange).not.toHaveBeenCalled();
  });

  it("resets preview transforms and page-owned image adjustments", () => {
    const onResetImageAdjustments = vi.fn();
    const { onLandmarksChange } = renderCanvas(onResetImageAdjustments);
    const flip = screen.getByRole("button", { name: "قلب العرض أفقياً" });

    fireEvent.click(flip);
    expect(flip).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(screen.getByRole("button", { name: "إعادة ضبط العارض بالكامل" }));

    expect(flip).toHaveAttribute("aria-pressed", "false");
    expect(onResetImageAdjustments).toHaveBeenCalledOnce();
    expect(onLandmarksChange).not.toHaveBeenCalled();
  });
});
