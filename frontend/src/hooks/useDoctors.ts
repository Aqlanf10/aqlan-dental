import { useQuery } from "@tanstack/react-query";
import api from "@/lib/api";

interface DoctorDto {
  id: string;
  name: string;
  specialty: string;
  licenseNumber?: string;
  color?: string;
  avatarInitials?: string;
  branchId?: string;
  branchName?: string;
  isActive: boolean;
}

/** Hook: Fetch all active doctors */
export function useDoctors() {
  return useQuery({
    queryKey: ["doctors"],
    queryFn: async () => {
      const { data } = await api.get<DoctorDto[]>("/api/doctors");
      return data;
    },
    staleTime: 60_000,
  });
}
