import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import CephDeidentificationCanvas from "@/components/ceph/CephDeidentificationCanvas";

describe("CephDeidentificationCanvas", () => {
  const context = {
    clearRect: vi.fn(),
    drawImage: vi.fn(),
    fillRect: vi.fn(),
    strokeRect: vi.fn(),
    setLineDash: vi.fn(),
    fillStyle: "",
    strokeStyle: "",
    lineWidth: 1,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    let objectUrlSequence = 0;
    Object.defineProperty(URL, "createObjectURL", {
      configurable: true,
      value: vi.fn(() => `blob:pilot-${++objectUrlSequence}`),
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      configurable: true,
      value: vi.fn(),
    });
    vi.stubGlobal("Image", class {
      decoding = "";
      src = "";
      naturalWidth = 640;
      naturalHeight = 480;
      decode = vi.fn().mockResolvedValue(undefined);
    });
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(context as never);
    vi.spyOn(HTMLCanvasElement.prototype, "getBoundingClientRect").mockReturnValue({
      left: 0,
      top: 0,
      right: 640,
      bottom: 480,
      width: 640,
      height: 480,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    HTMLCanvasElement.prototype.setPointerCapture = vi.fn();
    HTMLCanvasElement.prototype.toBlob = vi.fn(callback => callback(new Blob(["sanitized"], { type: "image/png" })));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("draws a redaction and returns only the generated PNG preview", async () => {
    const onPrepared = vi.fn();
    render(<CephDeidentificationCanvas resetKey={0} onPrepared={onPrepared} />);

    const input = screen.getByLabelText("اختيار التصدير الرسمي");
    fireEvent.change(input, {
      target: { files: [new File(["synthetic"], "synthetic.png", { type: "image/png" })] },
    });

    const canvas = await screen.findByLabelText("مساحة تحديد مناطق إزالة الهوية");
    fireEvent.pointerDown(canvas, { button: 0, pointerId: 1, clientX: 20, clientY: 20 });
    fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 220, clientY: 90 });
    fireEvent.pointerUp(canvas, { pointerId: 1, clientX: 220, clientY: 90 });
    fireEvent.click(screen.getByRole("button", { name: "إنشاء المعاينة النهائية" }));

    await waitFor(() => expect(onPrepared).toHaveBeenLastCalledWith(expect.objectContaining({
      width: 640,
      height: 480,
      redactionCount: 1,
    })));
    expect(context.fillRect).toHaveBeenCalledWith(20, 20, 200, 70);
    expect(HTMLCanvasElement.prototype.toBlob).toHaveBeenCalled();
  });
});
