"use client";
import { Suspense, useMemo } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeftRight,
  ArrowRight,
  Printer,
  TrendingUp,
  TrendingDown,
  Minus,
  AlertTriangle,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import type { CephAnalysis, CephCompareResult, CephCompareRow, MeasurementSeverity } from "@/types/ceph";
import { CephSuperimposeCanvas } from "@/components/ceph/CephSuperimposeCanvas";

// ─── Labels ──────────────────────────────────────────────────────────────────

const GROUP_AR: Record<string, string> = {
  steiner: "تحليل Steiner",
  tweed: "تحليل Tweed",
  mcnamara: "تحليل McNamara",
  ricketts: "تحليل Ricketts",
  downs: "تحليل Downs",
  jarabak: "تحليل Jarabak",
  wits: "تحليل Wits",
};
const GROUP_ORDER = ["steiner", "tweed", "mcnamara", "ricketts", "downs", "jarabak", "wits", "other"];

const SEVERITY_AR: Record<MeasurementSeverity, string> = {
  normal: "طبيعي",
  mild: "انحراف بسيط",
  severe: "انحراف شديد",
};
const SEVERITY_CLS: Record<MeasurementSeverity, string> = {
  normal: "bg-green-50 text-green-700 border-green-200",
  mild: "bg-yellow-50 text-yellow-700 border-yellow-200",
  severe: "bg-red-50 text-red-700 border-red-200",
};

// ─── Helpers ─────────────────────────────────────────────────────────────────

type ChangeKind = "improved" | "worsened" | "unchanged";

function changeKind(row: CephCompareRow): ChangeKind {
  if (row.improved === null || row.delta === null || Math.abs(row.delta) <= 0.1) return "unchanged";
  return row.improved ? "improved" : "worsened";
}

function fmtVal(v: number | null, unit: string): string {
  if (v === null) return "—";
  return `${v.toFixed(1)}${unit}`;
}

function fmtDelta(d: number | null, unit: string): string {
  if (d === null) return "—";
  const sign = d > 0 ? "+" : "";
  return `${sign}${d.toFixed(1)}${unit}`;
}

function SeverityBadge({ severity }: { severity: MeasurementSeverity | null }) {
  if (!severity) return <span className="text-xs text-gray-400">—</span>;
  return (
    <span className={cn("inline-block text-xs font-medium px-2 py-0.5 rounded-full border", SEVERITY_CLS[severity])}>
      {SEVERITY_AR[severity]}
    </span>
  );
}

// ─── Page ────────────────────────────────────────────────────────────────────

function ComparePageInner() {
  const router = useRouter();
  const params = useSearchParams();
  const baseId = params.get("baseId") ?? "";
  const targetId = params.get("targetId") ?? "";

  const { data, isLoading, error } = useQuery({
    queryKey: ["ceph-compare", baseId, targetId],
    enabled: Boolean(baseId && targetId),
    retry: false,
    queryFn: async () => {
      const res = await api.get<CephCompareResult>(
        `/api/ceph/compare?baseId=${encodeURIComponent(baseId)}&targetId=${encodeURIComponent(targetId)}`
      );
      return res.data;
    },
  });

  // Full analyses (with landmark coordinates) for the visual superimposition.
  const baseAnalysis = useQuery({
    queryKey: ["ceph-analysis", baseId],
    enabled: Boolean(baseId),
    retry: false,
    queryFn: async () => (await api.get<CephAnalysis>(`/api/ceph/${encodeURIComponent(baseId)}`)).data,
  });
  const targetAnalysis = useQuery({
    queryKey: ["ceph-analysis", targetId],
    enabled: Boolean(targetId),
    retry: false,
    queryFn: async () => (await api.get<CephAnalysis>(`/api/ceph/${encodeURIComponent(targetId)}`)).data,
  });

  const summary = useMemo(() => {
    const s = { improved: 0, worsened: 0, unchanged: 0 };
    for (const row of data?.rows ?? []) s[changeKind(row)] += 1;
    return s;
  }, [data]);

  const groups = useMemo(() => {
    const map = new Map<string, CephCompareRow[]>();
    for (const row of data?.rows ?? []) {
      const key = row.analysisGroup ?? "other";
      const list = map.get(key) ?? [];
      list.push(row);
      map.set(key, list);
    }
    return GROUP_ORDER.filter((g) => map.has(g)).map((g) => ({
      key: g,
      label: GROUP_AR[g] ?? "أخرى",
      rows: map.get(g)!,
    }));
  }, [data]);

  const serverMessage =
    (error as { response?: { data?: { message?: string } } } | null)?.response?.data?.message;

  // Missing params
  if (!baseId || !targetId) {
    return (
      <div className="space-y-5 max-w-5xl">
        <Breadcrumb />
        <div className="text-center py-20 text-gray-400">
          <AlertTriangle className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لم يتم تحديد تحليلَين للمقارنة</p>
          <p className="text-xs mt-2 text-gray-300">
            اختر «قارن» من قائمة التحاليل ثم «قارن مع هذا» على تحليل آخر لنفس الحالة
          </p>
          <Link href="/ceph" className="inline-block mt-4 text-sm text-clinic-blue hover:underline font-medium">
            العودة إلى قائمة التحاليل
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-5 max-w-5xl">
      <Breadcrumb />

      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Link href="/ceph"
            className="no-print p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div>
            <h1 className="text-2xl font-extrabold text-gray-900">مقارنة سيفالومترية</h1>
            {data && <p className="text-sm text-gray-500 mt-0.5">{data.patientName}</p>}
          </div>
        </div>

        {data && (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs bg-blue-50 text-blue-700 px-2.5 py-1 rounded-full font-medium">
              قبل: {formatArabicDate(data.base.analysisDate)}
            </span>
            <span className="text-xs bg-emerald-50 text-emerald-700 px-2.5 py-1 rounded-full font-medium">
              بعد: {formatArabicDate(data.target.analysisDate)}
            </span>
            <button
              onClick={() => router.replace(`/ceph/compare?baseId=${targetId}&targetId=${baseId}`)}
              className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              <ArrowLeftRight className="w-3.5 h-3.5" />
              تبديل
            </button>
            <button
              onClick={() => window.print()}
              className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
            >
              <Printer className="w-3.5 h-3.5" />
              طباعة
            </button>
          </div>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-3 animate-pulse">
          <div className="grid grid-cols-3 gap-3">
            {Array.from({ length: 3 }).map((_, i) => <div key={i} className="h-20 bg-gray-100 rounded-xl" />)}
          </div>
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-xl" />)}
        </div>
      ) : error ? (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-4 text-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          {serverMessage ?? "تعذر تحميل بيانات المقارنة"}
        </div>
      ) : !data || data.rows.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <Minus className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد قياسات مشتركة بين التحليلَين للمقارنة</p>
        </div>
      ) : (
        <>
          {/* Visual superimposition (cranial base, registered on SN) */}
          {baseAnalysis.data && targetAnalysis.data &&
           baseAnalysis.data.landmarks.length > 0 && targetAnalysis.data.landmarks.length > 0 && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 avoid-break">
              <h2 className="text-sm font-bold text-gray-700 mb-3">التراكب البنيوي (قاعدة الجمجمة)</h2>
              <CephSuperimposeCanvas
                baseLandmarks={baseAnalysis.data.landmarks}
                targetLandmarks={targetAnalysis.data.landmarks}
                baseDate={formatArabicDate(data.base.analysisDate)}
                targetDate={formatArabicDate(data.target.analysisDate)}
              />
            </div>
          )}

          {/* Summary cards */}
          <div className="grid grid-cols-3 gap-3">
            <SummaryCard icon={TrendingUp} label="تحسّن" count={summary.improved}
              cls="border-green-200 bg-green-50 text-green-700" />
            <SummaryCard icon={TrendingDown} label="تراجع" count={summary.worsened}
              cls="border-red-200 bg-red-50 text-red-700" />
            <SummaryCard icon={Minus} label="بلا تغيير" count={summary.unchanged}
              cls="border-gray-200 bg-gray-50 text-gray-600" />
          </div>

          {/* Comparison table */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-gray-50 border-b border-gray-200">
                  <tr>
                    {["القياس", "قبل", "بعد", "الفرق", "المعيار", "الحالة"].map((h) => (
                      <th key={h} className="text-start px-4 py-3 text-xs font-semibold text-gray-500 whitespace-nowrap">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {groups.map((group) => (
                    <GroupRows key={group.key} label={group.label} rows={group.rows} />
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function Breadcrumb() {
  return (
    <div className="no-print flex items-center gap-2 text-sm text-gray-500">
      <Link href="/ceph" className="hover:text-clinic-blue transition">السيفالومتري</Link>
      <span>/</span>
      <span className="text-gray-900 font-medium">مقارنة</span>
    </div>
  );
}

function SummaryCard({ icon: Icon, label, count, cls }: {
  icon: typeof TrendingUp; label: string; count: number; cls: string;
}) {
  return (
    <div className={cn("rounded-xl border p-4 flex items-center gap-3 avoid-break", cls)}>
      <Icon className="w-5 h-5 shrink-0" />
      <div>
        <div className="text-2xl font-extrabold leading-tight">{count}</div>
        <div className="text-xs font-medium">{label}</div>
      </div>
    </div>
  );
}

function GroupRows({ label, rows }: { label: string; rows: CephCompareRow[] }) {
  return (
    <>
      <tr className="bg-gray-50/70">
        <td colSpan={6} className="px-4 py-2 text-xs font-bold text-gray-600">{label}</td>
      </tr>
      {rows.map((row) => {
        const kind = changeKind(row);
        return (
          <tr key={`${row.analysisGroup ?? "other"}-${row.measurementName}`} className="hover:bg-gray-50 transition">
            <td className="px-4 py-3">
              <div className="font-medium text-gray-900">{row.nameAr}</div>
              <div className="text-xs text-gray-400 font-mono" dir="ltr">{row.measurementName}</div>
            </td>
            <td className="px-4 py-3 font-mono text-gray-700 whitespace-nowrap" dir="ltr">
              {fmtVal(row.baseValue, row.unit)}
            </td>
            <td className="px-4 py-3 font-mono text-gray-700 whitespace-nowrap" dir="ltr">
              {fmtVal(row.targetValue, row.unit)}
            </td>
            <td className="px-4 py-3 whitespace-nowrap">
              <span dir="ltr" className={cn("font-mono font-semibold",
                kind === "improved" ? "text-green-600" : kind === "worsened" ? "text-red-600" : "text-gray-400"
              )}>
                {fmtDelta(row.delta, row.unit)}
              </span>
            </td>
            <td className="px-4 py-3 text-xs text-gray-500 font-mono whitespace-nowrap" dir="ltr">
              {row.normalValue == null
                ? "—"
                : `${row.normalValue.toFixed(1)}${row.stdDeviation == null ? "" : ` ± ${row.stdDeviation.toFixed(1)}`}${row.unit}`}
            </td>
            <td className="px-4 py-3">
              <SeverityBadge severity={row.targetClassification} />
            </td>
          </tr>
        );
      })}
    </>
  );
}

export default function CephComparePage() {
  return (
    <Suspense>
      <ComparePageInner />
    </Suspense>
  );
}
