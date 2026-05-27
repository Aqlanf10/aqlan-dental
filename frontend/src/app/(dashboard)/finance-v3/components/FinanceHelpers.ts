/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 — Helper Functions
   ═══════════════════════════════════════════════════════════════════════════════ */

export function todayArabic(): string {
  return new Date().toLocaleDateString("ar-SA", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

export function formatYER(amount: number): string {
  return amount.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + " ر.ي";
}

export function formatNumber(amount: number): string {
  return amount.toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 });
}

export function extractErrorMessage(err: unknown, fallback = "حدث خطأ"): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string }; status?: number } }).response;
    if (resp?.data?.message) return resp.data.message;
    if (resp?.status === 401) return "ليس لديك صلاحية. يرجى تسجيل الدخول مجدداً.";
    if (resp?.status === 403) return "غير مصرح بهذا الإجراء.";
  }
  return fallback;
}
