"use client";

import { useEffect, useState } from "react";
import { FlaskConical } from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

interface LabOrderDto {
  id: string;
  orderDate: string;
  labName?: string;
  workType?: string;
  status?: string;
  dueDate?: string;
  notes?: string;
}

const LAB_STATUS_LABELS: Record<string, string> = {
  pending: "معلّق",
  in_progress: "جارٍ التنفيذ",
  completed: "مكتمل",
  cancelled: "ملغى",
  sent: "مرسل",
  received: "مستلم",
};

interface LabOrdersTabProps {
  patientId: string;
}

export function LabOrdersTab({ patientId }: LabOrdersTabProps) {
  const [orders, setOrders] = useState<LabOrderDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.get<LabOrderDto[]>(`/api/lab-orders?patientId=${patientId}`)
      .then((r) => setOrders(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  if (loading) {
    return (
      <div className="space-y-2 animate-pulse">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-16 bg-[#f1f5f9] rounded-lg" />
        ))}
      </div>
    );
  }

  if (orders.length === 0) {
    return (
      <div className="text-center py-12 text-[#94a3b8]" dir="rtl">
        <FlaskConical className="w-10 h-10 mx-auto mb-2 opacity-30" />
        <p className="text-sm">لا توجد طلبات مختبر</p>
      </div>
    );
  }

  return (
    <div className="space-y-2" dir="rtl">
      {orders.map((order) => (
        <div key={order.id} className="p-3 bg-white border border-[#e8f0f9] rounded-lg hover:border-[#e8f0f9] transition">
          <div className="flex items-center justify-between gap-2 flex-wrap">
            <div className="flex items-center gap-2">
              <FlaskConical className="w-4 h-4 text-amber-500 flex-shrink-0" />
              <span className="text-sm font-medium text-[#0d2137]">
                {order.workType ?? "طلب مختبر"}
              </span>
              {order.labName && <span className="text-xs text-[#64748b]">({order.labName})</span>}
            </div>
            {order.status && (
              <span className={cn("text-xs px-1.5 py-0.5 rounded-full font-medium",
                order.status === "completed" || order.status === "received" ? "bg-green-50 text-green-700" :
                order.status === "in_progress" || order.status === "sent" ? "bg-yellow-50 text-yellow-700" :
                order.status === "pending" ? "bg-[#3d7ab518] text-[#3d7ab5]" :
                "bg-[#f1f5f9] text-[#64748b]"
              )}>
                {LAB_STATUS_LABELS[order.status] ?? order.status}
              </span>
            )}
          </div>
          <div className="flex items-center gap-3 mt-1">
            <span className="text-xs text-[#94a3b8]">{formatArabicDate(order.orderDate)}</span>
            {order.dueDate && <span className="text-xs text-[#94a3b8]">موعد التسليم: {formatArabicDate(order.dueDate)}</span>}
          </div>
          {order.notes && <p className="text-xs text-[#64748b] mt-1 line-clamp-2">{order.notes}</p>}
        </div>
      ))}
    </div>
  );
}
