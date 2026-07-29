
import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";

export interface CashierSessionInfo {
  id: string;
  sessionNumber: string;
  openedAt: string;
  cashierName: string;
  expectedClosingCash: number;
  expectedClosingCard: number;
  expectedClosingBank: number;
  hasActiveSession?: boolean;
}

/**
 * Hook to check if the current user has an active CashierSession.
 * Used by payment-creating components to enforce the business rule:
 * "يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل أي مدفوعات"
 *
 * Polls every 30s with 15s stale time — same cadence as the daily-ops hook.
 */
export function useActiveCashierSession() {
  return useQuery<CashierSessionInfo | null>({
    queryKey: ["active-cashier-session"],
    queryFn: async () => {
      try {
        const { data } = await api.get<{ hasActiveSession: boolean; id?: string } & CashierSessionInfo>(
          "/api/cashier-sessions/active"
        );
        if (!data?.hasActiveSession || !data.id) return null;
        return data;
      } catch {
        return null;
      }
    },
    staleTime: 15_000,
    refetchInterval: 30_000,
  });
}
