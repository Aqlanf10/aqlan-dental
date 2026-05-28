/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 — Helper Functions
   Zero-State Resiliency: All formatters handle null/undefined gracefully.
   ═══════════════════════════════════════════════════════════════════════════════ */

export function todayArabic(): string {
  return new Date().toLocaleDateString("ar-SA", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

/** Format amount as YER currency — never crashes on null/undefined */
export function formatYER(amount: number | null | undefined): string {
  const value = amount ?? 0;
  try {
    return new Intl.NumberFormat("ar-YE", {
      style: "currency",
      currency: "YER",
      maximumFractionDigits: 0,
    }).format(value);
  } catch {
    return value.toLocaleString("ar-YE") + " ر.ي";
  }
}

/** Format number without currency symbol — never crashes on null/undefined */
export function formatNumber(amount: number | null | undefined): string {
  return (amount ?? 0).toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 });
}

/** Safe date formatter with Arabic locale — returns "—" for null/undefined/invalid */
export function safeFormatDate(dateStr: string | null | undefined): string {
  if (!dateStr) return "—";
  try {
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return "—";
    return new Intl.DateTimeFormat("ar-YE", {
      year: "numeric",
      month: "long",
      day: "numeric",
    }).format(d);
  } catch {
    return "—";
  }
}

/** Safe datetime formatter with Arabic locale — returns "—" for null/undefined/invalid */
export function safeFormatDateTime(dateTimeStr: string | null | undefined): string {
  if (!dateTimeStr) return "—";
  try {
    const d = new Date(dateTimeStr);
    if (isNaN(d.getTime())) return "—";
    return new Intl.DateTimeFormat("ar-YE", {
      year: "numeric",
      month: "long",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      hour12: true,
    }).format(d);
  } catch {
    return "—";
  }
}

/** Safe currency formatter with full Intl support — never crashes */
export function safeFormatCurrency(amount: number | null | undefined): string {
  const value = amount ?? 0;
  try {
    return new Intl.NumberFormat("ar-YE", {
      style: "currency",
      currency: "YER",
      maximumFractionDigits: 2,
    }).format(value);
  } catch {
    return value.toFixed(2) + " ر.ي";
  }
}

/** Safely extract error message from Axios error objects */
export function extractErrorMessage(err: unknown, fallback = "حدث خطأ"): string {
  if (err && typeof err === "object" && "response" in err) {
    const resp = (err as { response?: { data?: { message?: string; title?: string; errors?: Record<string, string[]> }; status?: number } }).response;
    if (resp?.data?.message) return resp.data.message;
    if (resp?.data?.title) return resp.data.title;
    if (resp?.data?.errors) {
      const firstError = Object.values(resp.data.errors).find(e => e && e.length > 0);
      if (firstError && firstError.length > 0) return firstError[0];
    }
    if (resp?.status === 401) return "ليس لديك صلاحية. يرجى تسجيل الدخول مجدداً.";
    if (resp?.status === 403) return "غير مصرح بهذا الإجراء.";
  }
  if (err instanceof Error && err.message && !err.message.startsWith("Request failed")) return err.message;
  return fallback;
}

/** Safe array guard — always returns an array, never null/undefined */
export function safeArray<T>(data: T[] | null | undefined): T[] {
  return Array.isArray(data) ? data : [];
}

/** Safe object property accessor — returns fallback for null/undefined nested props */
export function safeGet<T>(obj: unknown, path: string, fallback: T): T {
  if (!obj || typeof obj !== "object") return fallback;
  const keys = path.split(".");
  let current: unknown = obj;
  for (const key of keys) {
    if (current == null || typeof current !== "object") return fallback;
    current = (current as Record<string, unknown>)[key];
  }
  return (current as T) ?? fallback;
}
