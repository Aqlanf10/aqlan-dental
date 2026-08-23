import { apiRequest } from "@/lib/api";
import React, { createContext, type PropsWithChildren, useContext, useEffect, useMemo, useState } from "react";

export type ClinicBranding = {
  clinicName: string;
  address: string;
  phone: string;
  workingHours: string;
  leadDoctor: string;
  credentials: string;
};

export const clinicBrandFallback: ClinicBranding = {
  clinicName: "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
  address: "تعز، اليمن — شارع التحرير الأعلى",
  phone: "04-253028 · 770-245745 · 711-752823",
  workingHours: "السبت – الخميس: 8 ص – 8 م",
  leadDoctor: "د. عقلان الكامل — أخصائي تقويم الأسنان",
  credentials: "جامعة مانيلا المركزية — الفلبين"
};

type PublicBrandResponse = {
  clinicName?: string;
  address?: string;
  phone?: string;
  workingHours?: string;
  leadDoctorAr?: string;
  leadDoctorCredentialsAr?: string;
};

function brandText(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function normalizePublicBrand(value: unknown): PublicBrandResponse | null {
  if (!value || typeof value !== "object") return null;
  const source = value as Record<string, unknown>;
  return {
    clinicName: brandText(source.clinicName ?? source.ClinicName),
    address: brandText(source.address ?? source.Address),
    phone: brandText(source.phone ?? source.Phone),
    workingHours: brandText(source.workingHours ?? source.WorkingHours),
    leadDoctorAr: brandText(source.leadDoctorAr ?? source.LeadDoctorAr),
    leadDoctorCredentialsAr: brandText(source.leadDoctorCredentialsAr ?? source.LeadDoctorCredentialsAr)
  };
}

const BrandContext = createContext<ClinicBranding>(clinicBrandFallback);

export function BrandProvider({ children }: PropsWithChildren) {
  const [remote, setRemote] = useState<PublicBrandResponse | null>(null);

  useEffect(() => {
    let active = true;
    apiRequest<unknown>("/api/public/website-settings")
      .then((value) => { if (active) setRemote(normalizePublicBrand(value)); })
      .catch(() => { /* The official local identity remains available offline. */ });
    return () => { active = false; };
  }, []);

  const value = useMemo<ClinicBranding>(() => ({
    clinicName: remote?.clinicName || clinicBrandFallback.clinicName,
    address: remote?.address || clinicBrandFallback.address,
    phone: remote?.phone || clinicBrandFallback.phone,
    workingHours: remote?.workingHours || clinicBrandFallback.workingHours,
    leadDoctor: remote?.leadDoctorAr || clinicBrandFallback.leadDoctor,
    credentials: remote?.leadDoctorCredentialsAr || clinicBrandFallback.credentials
  }), [remote]);

  return <BrandContext.Provider value={value}>{children}</BrandContext.Provider>;
}

export function useClinicBranding(): ClinicBranding {
  return useContext(BrandContext);
}
