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

export function formatMoney(amount: number | null | undefined, currency = "YER"): string {
  const suffixes: Record<string, string> = { YER: "ر.ي", SAR: "ر.س", USD: "USD" };
  const fractionDigits = currency === "YER" ? 0 : 2;
  return `${(amount ?? 0).toLocaleString("ar-YE", { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits })} ${suffixes[currency] ?? currency}`;
}

export function formatCurrencyAmounts(
  items: { currency: string; amount: number }[] | null | undefined,
): string {
  if (!items?.length) return formatMoney(0, "YER");
  return items
    .map((item) => formatMoney(item.amount, item.currency))
    .join(" • ");
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
