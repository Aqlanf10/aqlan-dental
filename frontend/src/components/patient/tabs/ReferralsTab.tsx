"use client";

import { useEffect, useState } from "react";
import { ArrowRightLeft } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface ReferralDto {
  id: string;
  referralDate: string;
  referringDoctor?: string;
  referredTo?: string;
  reason?: string;
  status?: string;
  notes?: string;
}

const REFERRAL_STATUS_LABELS: Record<string, string> = {
  pending: "معلّقة",
  completed: "مكتملة",
  cancelled: "ملغاة",
};

interface ReferralsTabProps {
  patientId: string;
}

export function ReferralsTab({ patientId }: ReferralsTabProps) {
  const [referrals, setReferrals] = useState<ReferralDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<ReferralDto[]>(`/api/referrals?patientId=${patientId}`)
      .then((r) => setReferrals(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-gray-100 rounded-lg" />
        ))}
      </div>
    );
  }

  if (referrals.length === 0) {
    return (
      <div className="text-center py-12 text-gray-400" dir="rtl">
        <ArrowRightLeft className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا توجد إحالات</p>
      </div>
    );
  }

  return (
    <div className="space-y-2" dir="rtl">
      {referrals.map((ref) => (
        <div key={ref.id} className="p-3 bg-white border border-gray-100 rounded-lg hover:border-gray-200 transition">
          <div className="flex items-center justify-between gap-2 flex-wrap">
            <div className="flex items-center gap-2">
              <ArrowRightLeft className="w-4 h-4 text-orange-500 flex-shrink-0" />
              <span className="text-sm font-medium text-gray-900">{formatArabicDate(ref.referralDate)}</span>
            </div>
            {ref.status && (
              <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                ref.status === "completed" ? "bg-green-50 text-green-700" :
                ref.status === "pending" ? "bg-yellow-50 text-yellow-700" :
                "bg-gray-100 text-gray-500"
              )}>
                {REFERRAL_STATUS_LABELS[ref.status] ?? ref.status}
              </span>
            )}
          </div>
          {ref.referringDoctor && <p className="text-xs text-gray-500 mt-1">من: {ref.referringDoctor}</p>}
          {ref.referredTo && <p className="text-xs text-gray-500">إلى: {ref.referredTo}</p>}
          {ref.reason && <p className="text-xs text-gray-600 mt-1">{ref.reason}</p>}
          {ref.notes && <p className="text-xs text-gray-400 mt-1 line-clamp-2">{ref.notes}</p>}
        </div>
      ))}
    </div>
  );
}
