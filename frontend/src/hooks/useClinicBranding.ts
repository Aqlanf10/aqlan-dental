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
}

// ─── URL resolver ─────────────────────────────────────────────────────────────

export function resolveImageUrl(url: string | null | undefined): string {
  if (!url || url.trim() === "") return "";
  if (url.startsWith("http://") || url.startsWith("https://")) return url;
  const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "";
  return `${apiBase}${url}`;
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
        });
      })
      .catch(() => {
        // Use fallbacks on error
        setBranding({ ...FALLBACKS, logoUrl: "" });
      });
  }, []);

  return branding;
}
