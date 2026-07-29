"use client";

import { useState } from "react";
import { AlertTriangle, CheckCircle2, FileUp, Loader2, X } from "lucide-react";
import api from "@/lib/api";
import type { CephAnalysis, CephLandmark } from "@/types/ceph";

export interface WebCephImportSummary {
  parsed: number;
  imported: number;
  replaced: number;
  skippedExisting: number;
  skippedOutOfBounds: number;
  recordingDate?: string | null;
  skippedNames: string[];
}

interface Props {
  analysisId: string;
  pixelsPerMm: number | null;
  imageWidth: number;
  imageHeight: number;
  sLandmark?: CephLandmark;
  onImported: (analysis: CephAnalysis, summary: WebCephImportSummary) => void;
}

export function CephWebCephImportDialog({
  analysisId,
  pixelsPerMm,
  imageWidth,
  imageHeight,
  sLandmark,
  onImported,
}: Props) {
  const [open, setOpen] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [replaceExisting, setReplaceExisting] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [summary, setSummary] = useState<WebCephImportSummary | null>(null);

  const ready = Boolean(
    pixelsPerMm && pixelsPerMm > 0 && sLandmark && imageWidth > 0 && imageHeight > 0,
  );

  const submit = async () => {
    if (!file || !ready || !pixelsPerMm || !sLandmark) return;
    setSubmitting(true);
    setError(null);
    setSummary(null);

    const form = new FormData();
    form.append("file", file);
    form.append("pixelsPerMm", String(pixelsPerMm));
    form.append("anchorX", String(sLandmark.x));
    form.append("anchorY", String(sLandmark.y));
    form.append("imageWidth", String(imageWidth));
    form.append("imageHeight", String(imageHeight));
    form.append("replaceExisting", String(replaceExisting));

    try {
      const response = await api.post<{
        analysis: CephAnalysis;
        summary: WebCephImportSummary;
      }>(`/api/ceph/${analysisId}/imports/webceph-landmarks`, form);
      setSummary(response.data.summary);
      onImported(response.data.analysis, response.data.summary);
    } catch (requestError) {
      const message = (requestError as { response?: { data?: { message?: string } } })
        .response?.data?.message;
      setError(message ?? "تعذر استيراد جدول نقاط WebCeph");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <button
        type="button"
        onClick={() => {
          setOpen(true);
          setError(null);
          setSummary(null);
        }}
        title="استيراد Landmark Table المصدّر من WebCeph"
        className="flex items-center gap-1.5 rounded-md border border-sky-300 bg-sky-50 px-2.5 py-1.5 text-xs font-medium text-sky-800 transition hover:bg-sky-100"
      >
        <FileUp className="h-3.5 w-3.5" />
        استيراد WebCeph
      </button>

      {open && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4" dir="rtl">
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby="webceph-import-title"
            className="w-full max-w-lg rounded-md bg-white shadow-xl"
          >
            <div className="flex items-center justify-between border-b border-gray-200 px-4 py-3">
              <h2 id="webceph-import-title" className="text-sm font-bold text-gray-900">
                استيراد جدول نقاط WebCeph
              </h2>
              <button
                type="button"
                onClick={() => setOpen(false)}
                title="إغلاق"
                className="rounded p-1 text-gray-500 hover:bg-gray-100"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <div className="space-y-4 px-4 py-4">
              {!ready && (
                <div className="flex gap-2 rounded-md border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                  <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
                  <span>احفظ معايرة الصورة وحدد نقطة S أولًا، ثم أعد فتح الاستيراد.</span>
                </div>
              )}

              <label className="block text-xs font-medium text-gray-700">
                ملف Landmark Table
                <input
                  type="file"
                  accept=".xls,.html,.htm"
                  onChange={(event) => {
                    setFile(event.target.files?.[0] ?? null);
                    setError(null);
                    setSummary(null);
                  }}
                  className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-xs file:me-3 file:rounded file:border-0 file:bg-sky-50 file:px-3 file:py-1.5 file:text-sky-800"
                />
              </label>

              <label className="flex items-center gap-2 text-xs text-gray-700">
                <input
                  type="checkbox"
                  checked={replaceExisting}
                  onChange={(event) => setReplaceExisting(event.target.checked)}
                  className="h-4 w-4 rounded border-gray-300"
                />
                استبدال النقاط الحالية عند تطابق المفتاح التشريحي
              </label>

              {error && (
                <div className="rounded-md border border-red-200 bg-red-50 p-3 text-xs text-red-800">
                  {error}
                </div>
              )}

              {summary && (
                <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-900">
                  <div className="flex items-center gap-2 font-bold">
                    <CheckCircle2 className="h-4 w-4" />
                    تم استيراد {summary.imported} من {summary.parsed} نقطة
                  </div>
                  <p className="mt-1">
                    استُبدلت {summary.replaced}، وتُركت {summary.skippedExisting} نقطة موجودة،
                    ورُفضت {summary.skippedOutOfBounds} خارج حدود الصورة.
                  </p>
                  <p className="mt-1 font-medium">راجع النقاط المستوردة يدويًا قبل اعتماد التحليل.</p>
                </div>
              )}
            </div>

            <div className="flex justify-end gap-2 border-t border-gray-200 px-4 py-3">
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded-md border border-gray-300 px-3 py-1.5 text-xs text-gray-700 hover:bg-gray-50"
              >
                إغلاق
              </button>
              <button
                type="button"
                onClick={submit}
                disabled={!file || !ready || submitting}
                className="flex items-center gap-1.5 rounded-md bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50"
              >
                {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileUp className="h-3.5 w-3.5" />}
                {submitting ? "جارٍ الاستيراد..." : "استيراد النقاط"}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
