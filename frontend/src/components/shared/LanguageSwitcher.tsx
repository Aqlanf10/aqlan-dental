"use client";

import { Languages } from "lucide-react";
import { useLocale } from "@/i18n/LocaleProvider";

/**
 * CORE-REQ-006 — switches the interface between Arabic (RTL) and English (LTR).
 *
 * Deliberately a single toggle rather than a dropdown: there are two languages, and a control
 * that takes one click is the difference between a feature staff use and one they do not.
 *
 * The label always shows the language you would switch *to*, in that language's own script, so
 * it reads correctly whichever direction the page is currently in.
 */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { locale, setLocale } = useLocale();

  const next = locale === "ar" ? "en" : "ar";
  const label = next === "en" ? "English" : "العربية";

  return (
    <button
      type="button"
      onClick={() => setLocale(next)}
      aria-label={next === "en" ? "Switch to English" : "التبديل إلى العربية"}
      title={next === "en" ? "Switch to English" : "التبديل إلى العربية"}
      className={
        className ??
        "flex items-center gap-1.5 rounded-lg px-2.5 py-1.5 text-xs font-semibold text-gray-600 hover:bg-gray-100 hover:text-gray-900"
      }
    >
      <Languages className="h-4 w-4" aria-hidden />
      <span dir={next === "en" ? "ltr" : "rtl"}>{label}</span>
    </button>
  );
}
