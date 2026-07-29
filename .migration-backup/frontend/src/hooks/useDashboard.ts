import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";

interface DashboardStats {
  totalPatients: number;
  totalAppointmentsToday: number;
  activeOrthoCases: number;
  monthlyRevenue: number;
  pendingPayments: number;
  newPatientsThisMonth: number;
}

interface ChartData {
  labels: string[];
  appointmentsPerDay: number[];
  revenuePerDay: number[];
  patientsPerDay: number[];
}

/** Hook: Fetch dashboard stats */
export function useDashboardStats() {
  return useQuery({
    queryKey: ["dashboard", "stats"],
    queryFn: async () => {
      const { data } = await api.get<DashboardStats>("/api/dashboard/stats");
      return data;
    },
    staleTime: 30_000,
    refetchInterval: 120_000, // Refresh every 2 minutes
  });
}

/** Hook: Fetch dashboard chart data */
export function useDashboardCharts() {
  return useQuery({
    queryKey: ["dashboard", "charts"],
    queryFn: async () => {
      const { data } = await api.get<ChartData>("/api/dashboard/charts");
      return data;
    },
    staleTime: 60_000,
  });
}
