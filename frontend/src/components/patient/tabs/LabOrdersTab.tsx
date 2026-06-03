"use client";

import { useEffect, useState } from "react";
import { FlaskConical } from "lucide-react";
import api from "@/lib/api";
import { EmptyState } from "./EmptyState";
import { cn, formatArabicDate } from "@/lib/utils";

interface LabOrderDto {
  id: string;
  orderNumber?: string;
  patientId?: string;
  patientName?: string;
  patientNumber?: string;
  orthoCaseNumber?: string;
  applianceType?: string;
  labName?: string;
  sentDate?: string;
  expectedDate?: string;
  receivedDate?: string;
  deliveredDate?: string;
  status?: string;
  priority?: string;
  instructions?: string;
  cost?: number;
  doctorName?: string;
  shade?: string;
  restorationType?: string;
  visitId?: string;
  cancellationReason?: string;
  createdAt?: string;
}

const LAB_STATUS_LABELS: Record<string, string> = {
  draft: "مسودة",
  sent: "تم الإرسال",
  manufacturing: "قيد التصنيع",
  tryIn: "تجربة",
  ready: "جاهز",
  received: "تم الاستلام",
  delivered: "تم التسليم",
  returned: "مرتجع",
  remake: "إعادة صناعة",
  cancelled: "ملغي",
};

const LAB_STATUS_COLORS: Record<string, string> = {
  draft: "bg-gray-50 text-gray-600",
  sent: "bg-blue-50 text-blue-700",
  manufacturing: "bg-amber-50 text-amber-700",
  tryIn: "bg-teal-50 text-teal-700",
  ready: "bg-green-50 text-green-700",
  received: "bg-emerald-50 text-emerald-700",
  delivered: "bg-teal-50 text-teal-700",
  returned: "bg-orange-50 text-orange-700",
  remake: "bg-purple-50 text-purple-700",
  cancelled: "bg-red-50 text-red-700",
};

interface LabOrdersTabProps {
  patientId: string;
}

/** Safely extract lab orders array from either { data: [...] } or a raw array response */
function extractOrders(raw: unknown): LabOrderDto[] {
  if (Array.isArray(raw)) return raw;
  if (raw && typeof raw === "object" && "data" in raw && Array.isArray((raw as { data?: unknown }).data)) {
    return (raw as { data: LabOrderDto[] }).data;
  }
  return [];
}

export function LabOrdersTab({ patientId }: LabOrdersTabProps) {
  const [orders, setOrders] = useState<LabOrderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [fetchError, setFetchError] = useState(false);
  const [retryKey, setRetryKey] = useState(0);

  useEffect(() => {
    setFetchError(false);
    setLoading(true);
    api.get(`/api/lab-orders?patientId=${patientId}`)
      .then((r) => setOrders(extractOrders(r.data)))
      .catch(() => { setFetchError(true); })
      .finally(() => setLoading(false));
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

  if (orders.length === 0) {
    return (
      <EmptyState
        icon={FlaskConical}
        title="لا توجد طلبات مختبر"
        description="لم يتم تسجيل أي طلبات مختبر لهذا المريض"
      />
    );
  }

  return (
    <div className="space-y-2" dir="rtl">
      {orders.map((order) => (
        <div key={order.id} className="p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-[#3d7ab5] hover:shadow-sm transition">
          {/* Row 1: Appliance type + lab name + status badge */}
          <div className="flex items-center justify-between gap-2 flex-wrap">
            <div className="flex items-center gap-2">
              <FlaskConical className="w-4 h-4 text-amber-500 flex-shrink-0" />
              <span className="text-sm font-medium text-[#0d2137]">
                {order.applianceType ?? "طلب مختبر"}
              </span>
              {order.labName && <span className="text-xs text-[#64748b]">({order.labName})</span>}
            </div>
            {order.status && (
              <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                LAB_STATUS_COLORS[order.status] ?? "bg-[#f1f5f9] text-[#64748b]"
              )}>
                {LAB_STATUS_LABELS[order.status] ?? order.status}
              </span>
            )}
          </div>

          {/* Row 2: Date timeline */}
          <div className="flex items-center gap-3 mt-1 flex-wrap">
            {order.sentDate && (
              <span className="text-xs text-[#94a3b8]">إرسال: {formatArabicDate(order.sentDate)}</span>
            )}
            {order.expectedDate && (
              <span className="text-xs text-[#94a3b8]">متوقع: {formatArabicDate(order.expectedDate)}</span>
            )}
            {order.receivedDate && (
              <span className="text-xs text-[#94a3b8]">استلام: {formatArabicDate(order.receivedDate)}</span>
            )}
            {order.deliveredDate && (
              <span className="text-xs text-[#94a3b8]">تسليم: {formatArabicDate(order.deliveredDate)}</span>
            )}
            {!order.sentDate && order.createdAt && (
              <span className="text-xs text-[#94a3b8]">{formatArabicDate(order.createdAt)}</span>
            )}
          </div>

          {/* Row 3: Shade / Restoration type / Cost */}
          {(order.shade || order.restorationType || (order.cost != null && order.cost > 0)) && (
            <div className="flex items-center gap-3 mt-1 flex-wrap">
              {order.shade && (
                <span className="text-xs text-[#3d7ab5] bg-[#3d7ab510] px-1.5 py-0.5 rounded">ظل: {order.shade}</span>
              )}
              {order.restorationType && (
                <span className="text-xs text-[#3d7ab5] bg-[#3d7ab510] px-1.5 py-0.5 rounded">نوع الترميم: {order.restorationType}</span>
              )}
              {order.cost != null && order.cost > 0 && (
                <span className="text-xs text-[#64748b]">التكلفة: {order.cost}</span>
              )}
            </div>
          )}

          {/* Row 4: Instructions */}
          {order.instructions && (
            <p className="text-xs text-[#64748b] mt-1 line-clamp-2">{order.instructions}</p>
          )}

          {/* Row 5: Cancellation reason */}
          {order.cancellationReason && (
            <p className="text-xs text-red-500 mt-1">سبب الإلغاء: {order.cancellationReason}</p>
          )}
        </div>
      ))}
    </div>
  );
}
