import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";
import type {
  CenterSummary,
  DoctorPerformance,
  FinancialReport,
  OrthoReport,
  GeneralDentistryReport,
  SurgeryReport,
  GrowthAnalysis,
  OverdueReport,
} from "@/types/finance";

/** Hook: Center summary report */
export function useCenterSummary(from: string, to: string) {
  return useQuery({
    queryKey: ["center-summary", from, to],
    queryFn: async () => {
      const { data } = await api.get<CenterSummary>(
        `/api/reports/center-summary?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Doctor performance report */
export function useDoctorPerformance(from: string, to: string) {
  return useQuery({
    queryKey: ["doctor-performance", from, to],
    queryFn: async () => {
      const { data } = await api.get<DoctorPerformance[]>(
        `/api/reports/doctor-performance?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Financial report */
export function useFinancialReport(from: string, to: string) {
  return useQuery({
    queryKey: ["financial-report", from, to],
    queryFn: async () => {
      const { data } = await api.get<FinancialReport>(
        `/api/reports/financial?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Ortho (orthodontic) report */
export function useOrthoReport(from: string, to: string) {
  return useQuery({
    queryKey: ["ortho-report", from, to],
    queryFn: async () => {
      const { data } = await api.get<OrthoReport>(
        `/api/reports/ortho-cases?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: General dentistry report */
export function useGeneralReport(from: string, to: string) {
  return useQuery({
    queryKey: ["general-report", from, to],
    queryFn: async () => {
      const { data } = await api.get<GeneralDentistryReport>(
        `/api/reports/general-dentistry?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Surgery report */
export function useSurgeryReport(from: string, to: string) {
  return useQuery({
    queryKey: ["surgery-report", from, to],
    queryFn: async () => {
      const { data } = await api.get<SurgeryReport>(
        `/api/reports/surgery?from=${from}&to=${to}`
      );
      return data;
    },
    enabled: !!from && !!to,
    staleTime: 60_000,
  });
}

/** Hook: Overdue report */
export function useOverdueReport() {
  return useQuery({
    queryKey: ["overdue-report"],
    queryFn: async () => {
      const { data } = await api.get<OverdueReport>("/api/reports/overdue");
      return data;
    },
    staleTime: 120_000,
  });
}

/** Hook: Growth analysis — 12-month patient growth */
export function useGrowthAnalysis() {
  return useQuery({
    queryKey: ["growth-analysis"],
    queryFn: async () => {
      const { data } = await api.get<GrowthAnalysis>("/api/reports/growth-analysis");
      return data;
    },
    staleTime: 300_000,
  });
}
