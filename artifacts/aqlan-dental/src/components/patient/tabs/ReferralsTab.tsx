
import { useEffect, useState } from "react";
import { ArrowRightLeft } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate } from "@/lib/utils";

interface ReferralDto {
  id: string;
  referralDate?: string;
  createdAt?: string;
  referringDoctor?: string;
  fromDoctorName?: string;
  referredTo?: string;
  toDoctorName?: string;
  reason?: string;
  status?: string;
  notes?: string;
  priority?: string;
}

interface ReferralsPagedResponse {
  data: ReferralDto[];
  total?: number;
  page?: number;
  pageSize?: number;
}

function parseReferralsResponse(body: ReferralsPagedResponse | ReferralDto[]): ReferralDto[] {
  if (Array.isArray(body)) return body;
  return body.data ?? [];
}

const REFERRAL_STATUS_LABELS: Record<string, string> = {
  pending: "معلّقة",
  accepted: "مقبولة",
  completed: "مكتملة",
  cancelled: "ملغاة",
};

const PRIORITY_LABELS: Record<string, { label: string; color: string }> = {
  urgent: { label: "عاجل", color: "bg-red-50 text-red-700" },
  normal: { label: "عادي", color: "bg-blue-50 text-blue-700" },
  low: { label: "منخفض", color: "bg-gray-50 text-gray-700" },
};

interface ReferralsTabProps {
  patientId: string;
}

export function ReferralsTab({ patientId }: ReferralsTabProps) {
  const [referrals, setReferrals] = useState<ReferralDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setFetchError(false);
    api
      .get<ReferralsPagedResponse | ReferralDto[]>(`/api/referrals?patientId=${patientId}&pageSize=50`)
      .then((r) => {
        if (cancelled) return;
        setReferrals(parseReferralsResponse(r.data));
      })
      .catch(() => {
        if (!cancelled) setFetchError(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [patientId, retryKey]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  if (fetchError) {
    return (
      <div className="p-4 text-center">
        <p className="text-sm text-red-600 mb-2">فشل في تحميل البيانات</p>
        <button onClick={() => setRetryKey((k) => k + 1)} className="text-xs text-blue-600 underline">إعادة المحاولة</button>
      </div>
    );
  }

  if (referrals.length === 0) {
    return (
      <EmptyState
        icon={ArrowRightLeft}
        title="لا توجد إحالات"
        description="لم يتم تسجيل أي إحالات لهذا المريض"
      />
    );
  }

  return (
    <div className="space-y-2">
      {referrals.map((ref) => {
        const dateStr = ref.referralDate ?? ref.createdAt ?? "";
        const fromDoctor = ref.referringDoctor ?? ref.fromDoctorName ?? "";
        const toDoctor = ref.referredTo ?? ref.toDoctorName ?? "";
        const priorityInfo = ref.priority ? PRIORITY_LABELS[ref.priority] : null;

        return (
          <div key={ref.id} className="p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-[#3d7ab5] hover:shadow-sm transition">
            <div className="flex items-center justify-between gap-2 flex-wrap">
              <div className="flex items-center gap-2">
                <ArrowRightLeft className="w-4 h-4 text-orange-500 flex-shrink-0" />
                <span className="text-sm font-medium text-[#0d2137]">{formatArabicDate(dateStr)}</span>
              </div>
              <div className="flex items-center gap-2">
                {priorityInfo && (
                  <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium", priorityInfo.color)}>
                    {priorityInfo.label}
                  </span>
                )}
                {ref.status && (
                  <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                    ref.status === "completed" ? "bg-green-50 text-green-700" :
                    ref.status === "accepted" ? "bg-blue-50 text-blue-700" :
                    ref.status === "pending" ? "bg-yellow-50 text-yellow-700" :
                    "bg-[#f1f5f9] text-[#64748b]"
                  )}>
                    {REFERRAL_STATUS_LABELS[ref.status] ?? ref.status}
                  </span>
                )}
              </div>
            </div>
            <div className="flex items-center gap-4 mt-1.5">
              {fromDoctor && <p className="text-xs text-[#64748b]">من: <span className="font-medium text-[#0d2137]">{fromDoctor}</span></p>}
              {toDoctor && <p className="text-xs text-[#64748b]">إلى: <span className="font-medium text-[#0d2137]">{toDoctor}</span></p>}
            </div>
            {ref.reason && <p className="text-xs text-[#64748b] mt-1">{ref.reason}</p>}
            {ref.notes && <p className="text-xs text-[#94a3b8] mt-1 line-clamp-2">{ref.notes}</p>}
          </div>
        );
      })}
    </div>
  );
}
