"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { FlaskConical, Loader2, ExternalLink } from "lucide-react";
import api from "@/lib/api";
import { formatArabicDate, formatYemeniRiyal } from "@/lib/utils";
import { cn } from "@/lib/utils";

interface LabOrderRow {
  id: string;
  orderNumber?: string | null;
  applianceType?: string | null;
  status?: string | null;
  priority?: string | null;
  labName?: string | null;
  totalCost?: number | null;
  sentDate?: string | null;
  expectedDate?: string | null;
  receivedDate?: string | null;
  deliveredDate?: string | null;
}

const STATUS_AR: Record<string, string> = {
  draft: "مسودة", sent: "مُرسل", manufacturing: "قيد التصنيع", tryIn: "تجربة",
  ready: "جاهز", received: "مُستلم", delivered: "مُسلّم", returned: "مُرتجع",
  remake: "إعادة عمل", cancelled: "ملغى",
};
const STATUS_CLS: Record<string, string> = {
  ready: "bg-green-50 text-green-700", received: "bg-green-50 text-green-700",
  delivered: "bg-green-50 text-green-700", cancelled: "bg-gray-100 text-gray-500",
  returned: "bg-red-50 text-red-700", remake: "bg-red-50 text-red-700",
};

/** Read-only view of lab orders linked to this ortho case (LabOrder.OrthoCaseId).
 *  The Lab module remains the source of truth — this never edits orders. */
export function LabOrdersPanel({ caseId }: { caseId: string }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["ortho-lab-orders", caseId],
    enabled: !!caseId,
    retry: false,
    queryFn: async () =>
      (await api.get<LabOrderRow[]>(`/api/ortho-cases/${encodeURIComponent(caseId)}/lab-orders`)).data,
  });

  return (
    <div className="space-y-4" dir="rtl">
      <div className="flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-sm font-bold text-clinic-navy">
          <FlaskConical className="h-4 w-4 text-clinic-blue" />أوامر المختبر
        </h3>
        <Link href="/lab"
          className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50">
          <ExternalLink className="h-3.5 w-3.5" />وحدة المختبر
        </Link>
      </div>

      {isLoading ? (
        <div className="flex items-center gap-2 py-8 text-sm text-gray-400"><Loader2 className="h-4 w-4 animate-spin" />جارٍ التحميل…</div>
      ) : isError ? (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">تعذّر تحميل أوامر المختبر</div>
      ) : !data || data.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 py-10 text-center text-sm text-gray-400">
          لا أوامر مختبر مرتبطة بهذه الحالة. تُنشأ أوامر المختبر من وحدة المختبر وتُربط بالحالة.
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500">
              <tr>
                {["الرقم", "الجهاز", "الحالة", "المختبر", "التكلفة", "الإرسال", "المتوقع"].map((h) => (
                  <th key={h} className="px-3 py-2 text-start font-semibold whitespace-nowrap">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {data.map((o) => (
                <tr key={o.id} className="hover:bg-gray-50/60">
                  <td className="px-3 py-2 font-mono text-xs text-gray-700" dir="ltr">{o.orderNumber ?? "—"}</td>
                  <td className="px-3 py-2 text-gray-800">{o.applianceType ?? "—"}</td>
                  <td className="px-3 py-2">
                    <span className={cn("rounded-full px-2 py-0.5 text-[11px] font-medium", STATUS_CLS[o.status ?? ""] ?? "bg-blue-50 text-blue-700")}>
                      {STATUS_AR[o.status ?? ""] ?? o.status ?? "—"}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-gray-600">{o.labName ?? "—"}</td>
                  <td className="px-3 py-2 font-mono text-xs text-gray-700" dir="ltr">{o.totalCost ? formatYemeniRiyal(o.totalCost) : "—"}</td>
                  <td className="px-3 py-2 text-xs text-gray-500">{o.sentDate ? formatArabicDate(o.sentDate) : "—"}</td>
                  <td className="px-3 py-2 text-xs text-gray-500">{o.expectedDate ? formatArabicDate(o.expectedDate) : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
