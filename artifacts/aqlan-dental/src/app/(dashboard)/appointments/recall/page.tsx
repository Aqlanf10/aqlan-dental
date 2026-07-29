
import { useState } from "react";
import Link from "@/lib/nextLinkCompat";
import { useQuery } from "@tanstack/react-query";
import { BellRing, CalendarPlus, MessageCircle, UserCheck } from "lucide-react";
import api from "@/lib/api";
import { TableSkeleton } from "@/components/ui/skeleton";
import { ErrorBoundary } from "@/components/shared/ErrorBoundary";
import { cn, formatArabicDate, formatPhoneForWhatsApp } from "@/lib/utils";
import { useClinicBranding } from "@/hooks/useClinicBranding";

interface RecallCandidate {
  patientId: string;
  patientName: string;
  patientNumber: string;
  phone: string | null;
  missedCount: number;
  lastMissedDate: string;
}

interface RecallCandidatesResponse {
  items: RecallCandidate[];
  totalCount: number;
  windowDays: number;
}

const WINDOW_OPTIONS = [7, 14, 30, 60, 90];

// MS-TASK-006: clinic name in outbound patient messages comes from settings.
const whatsappMessage = (clinicName: string) =>
  `مرحباً، نود تذكيركم بأن لديكم موعداً فائتاً في ${clinicName}. يرجى التواصل معنا لإعادة حجز موعد جديد. نتمنى لكم دوام الصحة.`;

export default function RecallWorklistPage() {
  const [windowDays, setWindowDays] = useState(30);
  const branding = useClinicBranding();

  const { data, isLoading, isError } = useQuery({
    queryKey: ["recall-candidates", windowDays],
    queryFn: async () => {
      const res = await api.get<RecallCandidatesResponse>(
        "/api/appointments/recall-candidates",
        { params: { windowDays } },
      );
      return res.data;
    },
  });

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;

  return (
    <ErrorBoundary>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <BellRing className="w-7 h-7 text-amber-600" />
            <div>
              <h1 className="text-2xl font-bold text-gray-900">
                قائمة الاستدعاء — مرضى بحاجة لإعادة حجز
              </h1>
              <p className="text-sm text-gray-500 mt-0.5">
                مرضى تغيبوا عن مواعيدهم خلال آخر {windowDays} يوم وليس لديهم موعد قادم
                {!isLoading && !isError && (
                  <span className="text-amber-600 font-medium"> — {totalCount} مريض</span>
                )}
              </p>
            </div>
          </div>

          {/* Window selector */}
          <div className="flex items-center gap-1 bg-white rounded-lg border border-gray-200 p-1 shadow-sm">
            {WINDOW_OPTIONS.map((days) => (
              <button
                key={days}
                onClick={() => setWindowDays(days)}
                className={cn(
                  "px-3 py-1.5 text-xs font-medium rounded-md transition-colors",
                  windowDays === days
                    ? "bg-amber-100 text-amber-800"
                    : "text-gray-500 hover:bg-gray-50",
                )}
              >
                {days} يوم
              </button>
            ))}
          </div>
        </div>

        {/* Table */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          {isLoading ? (
            <div className="p-6"><TableSkeleton rows={5} cols={6} /></div>
          ) : isError ? (
            <div className="flex flex-col items-center py-16 text-red-500">
              <BellRing className="w-10 h-10 mb-3" />
              <p className="font-medium">فشل تحميل قائمة الاستدعاء، حاول مرة أخرى</p>
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center py-16 text-gray-400">
              <UserCheck className="w-10 h-10 mb-3" />
              <p className="font-medium">لا يوجد مرضى بحاجة لإعادة حجز خلال هذه الفترة</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 border-b border-gray-200">
                  <tr>
                    {["اسم المريض", "رقم الملف", "الهاتف", "عدد مرات الغياب", "آخر غياب", "إجراءات"].map((h) => (
                      <th key={h} className="text-right px-4 py-3 font-medium text-gray-600 text-xs whitespace-nowrap">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {items.map((item) => (
                    <tr key={item.patientId} className="hover:bg-amber-50/30">
                      <td className="px-4 py-3">
                        <Link
                          href={`/patients/${item.patientId}`}
                          className="font-medium text-indigo-700 hover:text-indigo-900 hover:underline"
                        >
                          {item.patientName}
                        </Link>
                      </td>
                      <td className="px-4 py-3 font-mono text-xs text-gray-500">{item.patientNumber}</td>
                      <td className="px-4 py-3 text-gray-700" dir="ltr">{item.phone ?? "—"}</td>
                      <td className="px-4 py-3">
                        <span className={cn(
                          "inline-flex items-center px-2 py-0.5 rounded-full text-xs font-bold",
                          item.missedCount >= 2
                            ? "bg-red-100 text-red-700"
                            : "bg-amber-100 text-amber-700",
                        )}>
                          {item.missedCount}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-gray-500 text-xs whitespace-nowrap">
                        {item.lastMissedDate ? formatArabicDate(item.lastMissedDate) : "—"}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          <Link
                            href={`/appointments/new?patientId=${item.patientId}`}
                            className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-medium bg-indigo-50 text-indigo-700 hover:bg-indigo-100 transition-colors"
                          >
                            <CalendarPlus className="w-3.5 h-3.5" />
                            حجز موعد
                          </Link>
                          {item.phone && (
                            <a
                              href={`https://wa.me/${formatPhoneForWhatsApp(item.phone)}?text=${encodeURIComponent(whatsappMessage(branding.clinicName))}`}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="inline-flex items-center justify-center w-7 h-7 rounded-lg hover:bg-green-50 transition-colors"
                              title="إرسال تذكير عبر واتساب"
                            >
                              <MessageCircle className="w-4 h-4" style={{ color: "#25D366" }} />
                            </a>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </ErrorBoundary>
  );
}
