import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Normalize a Yemeni phone number to the canonical format "967XXXXXXXXX".
 *
 * Handles these input formats:
 *  - 770245745         → 967770245745
 *  - 0770245745        → 967770245745
 *  - +967770245745     → 967770245745
 *  - 00967770245745    → 967770245745
 *  - ٧٧٠٢٤٥٧٤٥ (Arabic digits) → 967770245745
 */
export function normalizePhone(phone: string): string {
  if (!phone) return "";
  let result = phone
    // Convert Arabic/Indic digits (٠-٩, ۰-۹) to English
    .replace(/[٠-٩۰-۹]/g, (c) => {
      const map: Record<string, string> = {
        '٠': '0', '۰': '0', '١': '1', '۱': '1', '٢': '2', '۲': '2',
        '٣': '3', '۳': '3', '٤': '4', '۴': '4', '٥': '5', '۵': '5',
        '٦': '6', '۶': '6', '٧': '7', '۷': '7', '٨': '8', '۸': '8',
        '٩': '9', '۹': '9',
      };
      return map[c] ?? c;
    })
    // Remove spaces, dashes, parentheses, dots
    .replace(/[\s\-.()]/g, "");

  if (result.startsWith("+")) result = result.slice(1);
  if (result.startsWith("00")) result = result.slice(2);
  if (result.startsWith("0") && result.length >= 9) result = "967" + result.slice(1);
  else if (result.startsWith("7") && result.length === 9) result = "967" + result;

  return result;
}

/**
 * Format a phone number for WhatsApp link.
 * Returns the phone in international format without the + sign (e.g., "967770245745").
 */
export function formatPhoneForWhatsApp(phone: string | null | undefined): string {
  if (!phone) return "";
  return normalizePhone(phone);
}

export function formatYemeniRiyal(amount: number): string {
  return new Intl.NumberFormat("ar-YE", {
    style: "currency",
    currency: "YER",
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  }).format(amount);
}

export function formatArabicDate(dateStr: string): string {
  return new Intl.DateTimeFormat("ar-YE", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(new Date(dateStr));
}

export function formatTime(timeStr: string): string {
  const [h, m] = timeStr.split(":");
  const hour = parseInt(h);
  const period = hour >= 12 ? "م" : "ص";
  const h12 = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
  return `${h12}:${m} ${period}`;
}

export const APPOINTMENT_STATUS_LABELS: Record<string, string> = {
  Scheduled: "مجدول",
  Confirmed: "مؤكد",
  Arrived: "وصل",
  Waiting: "في الانتظار",
  Called: "تم النداء",
  InRoom: "داخل الغرفة",
  InProgress: "جاري العلاج",
  Completed: "مكتمل",
  Cancelled: "ملغي",
  NoShow: "لم يحضر",
};

export const GENDER_LABELS: Record<string, string> = {
  Male: "ذكر",
  Female: "أنثى",
};
