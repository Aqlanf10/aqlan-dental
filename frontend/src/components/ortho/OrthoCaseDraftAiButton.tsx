"use client";

import { useState } from "react";
import { Loader2, Sparkles } from "lucide-react";
import api from "@/lib/api";
import { toast } from "@/stores/toastStore";

interface DraftResponse {
  draft: string;
  disclaimer?: string;
  evidenceUsed?: string[];
  missingData?: string[];
  warnings?: string[];
  modelId?: string;
}

const sectionFor = (kind?: string) => kind === "mechano" ? "mechanotherapy" : kind || "diagnosis";

export function OrthoCaseDraftAiButton({
  caseId,
  draftKind,
  currentDraft,
  template,
  onDraft,
}: {
  caseId: string;
  draftKind?: string;
  currentDraft: string;
  template: string;
  onDraft: (value: string) => void;
}) {
  const [loading, setLoading] = useState(false);

  const generate = async () => {
    setLoading(true);
    try {
      const { data } = await api.post<DraftResponse>(`/api/ortho-cases/${caseId}/ai/clinical-draft`, {
        section: sectionFor(draftKind),
      });
      const generated = [
        data.modelId ? `AI draft (${data.modelId})` : "AI draft",
        data.draft,
        data.evidenceUsed?.length ? `Evidence: ${data.evidenceUsed.join(", ")}` : undefined,
        data.missingData?.length ? `Missing: ${data.missingData.join(", ")}` : undefined,
        data.warnings?.length ? `Warnings: ${data.warnings.join(", ")}` : undefined,
        data.disclaimer,
      ].filter(Boolean).join("\n\n");
      onDraft(currentDraft.trim() && currentDraft.trim() !== template.trim() ? `${currentDraft.trim()}\n\n---\n${generated}` : generated);
      toast.success("تم توليد مسودة الحالة");
    } catch (error) {
      const message = (error as { response?: { data?: { message?: string } } }).response?.data?.message ?? "تعذر توليد مسودة الحالة";
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <button
      type="button"
      onClick={generate}
      disabled={loading}
      className="inline-flex items-center gap-1 rounded-lg bg-clinic-blue px-2.5 py-1.5 text-[11px] font-medium text-white hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
    >
      {loading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
      اقتراح AI
    </button>
  );
}
