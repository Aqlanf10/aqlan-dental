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

const BrandContext = createContext<ClinicBranding>(clinicBrandFallback);

export function BrandProvider({ children }: PropsWithChildren) {
  const [remote, setRemote] = useState<PublicBrandResponse | null>(null);

  useEffect(() => {
    let active = true;
    apiRequest<PublicBrandResponse>("/api/public/website-settings")
      .then((value) => { if (active) setRemote(value); })
      .catch(() => { /* The official local identity remains available offline. */ });
    return () => { active = false; };
  }, []);

  const value = useMemo<ClinicBranding>(() => ({
    clinicName: remote?.clinicName?.trim() || clinicBrandFallback.clinicName,
    address: remote?.address?.trim() || clinicBrandFallback.address,
    phone: remote?.phone?.trim() || clinicBrandFallback.phone,
    workingHours: remote?.workingHours?.trim() || clinicBrandFallback.workingHours,
    leadDoctor: remote?.leadDoctorAr?.trim() || clinicBrandFallback.leadDoctor,
    credentials: remote?.leadDoctorCredentialsAr?.trim() || clinicBrandFallback.credentials
  }), [remote]);

  return <BrandContext.Provider value={value}>{children}</BrandContext.Provider>;
}

export function useClinicBranding(): ClinicBranding {
  return useContext(BrandContext);
}
