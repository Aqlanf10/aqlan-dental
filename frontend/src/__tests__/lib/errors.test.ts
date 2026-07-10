import { describe, it, expect } from "vitest";
import { extractErrorMessage } from "@/lib/errors";

const axiosErr = (data: unknown, status = 400) => ({
  isAxiosError: true,
  response: { status, data },
});

// FE-11: single source of truth for API error extraction. The priority order
// is message → detail → errors[] → title → status-based → fallback.
describe("extractErrorMessage", () => {
  it("prefers the server's message field", () => {
    expect(
      extractErrorMessage(axiosErr({ message: "رسالة الخادم", title: "Bad Request" })),
    ).toBe("رسالة الخادم");
  });

  it("prefers validation errors over the generic title (Codex P2 on #647)", () => {
    // A FluentValidation 400: generic English title, Arabic field messages in errors.
    const err = axiosErr({
      title: "One or more validation errors occurred.",
      errors: { AppointmentDate: ["تاريخ الموعد مطلوب"] },
    });
    expect(extractErrorMessage(err, "حدث خطأ أثناء الحفظ")).toBe("تاريخ الموعد مطلوب");
  });

  it("supports errors as a plain array", () => {
    expect(extractErrorMessage(axiosErr({ errors: ["الاسم مطلوب"] }))).toBe("الاسم مطلوب");
  });

  it("falls back to title only when nothing more specific exists", () => {
    expect(extractErrorMessage(axiosErr({ title: "Conflict" }, 409))).toBe("Conflict");
  });

  it("maps 401/403 to Arabic messages and unknown errors to the fallback", () => {
    expect(extractErrorMessage(axiosErr({}, 401))).toContain("تسجيل الدخول");
    expect(extractErrorMessage(axiosErr({}, 403))).toBe("غير مصرح بهذا الإجراء.");
    expect(extractErrorMessage({}, "بديل")).toBe("بديل");
  });
});
