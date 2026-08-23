"use client";

import { useEffect, useState } from "react";
import api from "@/lib/api";

// ─── Fallback values ─────────────────────────────────────────────────────────

const FALLBACKS = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  phone: "04-253028",
  whatsApp: "967770245745",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  footer: "تم إنشاء هذا التقرير بواسطة نظام مركز الدكتور عقلان الكامل",
  logoUrl: "",
  // Spec 010 (RX-REQ-004): English identity for printed forms that leave the
  // clinic (radiology referrals, prescriptions) — served by the same endpoint.
  clinicNameEn: "Dr. Aqlan Alkamel Center for Orthodontics, Dental Implants & Cosmetic Dentistry",
  addressEn: "Upper Al-Tahrir Street, Taiz, Yemen",
  leadDoctorEn: "Dr. Aqlan Alkamel — Orthodontic Specialist",
  leadDoctorCredentialsEn: "Central University of Manila — Philippines",
  // CORE-REQ-006: print language for the forms a patient carries outside the clinic.
  // "en" preserves the behaviour those forms have always had (Spec 010, RX-REQ-004).
  printLanguage: "en",
  leadDoctorAr: "د. عقلان الكامل — أخصائي تقويم الأسنان",
  leadDoctorCredentialsAr: "جامعة مانيلا المركزية — الفلبين",
} as const;

// ─── Types ────────────────────────────────────────────────────────────────────

export interface ClinicBranding {
  clinicName: string;
  phone: string;
  whatsApp: string;
  address: string;
  workingHours: string;
  footer: string;
  logoUrl: string;  // resolved absolute URL or ""
  clinicNameEn: string;
  addressEn: string;
  leadDoctorEn: string;
  leadDoctorCredentialsEn: string;
  /** "ar" | "en" — chosen independently of the interface language. */
  printLanguage: string;
  leadDoctorAr: string;
  leadDoctorCredentialsAr: string;
}

/**
 * CORE-REQ-006 — the clinic identity a printed form should carry, in the language the clinic
 * selected.
 *
 * Prescriptions and radiology referrals used to read the English fields directly, so they were
 * permanently English with no way to change that. This returns the Arabic or English block
 * according to `printLanguage`, defaulting to English so nothing changes for a clinic that has
 * not chosen.
 */
/**
 * Identity for the *interface*, chosen by the language the user is reading.
 *
 * Deliberately separate from `printIdentity`: CORE-REQ-006 makes the print language an
 * independent setting, so a clinic can run the screens in English and still print Arabic
 * forms. Reusing `printIdentity` here would silently tie the two together again.
 */
export function interfaceIdentity(branding: ClinicBranding, locale: string) {
  const arabic = locale !== "en";
  return {
    clinicName: arabic ? branding.clinicName : branding.clinicNameEn,
    clinicAddress: arabic ? branding.address : branding.addressEn,
  };
}

export function printIdentity(branding: ClinicBranding) {
  const arabic = branding.printLanguage === "ar";
  return {
    clinicName: arabic ? branding.clinicName : branding.clinicNameEn,
    clinicAddress: arabic ? branding.address : branding.addressEn,
    leadDoctor: arabic ? branding.leadDoctorAr : branding.leadDoctorEn,
    leadDoctorCredentials: arabic ? branding.leadDoctorCredentialsAr : branding.leadDoctorCredentialsEn,
    isArabic: arabic,
  };
}

// ─── URL resolver ─────────────────────────────────────────────────────────────

/**
 * Resolve a relative upload URL (e.g. "/uploads/ceph/x.png") to a value the
 * browser can render in an `<img src=...>`.
 *
 * NAV-CEPH-FIX (Part 2): Returns the relative path as-is so the request stays
 * same-origin. The Next.js rewrite in `next.config.mjs` proxies `/uploads/*` to
 * the backend, which means the browser's `aqlan_access_token` cookie
 * (SameSite=Strict) travels with the request and the backend's /uploads auth
 * middleware (SEC-03) accepts it. Previously this prefixed `${NEXT_PUBLIC_API_URL}`
 * (the cross-origin backend base) → the cookie was not sent → 401 → broken ceph
 * X-rays and clinical photos in production (Vercel → Railway).
 *
 * Absolute URLs (http://, https://) are returned unchanged — they're already
 * fully qualified (e.g. R2 / S3 object URLs).
 */
export function resolveImageUrl(url: string | null | undefined): string {
  if (!url || url.trim() === "") return "";
  if (url.startsWith("http://") || url.startsWith("https://")) return url;
  return url;  // relative — Next.js rewrite proxies /uploads/* (same-origin → cookie travels)
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

export function useClinicBranding(): ClinicBranding {
  const [branding, setBranding] = useState<ClinicBranding>({ ...FALLBACKS, logoUrl: "" });

  useEffect(() => {
    api.get<Record<string, string>>("/api/public/website-settings")
      .then((res) => {
        const s = res.data;
        setBranding({
          clinicName: s.clinicName || FALLBACKS.clinicName,
          phone: s.phone || FALLBACKS.phone,
          whatsApp: s.whatsapp || FALLBACKS.whatsApp,
          address: s.address || FALLBACKS.address,
          workingHours: s.workingHours || FALLBACKS.workingHours,
          footer: FALLBACKS.footer,
          logoUrl: resolveImageUrl(s.logoUrl),
          clinicNameEn: s.clinicNameEn || FALLBACKS.clinicNameEn,
          addressEn: s.addressEn || FALLBACKS.addressEn,
          printLanguage: s.printLanguage === "ar" ? "ar" : "en",
          leadDoctorAr: s.leadDoctorAr || FALLBACKS.leadDoctorAr,
          leadDoctorCredentialsAr: s.leadDoctorCredentialsAr || FALLBACKS.leadDoctorCredentialsAr,
          leadDoctorEn: s.leadDoctorEn || FALLBACKS.leadDoctorEn,
          leadDoctorCredentialsEn: s.leadDoctorCredentialsEn || FALLBACKS.leadDoctorCredentialsEn,
        });
      })
      .catch(() => {
        // Use fallbacks on error
        setBranding({ ...FALLBACKS, logoUrl: "" });
      });
  }, []);

  return branding;
}
