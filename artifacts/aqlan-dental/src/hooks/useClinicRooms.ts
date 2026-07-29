import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";

export interface ClinicRoomOption {
  id: string;
  arabicName: string;
}

/**
 * Shared active-rooms hook (doctor room assignments — "تعيينات غرف الأطباء").
 * daily-operations has its own route-local useRooms wrapper in _lib/hooks.ts;
 * this is the app-wide equivalent for pages outside that route (e.g. /doctors)
 * without cross-route _lib imports. Same endpoint, same shape.
 */
export function useClinicRooms() {
  return useQuery<ClinicRoomOption[]>({
    queryKey: ["clinic-rooms", "active"],
    queryFn: async () => {
      const { data } = await api.get<{ id: string; arabicName: string }[]>("/api/settings/rooms/active");
      return data.map((r) => ({ id: r.id, arabicName: r.arabicName }));
    },
    staleTime: 60_000,
  });
}
