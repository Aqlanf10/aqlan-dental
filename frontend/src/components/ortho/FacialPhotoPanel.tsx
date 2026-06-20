"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { UserSquare2, Smile, FileDown, Loader2, Clock } from "lucide-react";
import api from "@/lib/api";
import { downloadPdfFromApi } from "@/lib/pdfDownload";
import { formatArabicDate } from "@/lib/utils";

interface SavedPhotoAnalysis {
  id: string;
  viewType: string;
  imageFileUrl: string;
  analysisDate: string;
}

/** Surfaces the existing facial photo analyses (#371/#376/#377) inside the case
 *  workspace: list saved analyses, open the report PDF, and create new ones. */
export function FacialPhotoPanel({ caseId }: { caseId: string }) {
  const [pdfBusy, setPdfBusy] = useState<string | null>(null);
  const { data, isLoading } = useQuery({
    queryKey: ["ortho-photo-analyses", caseId],
    enabled: !!caseId,
    retry: false,
    queryFn: async () =>
      (await api.get<SavedPhotoAnalysis[]>(`/api/photo-analysis?orthoCaseId=${encodeURIComponent(caseId)}`)).data,
  });

  const profile = (data ?? []).filter((a) => a.viewType === "profile");
  const frontal = (data ?? []).filter((a) => a.viewType === "frontal");

  const reportPdf = async (id: string) => {
    setPdfBusy(id);
    try {
      await downloadPdfFromApi(`/api/photo-analysis/${id}/report/pdf`, `photo-analysis-${id}.pdf`);
    } catch { /* ignore — user can retry */ }
    finally { setPdfBusy(null); }
  };

  const Row = ({ a }: { a: SavedPhotoAnalysis }) => (
    <div className="flex items-center justify-between gap-2 rounded-lg border border-gray-100 px-3 py-2 text-sm">
      <span className="inline-flex items-center gap-1.5 text-gray-600">
        <Clock className="h-3.5 w-3.5 text-gray-400" />{formatArabicDate(a.analysisDate)}
      </span>
      <button onClick={() => reportPdf(a.id)} disabled={pdfBusy === a.id}
        className="inline-flex items-center gap-1 rounded border border-gray-200 px-2 py-1 text-[11px] text-gray-700 hover:bg-gray-50 disabled:opacity-60">
        {pdfBusy === a.id ? <Loader2 className="h-3 w-3 animate-spin" /> : <FileDown className="h-3 w-3" />}تقرير PDF
      </button>
    </div>
  );

  return (
    <div className="space-y-5">
      <div className="grid gap-4 md:grid-cols-2">
        {/* Profile */}
        <div className="rounded-lg border border-gray-200 p-4">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="flex items-center gap-2 text-sm font-bold text-clinic-navy">
              <UserSquare2 className="h-4 w-4 text-clinic-blue" />تحليل البروفايل
            </h3>
            <Link href={`/ceph/photo?orthoCaseId=${caseId}`}
              className="rounded-lg bg-clinic-blue px-2.5 py-1 text-[11px] font-medium text-white hover:opacity-90">
              تحليل جديد
            </Link>
          </div>
          {isLoading ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
            : profile.length === 0 ? <p className="py-4 text-center text-xs text-gray-400">لا تحاليل بروفايل بعد</p>
            : <div className="space-y-1.5">{profile.map((a) => <Row key={a.id} a={a} />)}</div>}
        </div>

        {/* Frontal */}
        <div className="rounded-lg border border-gray-200 p-4">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="flex items-center gap-2 text-sm font-bold text-clinic-navy">
              <Smile className="h-4 w-4 text-clinic-blue" />تحليل الصورة الأمامية
            </h3>
            <Link href={`/ceph/photo/frontal?orthoCaseId=${caseId}`}
              className="rounded-lg bg-clinic-blue px-2.5 py-1 text-[11px] font-medium text-white hover:opacity-90">
              تحليل جديد
            </Link>
          </div>
          {isLoading ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
            : frontal.length === 0 ? <p className="py-4 text-center text-xs text-gray-400">لا تحاليل أمامية بعد</p>
            : <div className="space-y-1.5">{frontal.map((a) => <Row key={a.id} a={a} />)}</div>}
        </div>
      </div>
      <p className="text-[11px] text-gray-400">
        تُحفظ تحاليل الصور في الحالة وتُزامَن نتائجها تلقائيًا إلى التشخيص (الأنسجة الرخوة).
      </p>
    </div>
  );
}
