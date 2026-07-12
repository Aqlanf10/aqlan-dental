import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  getClinicQueueActionErrorFallback,
  notifyClinicQueueActionFailure,
} from "@/lib/clinicQueueActionErrors";
import { useToastStore } from "@/stores/toastStore";

function requestError(
  url: string,
  method = "post",
  extra: Record<string, unknown> = {},
) {
  return Object.assign(new Error("Network Error"), {
    config: { url, method },
    ...extra,
  });
}

describe("SEQ-36 clinic queue action errors", () => {
  const show = vi.fn();

  beforeEach(() => {
    show.mockReset();
    useToastStore.setState({
      toasts: [],
      show,
      dismiss: vi.fn(),
    });
  });

  it.each([
    ["/api/clinic-queue/reorder", "post", "تعذر تغيير ترتيب قائمة الانتظار"],
    ["/api/clinic-queue/q-1/call", "post", "تعذر نداء المريض"],
    ["/api/clinic-queue/q-1/recall", "post", "تعذر نداء المريض"],
    ["/api/clinic-queue/q-1/enter-room", "post", "تعذر إدخال المريض إلى الغرفة"],
    ["/api/clinic-queue/q-1/start", "post", "تعذر بدء زيارة المريض"],
    ["/api/clinic-queue/q-1/complete", "post", "تعذر إنهاء زيارة المريض"],
    ["/api/clinic-queue/q-1/cancel", "post", "تعذر إلغاء المريض من قائمة الانتظار"],
    ["/api/clinic-queue/q-1/no-show", "post", "تعذر تسجيل عدم حضور المريض"],
    ["/api/clinic-queue/q-1/priority", "patch", "تعذر تغيير أولوية المريض"],
    ["/api/clinic-queue/q-1/notify", "post", "تعذر إرسال إشعار المريض"],
  ])("maps %s to the correct Arabic fallback", (url, method, expected) => {
    expect(getClinicQueueActionErrorFallback(requestError(url, method))).toBe(expected);
  });

  it("shows the operation fallback for a network failure", () => {
    const handled = notifyClinicQueueActionFailure(
      requestError("/api/clinic-queue/q-1/call"),
    );

    expect(handled).toBe(true);
    expect(show).toHaveBeenCalledWith("error", "تعذر نداء المريض", undefined);
  });

  it("prefers the server message over the fallback", () => {
    const error = {
      config: { url: "/api/clinic-queue/q-1/enter-room", method: "post" },
      response: { status: 409, data: { message: "الغرفة مشغولة حاليًا" } },
    };

    expect(notifyClinicQueueActionFailure(error)).toBe(true);
    expect(show).toHaveBeenCalledWith("error", "الغرفة مشغولة حاليًا", undefined);
  });

  it.each([
    requestError("/api/clinic-queue/today", "get"),
    requestError("/api/patients", "post"),
    requestError("/api/clinic-queue/q-1/call", "post", { code: "ERR_CANCELED" }),
    requestError("/api/clinic-queue/q-1/call", "post", {
      response: { status: 401 },
    }),
  ])("ignores non-action, cancelled, and authentication failures", (error) => {
    expect(notifyClinicQueueActionFailure(error)).toBe(false);
  });
});
