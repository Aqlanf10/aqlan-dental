"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { GitBranch, ChevronLeft, Plus, Loader2, Stethoscope, Scissors } from "lucide-react";
import api from "@/lib/api";
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
  const [creating, setCreating] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [selectedOrthoCase, setSelectedOrthoCase] = useState("");

  const fetchCases = useCallback(async () => {
    try {
      setLoading(true);
      const [osRes, orthoRes] = await Promise.all([
        api.get<{ data: OrthoSurgicalCaseListItem[] }>(`/api/ortho-surgical-cases?patientId=${patientId}`),
        api.get<OrthoCaseLite[]>(patientActiveOrthoCasesUrl(patientId)),
      ]);
      setCases(osRes.data?.data ?? []);
      setOrthoCases(orthoRes.data ?? []);
    } catch {
      /* silent — EmptyState covers the no-data case */
    } finally {
      setLoading(false);
    }
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
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "فشل إنشاء الحالة");
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

  if (cases.length === 0 && !showCreate) {
    return (
      <div className="space-y-4">
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
