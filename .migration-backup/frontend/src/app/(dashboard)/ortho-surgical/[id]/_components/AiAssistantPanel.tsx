"use client";

import { useState } from "react";
import { Sparkles, Loader2, Copy, AlertTriangle } from "lucide-react";
import type { OrthoSurgicalAiSection, OrthoSurgicalAiDraftResult } from "@/types/orthoSurgical";
import { AI_SECTION_LABELS } from "@/types/orthoSurgical";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface AiAssistantPanelProps {
  orthoSurgicalCaseId: string;
}

const SECTIONS: OrthoSurgicalAiSection[] = ["case_summary", "referral_letter", "patient_explanation", "joint_plan_draft"];

/**
 * Sprint A8 — the AI text-drafting assistant. Every output is a DRAFT: it is shown
 * here for the doctor to read and copy manually into the joint plan / referral /
 * patient file — nothing is auto-applied. Mirrors the safety contract of the
 * existing ceph/ortho-case AI draft endpoints (Settings-gated, honest 403/429/502).
 */
export function AiAssistantPanel({ orthoSurgicalCaseId }: AiAssistantPanelProps) {
  const [section, setSection] = useState<OrthoSurgicalAiSection>("case_summary");
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<OrthoSurgicalAiDraftResult | null>(null);

  const generate = async () => {
    setLoading(true);
    setResult(null);
    try {
      const res = await api.post<OrthoSurgicalAiDraftResult>(
        `/api/ortho-surgical-cases/${orthoSurgicalCaseId}/ai/draft`,
        { section }
      );
      setResult(res.data);
    } catch (e) {
      const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      toast.error(msg ?? "تعذر توليد المسودة");
    } finally {
      setLoading(false);
    }
  };

  const copyDraft = async () => {
    if (!result) return;
    try {
      await navigator.clipboard.writeText(result.draft);
      toast.success("تم نسخ المسودة");
    } catch {
      toast.error("تعذر النسخ");
    }
  };

  return (
    <div className="bg-white rounded-xl border border-[#e8f0f9] shadow-sm p-5 space-y-4">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-700">
        <Sparkles className="w-4 h-4 text-[#3d7ab5]" /> المساعد الذكي (مسودات فقط)
      </h2>

      <div className="flex flex-wrap items-center gap-2">
        <select value={section} onChange={(e) => setSection(e.target.value as OrthoSurgicalAiSection)}
          className="px-3 py-2 text-sm rounded-lg border border-gray-300 bg-white focus:outline-none focus:ring-2 focus:ring-[#3d7ab5] transition">
          {SECTIONS.map((s) => <option key={s} value={s}>{AI_SECTION_LABELS[s]}</option>)}
        </select>
        <button onClick={generate} disabled={loading}
          className="flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] disabled:opacity-60 transition">
          {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Sparkles className="w-4 h-4" />}
          توليد مسودة
        </button>
      </div>

      {result && (
        <div className="space-y-3">
          <div className="bg-gray-50 rounded-lg border border-gray-200 p-3">
            <p className="text-sm text-gray-800 whitespace-pre-wrap">{result.draft}</p>
          </div>

          <div className="flex items-center justify-between">
            <button onClick={copyDraft}
              className="flex items-center gap-1.5 text-xs font-medium text-[#3d7ab5] hover:underline">
              <Copy className="w-3.5 h-3.5" /> نسخ المسودة
            </button>
            <span className="text-[11px] text-gray-400">{result.modelId}</span>
          </div>

          {(result.missingData.length > 0 || result.warnings.length > 0) && (
            <div className="flex flex-wrap gap-1.5">
              {result.missingData.map((m) => (
                <span key={m} className="text-[11px] px-2 py-0.5 rounded-full bg-gray-100 text-gray-500">{m}</span>
              ))}
              {result.warnings.map((w) => (
                <span key={w} className="text-[11px] px-2 py-0.5 rounded-full bg-amber-50 text-amber-700">{w}</span>
              ))}
            </div>
          )}

          <div className="flex items-start gap-2 bg-amber-50 rounded-lg border border-amber-100 p-3">
            <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5" />
            <p className="text-xs text-amber-800">{result.disclaimer}</p>
          </div>
        </div>
      )}
    </div>
  );
}
