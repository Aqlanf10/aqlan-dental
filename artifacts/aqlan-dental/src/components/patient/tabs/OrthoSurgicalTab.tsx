
import { useEffect, useState, useCallback } from "react";
import Link from "@/lib/nextLinkCompat";
import { GitBranch, ChevronLeft, Plus, Loader2, Stethoscope, Scissors, AlertTriangle } from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { patientActiveOrthoCasesUrl } from "@/lib/orthoCaseRoutes";
import { EmptyState } from "./EmptyState";
import { cn } from "@/lib/utils";
import { toast } from "@/stores/toastStore";
import type { OrthoSurgicalCaseListItem } from "@/types/orthoSurgical";
import { ORTHO_SURGICAL_STATUS_COLORS } from "@/types/orthoSurgical";

interface OrthoSurgicalTabProps {
  patientId: string;
}

interface OrthoCaseLite {
  id: string;
  caseNumber: string;
  status: string;
}

export function OrthoSurgicalTab({ patientId }: OrthoSurgicalTabProps) {
  const [cases, setCases] = useState<OrthoSurgicalCaseListItem[]>([]);
  const [orthoCases, setOrthoCases] = useState<OrthoCaseLite[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [orthoCasesError, setOrthoCasesError] = useState("");
  const [creating, setCreating] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [selectedOrthoCase, setSelectedOrthoCase] = useState("");

  const fetchCases = useCallback(async () => {
    setLoading(true);
    setLoadError("");
    setOrthoCasesError("");

    const [surgicalResult, orthoResult] = await Promise.allSettled([
      api.get<{ data: OrthoSurgicalCaseListItem[] }>(`/api/ortho-surgical-cases?patientId=${patientId}`)
        .then((response) => response.data?.data ?? []),
      api.get<OrthoCaseLite[]>(patientActiveOrthoCasesUrl(patientId))
        .then((response) => response.data ?? []),
    ]);

    if (surgicalResult.status === "rejected") {
      setCases([]);
      setOrthoCases(orthoResult.status === "fulfilled" ? orthoResult.value : []);
      setLoadError(
        extractErrorMessage(
          surgicalResult.reason,
          "تعذر تحميل الحالات التقويمية الجراحية — تحقق من الاتصال وحاول مجدداً"
        )
      );
      setLoading(false);
      return;
    }

    setCases(surgicalResult.value);
    if (orthoResult.status === "fulfilled") {
      setOrthoCases(orthoResult.value);
    } else {
      setOrthoCases([]);
      setShowCreate(false);
      setSelectedOrthoCase("");
      setOrthoCasesError(
        extractErrorMessage(
          orthoResult.reason,
          "تعذر تحميل حالات التقويم النشطة؛ إنشاء حالة تقويمية جراحية غير متاح حالياً"
        )
      );
    }
    setLoading(false);
  }, [patientId]);

  useEffect(() => { fetchCases(); }, [fetchCases]);

  // Ortho cases that don't yet have an ortho-surgical case linked.
  const linkedOrthoCaseIds = new Set(cases.map((c) => c.orthoCaseId));
  const availableOrthoCases = orthoCases.filter((o) => !linkedOrthoCaseIds.has(o.id));

  const createCase = async () => {
    if (!selectedOrthoCase) return;
    setCreating(true);
    try {
      await api.post("/api/ortho-surgical-cases", { orthoCaseId: selectedOrthoCase });
      toast.success("تم إنشاء الحالة التقويمية الجراحية");
      setShowCreate(false);
      setSelectedOrthoCase("");
      await fetchCases();
    } catch (error: unknown) {
      toast.error(extractErrorMessage(error, "فشل إنشاء الحالة"));
    } finally {
      setCreating(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-3 animate-pulse">
        {Array.from({ length: 2 }).map((_, i) => <div key={i} className="h-24 bg-[#f1f5f9] rounded-xl" />)}
      </div>
    );
  }

  if (loadError) {
    return (
      <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-6 text-center">
        <AlertTriangle className="w-10 h-10 mx-auto mb-3 text-red-400" />
        <p className="text-sm font-semibold text-red-700">{loadError}</p>
        <p className="text-xs text-red-500 mt-1">
          لم نعتبر فشل الطلب «لا توجد حالات تقويمية جراحية».
        </p>
        <button
          type="button"
          onClick={fetchCases}
          className="mt-4 px-4 py-2 text-sm font-semibold rounded-lg bg-white border border-red-200 text-red-700 hover:bg-red-100 transition"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  if (cases.length === 0 && !showCreate) {
    return (
      <div className="space-y-4">
        {orthoCasesError && (
          <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 flex items-start gap-3">
            <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
            <div className="flex-1">
              <p className="text-xs font-semibold text-amber-800">{orthoCasesError}</p>
              <p className="text-[11px] text-amber-700 mt-0.5">يمكن عرض الحالات الجراحية، لكن لا يمكن التحقق من أهلية إنشاء حالة جديدة.</p>
            </div>
            <button type="button" onClick={fetchCases} className="text-xs font-semibold text-amber-800 underline">إعادة المحاولة</button>
          </div>
        )}
        <EmptyState
          icon={GitBranch}
          title="لا توجد حالات تقويمية جراحية"
          description="الحالة التقويمية الجراحية تربط خطة التقويم بالسيفالو ومراجعة الجراح واعتماد الطرفين. تُنشأ من حالة تقويم قائمة."
        />
        {availableOrthoCases.length > 0 ? (
          <button onClick={() => setShowCreate(true)}
            className="flex items-center justify-center gap-2 w-full p-3 border-2 border-dashed border-[#3d7ab5]/30 rounded-xl text-sm font-medium text-[#3d7ab5] hover:bg-[#3d7ab5]/5 transition">
            <Plus className="w-4 h-4" /> إنشاء حالة تقويمية جراحية
          </button>
        ) : (
          <p className="text-center text-xs text-gray-400">
            يلزم وجود حالة تقويم للمريض أولًا لإنشاء حالة تقويمية جراحية.
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {orthoCasesError && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 flex items-start gap-3">
          <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <p className="text-xs font-semibold text-amber-800">{orthoCasesError}</p>
            <p className="text-[11px] text-amber-700 mt-0.5">الحالات الحالية ظاهرة، لكن إنشاء حالة جديدة معطّل حتى ينجح تحميل حالات التقويم النشطة.</p>
          </div>
          <button type="button" onClick={fetchCases} className="text-xs font-semibold text-amber-800 underline">إعادة المحاولة</button>
        </div>
      )}
      {cases.map((c) => (
        <div key={c.id} className="rounded-xl border border-[#e8f0f9] bg-white p-4 hover:border-[#3d7ab5]/40 transition">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-3 min-w-0">
              <div className="w-11 h-11 rounded-lg flex items-center justify-center bg-[#3d7ab5]/10 flex-shrink-0">
                <GitBranch className="w-5 h-5 text-[#3d7ab5]" />
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="text-sm font-bold text-[#1a3a5c] font-mono">{c.caseNumber}</span>
                  <span className={cn("text-xs px-2 py-0.5 rounded-full font-medium", ORTHO_SURGICAL_STATUS_COLORS[c.status])}>
                    {c.statusLabel}
                  </span>
                </div>
                <div className="flex items-center gap-3 mt-0.5 text-xs text-[#64748b] flex-wrap">
                  <span className="flex items-center gap-1"><Stethoscope className="w-3 h-3" /> {c.orthodontistName ?? "—"}</span>
                  <span className="flex items-center gap-1"><Scissors className="w-3 h-3" /> {c.surgeonName ?? "لم يُحدَّد"}</span>
                  <span>المسؤول: {c.responsibleParty}</span>
                </div>
              </div>
            </div>
            <Link href={`/ortho/${c.orthoCaseId}?tab=surgical`}
              className="flex items-center gap-1 text-sm font-medium text-[#3d7ab5] hover:text-[#1a3a5c] transition flex-shrink-0">
              التفاصيل <ChevronLeft className="w-4 h-4" />
            </Link>
          </div>
        </div>
      ))}

      {/* Create panel */}
      {showCreate ? (
        <div className="rounded-xl border border-[#3d7ab5]/30 bg-[#f7fafd] p-4 space-y-3">
          <h3 className="text-sm font-semibold text-gray-700">إنشاء حالة تقويمية جراحية</h3>
          <select value={selectedOrthoCase} onChange={(e) => setSelectedOrthoCase(e.target.value)}
            className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5]">
            <option value="">اختر حالة التقويم...</option>
            {availableOrthoCases.map((o) => <option key={o.id} value={o.id}>حالة تقويم {o.caseNumber}</option>)}
          </select>
          <div className="flex items-center justify-end gap-2">
            <button onClick={() => { setShowCreate(false); setSelectedOrthoCase(""); }}
              className="px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-600 hover:bg-gray-100 transition">
              إلغاء
            </button>
            <button onClick={createCase} disabled={!selectedOrthoCase || creating}
              className="flex items-center gap-2 px-4 py-1.5 text-xs font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition">
              {creating ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
              إنشاء
            </button>
          </div>
        </div>
      ) : availableOrthoCases.length > 0 && (
        <button onClick={() => setShowCreate(true)}
          className="flex items-center justify-center gap-2 w-full p-3 border-2 border-dashed border-[#3d7ab5]/30 rounded-xl text-sm font-medium text-[#3d7ab5] hover:bg-[#3d7ab5]/5 transition">
          <Plus className="w-4 h-4" /> إنشاء حالة تقويمية جراحية
        </button>
      )}
    </div>
  );
}
