"use client";
import { printScreen } from "@/lib/printUtils";
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
  History,
} from "lucide-react";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import type {
  CephAnalysis,
  CephCompareResult,
  CephCompareRow,
  CephMeasurement,
  CephVersionDetail,
  MeasurementSeverity,
} from "@/types/ceph";
import { CephSuperimposeCanvas } from "@/components/ceph/CephSuperimposeCanvas";
import { CephImageSliderCompare } from "@/components/ceph/CephImageSliderCompare";

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

// C-B: compute a client-side diff table when comparing the live analysis
// against a saved VERSION snapshot (the backend CompareAsync requires two
// analysis IDs, so we recompute the rows from the measurement lists). The
// formula mirrors the server: improved = |target − normal| < |base − normal|.
function buildVersionCompareRows(
  base: CephMeasurement[],
  target: CephMeasurement[],
): CephCompareRow[] {
  const baseMap = new Map(base.map(m => [m.name, m]));
  const targetMap = new Map(target.map(m => [m.name, m]));
  const names = new Set<string>([...baseMap.keys(), ...targetMap.keys()]);

  const rows: CephCompareRow[] = [];
  for (const name of names) {
    const b = baseMap.get(name);
    const t = targetMap.get(name);
    const normal = b?.normal ?? t?.normal ?? 0;
    const stdDev = b?.stdDev ?? t?.stdDev ?? 0;
    const unit = (b?.unit ?? t?.unit ?? "°") as CephCompareRow["unit"];
    const bv = b?.value ?? null;
    const tv = t?.value ?? null;
    const delta = bv !== null && tv !== null ? Math.round((tv - bv) * 100) / 100 : null;
    let improved: boolean | null = null;
    if (bv !== null && tv !== null) {
      const beforeDist = Math.abs(bv - normal);
      const afterDist = Math.abs(tv - normal);
      improved = afterDist < beforeDist ? true
        : afterDist > beforeDist ? false
        : null;
    }
    rows.push({
      measurementName: name,
      nameAr: t?.nameAr ?? b?.nameAr ?? name,
      analysisGroup: (t?.analysisGroup ?? b?.analysisGroup ?? null) as CephCompareRow["analysisGroup"],
      unit,
      baseValue: bv,
      targetValue: tv,
      delta,
      normalValue: normal,
      stdDeviation: stdDev,
      baseClassification: (b?.severity ?? null) as MeasurementSeverity | null,
      targetClassification: (t?.severity ?? null) as MeasurementSeverity | null,
      improved,
    });
  }
  rows.sort((a, b) =>
    (a.analysisGroup ?? "zz").localeCompare(b.analysisGroup ?? "zz") ||
    a.measurementName.localeCompare(b.measurementName));
  return rows;
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
  const versionId = params.get("versionId") ?? "";

  // C-B: two modes on this page:
  //   • Analysis-vs-Analysis: ?baseId=&targetId= → server CompareAsync + slider
  //     on the two radiographs.
  //   • Analysis-vs-Version: ?baseId=&versionId= → fetch the saved version
  //     snapshot for the structural superimposition + client-side diff rows.
  //     The slider is hidden (the version shares the same radiograph as the
  //     live analysis, so a before/after image wipe is meaningless here).
  const isVersionMode = Boolean(baseId && versionId);

  const { data, isLoading, error } = useQuery({
    queryKey: ["ceph-compare", baseId, targetId],
    enabled: Boolean(baseId && targetId) && !isVersionMode,
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
    enabled: Boolean(targetId) && !isVersionMode,
    retry: false,
    queryFn: async () => (await api.get<CephAnalysis>(`/api/ceph/${encodeURIComponent(targetId)}`)).data,
  });

  // C-B: version snapshot fetch (only in version mode).
  const versionDetail = useQuery({
    queryKey: ["ceph-version", baseId, versionId],
    enabled: isVersionMode,
    retry: false,
    queryFn: async () =>
      (await api.get<CephVersionDetail>(
        `/api/ceph/${encodeURIComponent(baseId)}/versions/${encodeURIComponent(versionId)}`
      )).data,
  });

  // C-B: client-side diff rows for the version comparison (mirrors the server
  // CompareAsync formula). Recomputed whenever the base analysis or version
  // snapshot lands.
  const versionRows = useMemo<CephCompareRow[]>(() => {
    if (!isVersionMode) return [];
    const base = baseAnalysis.data?.measurements ?? [];
    const target = versionDetail.data?.measurements ?? [];
    if (base.length === 0 || target.length === 0) return [];
    return buildVersionCompareRows(base, target);
  }, [isVersionMode, baseAnalysis.data, versionDetail.data]);

  // The unified row list shown in the table — server rows for analysis mode,
  // client-computed rows for version mode. Wrapped in useMemo so the
  // downstream summary/groups useMemo deps stay referentially stable.
  const effectiveRows = useMemo<CephCompareRow[]>(
    () => (isVersionMode ? versionRows : (data?.rows ?? [])),
    [isVersionMode, versionRows, data],
  );

  const summary = useMemo(() => {
    const s = { improved: 0, worsened: 0, unchanged: 0 };
    for (const row of effectiveRows) s[changeKind(row)] += 1;
    return s;
  }, [effectiveRows]);

  const groups = useMemo(() => {
    const map = new Map<string, CephCompareRow[]>();
    for (const row of effectiveRows) {
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
  }, [effectiveRows]);

  const serverMessage =
    (error as { response?: { data?: { message?: string } } } | null)?.response?.data?.message;

  // Resolve radiograph URLs for the before/after slider (analysis-vs-analysis
  // mode only — version mode shares the same radiograph so the slider is
  // hidden). crossOrigin is intentionally NOT set (CEPH-EPIC: avoid CORS
  // tainting failures against the uploads host).
  const baseImageUrl = baseAnalysis.data?.xrayFileUrl
    ? resolveImageUrl(baseAnalysis.data.xrayFileUrl)
    : "";
  const targetImageUrl = targetAnalysis.data?.xrayFileUrl
    ? resolveImageUrl(targetAnalysis.data.xrayFileUrl)
    : "";

  // The "target date" shown in the header — either the second analysis's date
  // (analysis mode) or the saved version's snapshot date (version mode).
  const targetDateLabel = isVersionMode
    ? (versionDetail.data ? formatArabicDate(versionDetail.data.snapshotDate) : "")
    : (data ? formatArabicDate(data.target.analysisDate) : "");
  const baseDateLabel = isVersionMode
    ? (baseAnalysis.data ? formatArabicDate(baseAnalysis.data.analysisDate) : "")
    : (data ? formatArabicDate(data.base.analysisDate) : "");
  const patientName = isVersionMode
    ? (baseAnalysis.data?.patientName ?? "")
    : (data?.patientName ?? "");

  // Loading + error states for version mode.
  const versionLoading = isVersionMode && versionDetail.isLoading;
  const versionError = isVersionMode && versionDetail.error
    ? ((versionDetail.error as { response?: { data?: { message?: string } } })
        ?.response?.data?.message ?? "تعذر تحميل النسخة المحفوظة")
    : null;

  // Missing params
  if (!baseId || (!targetId && !versionId)) {
    return (
      <div className="space-y-5 max-w-5xl">
        <Breadcrumb />
        <div className="text-center py-20 text-gray-400">
          <AlertTriangle className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لم يتم تحديد تحليلَين للمقارنة</p>
          <p className="text-xs mt-2 text-gray-300">
            اختر «قارن» من قائمة التحاليل ثم «قارن مع هذا» على تحليل آخر لنفس الحالة،
            أو «قارن مع نسخة» على نسخة محفوظة من التحليل
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
          <Link href={baseId ? `/ceph/${baseId}` : "/ceph"}
            className="no-print p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div>
            <h1 className="text-2xl font-extrabold text-gray-900">
              {isVersionMode ? "مقارنة التحليل مع نسخة محفوظة" : "مقارنة سيفالومترية"}
            </h1>
            {patientName && <p className="text-sm text-gray-500 mt-0.5">{patientName}</p>}
          </div>
        </div>

        {(data || isVersionMode) && (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs bg-blue-50 text-blue-700 px-2.5 py-1 rounded-full font-medium">
              {isVersionMode ? "الحالي" : "قبل"}: {baseDateLabel}
            </span>
            <span className="text-xs bg-emerald-50 text-emerald-700 px-2.5 py-1 rounded-full font-medium flex items-center gap-1">
              {isVersionMode && <History className="w-3 h-3" />}
              {isVersionMode ? "النسخة" : "بعد"}: {targetDateLabel}
            </span>
            {!isVersionMode && (
              <button
                onClick={() => router.replace(`/ceph/compare?baseId=${targetId}&targetId=${baseId}`)}
                className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
              >
                <ArrowLeftRight className="w-3.5 h-3.5" />
                تبديل
              </button>
            )}
            <button
              onClick={() => printScreen()}
              className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 transition"
            >
              <Printer className="w-3.5 h-3.5" />
              طباعة
            </button>
          </div>
        )}
      </div>

      {/* Version-mode banner explaining what is being compared */}
      {isVersionMode && versionDetail.data && (
        <div className="rounded-lg border border-teal-200 bg-teal-50 px-3 py-2 text-xs text-teal-800 flex items-start gap-2">
          <History className="w-4 h-4 mt-0.5 flex-shrink-0" />
          <div>
            تقارن هذه الصفحة حالة التحليل <b>الحالية</b> مع النسخة المحفوظة
            «{versionDetail.data.label}» ({formatArabicDate(versionDetail.data.snapshotDate)}).
            التراكب البنيوي يستخدم معالم النسخة كطرف «بعد»، وجدول القياسات يُحتسب في المتصفح
            من قياسات التحليل الحالي مقابل قياسات النسخة المحفوظة.
          </div>
        </div>
      )}

      {versionLoading ? (
        <div className="space-y-3 animate-pulse">
          <div className="h-64 bg-gray-100 rounded-xl" />
          <div className="grid grid-cols-3 gap-3">
            {Array.from({ length: 3 }).map((_, i) => <div key={i} className="h-20 bg-gray-100 rounded-xl" />)}
          </div>
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-xl" />)}
        </div>
      ) : versionError ? (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-4 text-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          {versionError}
        </div>
      ) : isLoading && !isVersionMode ? (
        <div className="space-y-3 animate-pulse">
          <div className="h-64 bg-gray-100 rounded-xl" />
          <div className="grid grid-cols-3 gap-3">
            {Array.from({ length: 3 }).map((_, i) => <div key={i} className="h-20 bg-gray-100 rounded-xl" />)}
          </div>
          {Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 bg-gray-100 rounded-xl" />)}
        </div>
      ) : error && !isVersionMode ? (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-4 text-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          {serverMessage ?? "تعذر تحميل بيانات المقارنة"}
        </div>
      ) : effectiveRows.length === 0 ? (
        <div className="text-center py-20 text-gray-400">
          <Minus className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">لا توجد قياسات مشتركة بين الطرفين للمقارنة</p>
        </div>
      ) : (
        <>
          {/* C-B: before/after radiograph wipe slider (analysis-vs-analysis only). */}
          {!isVersionMode && baseImageUrl && targetImageUrl && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 avoid-break">
              <h2 className="text-sm font-bold text-gray-700 mb-3">مقارنة صور الأشعة (قبل/بعد)</h2>
              <CephImageSliderCompare
                baseImageUrl={baseImageUrl}
                targetImageUrl={targetImageUrl}
                baseDate={formatArabicDate(data!.base.analysisDate)}
                targetDate={formatArabicDate(data!.target.analysisDate)}
              />
            </div>
          )}

          {/* Visual superimposition (cranial base, registered on SN) */}
          {baseAnalysis.data &&
           (isVersionMode
             ? versionDetail.data?.landmarks && versionDetail.data.landmarks.length > 0
             : targetAnalysis.data && targetAnalysis.data.landmarks.length > 0) &&
           baseAnalysis.data.landmarks.length > 0 && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 avoid-break">
              <h2 className="text-sm font-bold text-gray-700 mb-3">التراكب البنيوي (قاعدة الجمجمة)</h2>
              <CephSuperimposeCanvas
                baseLandmarks={baseAnalysis.data.landmarks}
                targetLandmarks={
                  isVersionMode
                    ? versionDetail.data!.landmarks
                    : targetAnalysis.data!.landmarks
                }
                baseDate={baseDateLabel}
                targetDate={targetDateLabel}
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
                    {["القياس", isVersionMode ? "الحالي" : "قبل",
                      isVersionMode ? "النسخة" : "بعد",
                      "الفرق", "المعيار", "الحالة"].map((h) => (
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
