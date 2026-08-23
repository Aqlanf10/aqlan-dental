"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { FlaskConical, AlertTriangle } from "lucide-react";
import api from "@/lib/api";
import { cn } from "@/lib/utils";
import type { LabOrder } from "@/types/lab";

/**
 * Lab work on a phone, with the late ones first.
 *
 * Two lists, because they answer different questions: "what is late" is the one that costs the
 * clinic appointments, and "what is due today" is the one that decides whether tomorrow is
 * also a problem. Both come from endpoints the server already computes, so lateness is not
 * re-derived here against a device clock in a different timezone.
 */

type Tab = "overdue" | "today";

const STATUS_LABEL: Record<string, string> = {
  draft: "مسودة",
  sent: "تم الإرسال",
  manufacturing: "قيد الصنع",
  tryIn: "تجربة",
  ready: "جاهز",
  received: "تم الاستلام",
  delivered: "تم التسليم",
  returned: "مرتجع",
  remake: "إعادة صناعة",
  cancelled: "ملغى",
};

export default function MobileLabPage() {
  const [tab, setTab] = useState<Tab>("overdue");

  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ["m", "lab", tab],
    queryFn: async () => {
      const res = await api.get<LabOrder[] | { data: LabOrder[] }>(
        tab === "overdue" ? "/api/lab-orders/overdue" : "/api/lab-orders/today",
      );
      return Array.isArray(res.data) ? res.data : (res.data?.data ?? []);
    },
  });

  const orders = data ?? [];

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h1 className="text-base font-bold text-gray-900">المعامل</h1>
        <button
          type="button"
          onClick={() => refetch()}
          className="text-xs text-cyan-700 font-medium px-2 py-1 -m-1"
        >
          {isFetching ? "جارٍ التحديث…" : "تحديث"}
        </button>
      </div>

      {/* Two tabs only, each half the width — a scrolling pill strip is what made the desktop
          lab screen unusable on a phone in the first place (CORE-LAB-021). */}
      <div className="flex bg-gray-100 rounded-lg p-1">
        {([
          { key: "overdue", label: "متأخرة" },
          { key: "today", label: "اليوم" },
        ] as const).map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => setTab(t.key)}
            className={cn(
              "flex-1 min-h-11 rounded-md text-sm font-medium",
              tab === t.key ? "bg-white text-gray-900 shadow-sm" : "text-gray-500",
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <p className="text-sm text-gray-500 py-8 text-center">جارٍ التحميل…</p>
      ) : isError ? (
        <div className="bg-white rounded-xl p-5 text-center space-y-3">
          <p className="text-sm text-red-700">تعذّر تحميل طلبات المعمل.</p>
          <button
            type="button"
            onClick={() => refetch()}
            className="text-sm font-bold text-cyan-700 min-h-11 px-4"
          >
            إعادة المحاولة
          </button>
        </div>
      ) : orders.length === 0 ? (
        <div className="bg-white rounded-xl p-8 text-center text-gray-400">
          <FlaskConical className="w-8 h-8 mx-auto mb-2" />
          <p className="text-sm font-medium">
            {tab === "overdue" ? "لا توجد طلبات متأخرة" : "لا توجد طلبات لليوم"}
          </p>
        </div>
      ) : (
        <ul className="space-y-2">
          {orders.map((order) => (
            <li
              key={order.id}
              className={cn(
                "bg-white rounded-xl p-3 shadow-sm",
                tab === "overdue" && "border-s-4 border-red-500",
              )}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="font-bold text-gray-900 text-sm truncate">{order.patientName}</p>
                  <p className="text-xs text-gray-500 truncate">
                    {order.applianceType}
                    {order.labEntityName || order.labName
                      ? ` · ${order.labEntityName ?? order.labName}`
                      : ""}
                  </p>
                </div>
                {tab === "overdue" && <AlertTriangle className="w-4 h-4 text-red-600 shrink-0" />}
              </div>

              <div className="flex items-center justify-between gap-2 mt-2 text-xs">
                <span className="px-2 py-0.5 rounded-full bg-gray-100 text-gray-600 font-medium">
                  {STATUS_LABEL[order.status] ?? order.status}
                </span>
                <span className="text-gray-500 font-mono">{order.orderNumber}</span>
              </div>

              {order.expectedDate && (
                <p className="text-[11px] text-gray-400 mt-1">
                  متوقع: {order.expectedDate}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
