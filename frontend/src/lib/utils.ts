import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
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
  InProgress: "جارٍ",
  Completed: "مكتمل",
  Cancelled: "ملغى",
  NoShow: "لم يحضر",
};

export const GENDER_LABELS: Record<string, string> = {
  Male: "ذكر",
  Female: "أنثى",
};
