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

export function formatYER(amount: number | null | undefined): string {
  return (amount ?? 0).toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 }) + " ر.ي";
}

export function formatNumber(amount: number | null | undefined): string {
  return (amount ?? 0).toLocaleString("ar-YE", { minimumFractionDigits: 0, maximumFractionDigits: 0 });
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

export { extractErrorMessage } from "@/lib/errors";
