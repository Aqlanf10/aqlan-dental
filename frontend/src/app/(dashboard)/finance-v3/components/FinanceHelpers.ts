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

export function safeFormatDate(dateStr: string | null | undefined, locale = "ar-SA"): string {
  if (!dateStr) return "—";
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleDateString(locale);
  } catch {
    return "—";
  }
}

export function safeFormatDateTime(dateStr: string | null | undefined, locale = "ar-SA"): string {
  if (!dateStr) return "—";
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return "—";
    return d.toLocaleString(locale);
  } catch {
    return "—";
  }
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
