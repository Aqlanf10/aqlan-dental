"use client";

import { Suspense, useCallback, useRef, useState, type ReactNode } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { ArrowRight, ImagePlus, Loader2, Save, CheckCircle2, Clock, FileDown, Printer, GitCompareArrows } from "lucide-react";
import api from "@/lib/api";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { usePhotoAnalysisCase, type SavedPhotoAnalysis } from "@/hooks/usePhotoAnalysisCase";
import { downloadPdfFromApi, printPdfFromApi } from "@/lib/pdfDownload";
import { cn, formatArabicDate } from "@/lib/utils";

type Pt = { x: number; y: number };

const MAX_BYTES = 10 * 1024 * 1024;
const ACCEPTED = ["image/jpeg", "image/png", "image/webp"];

interface RenderArgs {
  imageUrl: string;
  initialPoints?: Record<string, Pt>;
  onChange: (data: { points: Record<string, Pt>; measurements: unknown }) => void;
}

interface Props {
  viewType: "profile" | "frontal";
  title: string;
  icon: ReactNode;
  uploadLabel: string;
  renderAnalyzer: (args: RenderArgs) => ReactNode;
}

function ShellInner({ viewType, title, icon, uploadLabel, renderAnalyzer }: Props) {
  const searchParams = useSearchParams();
  const orthoCaseId = searchParams.get("orthoCaseId");
  const returnHref = orthoCaseId ? `/ortho/${orthoCaseId}?tab=ceph` : "/ceph";
  const caseQuery = orthoCaseId ? `?orthoCaseId=${encodeURIComponent(orthoCaseId)}` : "";
  const profileHref = `/ceph/photo${caseQuery}`;
  const frontalHref = `/ceph/photo/frontal${caseQuery}`;
  const compareHref = orthoCaseId
    ? `/ceph/photo/compare?orthoCaseId=${encodeURIComponent(orthoCaseId)}&viewType=${viewType}`
    : null;

  const inputRef = useRef<HTMLInputElement>(null);
  const stateRef = useRef<{ points: Record<string, Pt>; measurements: unknown }>({ points: {}, measurements: [] });
  // JSON of the points as they were last saved/loaded — used to detect edits so
  // the PDF report (built from the persisted record) is never offered for a
  // dirty, unsaved state.
  const savedPointsRef = useRef<string>("");

  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [relativeUrl, setRelativeUrl] = useState<string | null>(null);
  const [initialPoints, setInitialPoints] = useState<Record<string, Pt> | undefined>(undefined);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedOk, setSavedOk] = useState(false);
  // Id of the currently saved/loaded analysis — enables the PDF report buttons.
  const [savedId, setSavedId] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);
  const [pdfBusy, setPdfBusy] = useState<"download" | "print" | null>(null);

  const { saved, saving, error: saveError, save } = usePhotoAnalysisCase(orthoCaseId, viewType);

  const onPick = async (file: File) => {
    setError(null);
    if (!ACCEPTED.includes(file.type)) { setError("نوع الصورة غير مدعوم — استخدم JPG أو PNG أو WEBP."); return; }
    if (file.size > MAX_BYTES) { setError("حجم الصورة يتجاوز 10 ميجابايت."); return; }
    setUploading(true);
    try {
      const form = new FormData();
      form.append("file", file);
      // The shared axios instance defaults to application/json, which makes
      // Axios serialize FormData to JSON — override so the file is sent as
      // multipart and UploadsController receives it.
      const { data } = await api.post<{ url: string }>("/api/uploads", form, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setRelativeUrl(data.url);
      setInitialPoints(undefined);
      setImageUrl(resolveImageUrl(data.url) || data.url);
      setSavedOk(false);
      setSavedId(null); // a fresh upload is an unsaved analysis
      savedPointsRef.current = "";
      setDirty(false);
    } catch (err) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(msg ?? "تعذّر رفع الصورة — حاول مرة أخرى.");
    } finally {
      setUploading(false);
    }
  };

  const loadSaved = async (item: SavedPhotoAnalysis) => {
    setError(null);
    setSavedOk(false);
    try {
      const { data } = await api.get<{ imageFileUrl: string; landmarksJson?: string }>(
        `/api/photo-analysis/${encodeURIComponent(item.id)}`,
      );
      let pts: Record<string, Pt> | undefined;
      if (data.landmarksJson) {
        try { pts = JSON.parse(data.landmarksJson); } catch { pts = undefined; }
      }
      setRelativeUrl(data.imageFileUrl);
      setInitialPoints(pts);
      setImageUrl(resolveImageUrl(data.imageFileUrl) || data.imageFileUrl);
      savedPointsRef.current = JSON.stringify(pts ?? {});
      setDirty(false);
      setSavedId(item.id);
    } catch {
      setError("تعذّر تحميل التحليل المحفوظ");
    }
  };

  const reportPdf = async (mode: "download" | "print") => {
    if (!savedId) return;
    setPdfBusy(mode);
    setError(null);
    try {
      const url = `/api/photo-analysis/${encodeURIComponent(savedId)}/report/pdf`;
      const filename = `photo-analysis-${savedId}.pdf`;
      if (mode === "download") await downloadPdfFromApi(url, filename);
      else await printPdfFromApi(url, filename);
    } catch {
      setError("تعذّر إنشاء تقرير PDF — احفظ التحليل أولًا ثم حاول.");
    } finally {
      setPdfBusy(null);
    }
  };

  const onChange = useCallback((data: { points: Record<string, Pt>; measurements: unknown }) => {
    stateRef.current = data;
    // An edit relative to the saved/loaded baseline marks the analysis dirty,
    // hiding the PDF actions (the report is built from the persisted record).
    const isDirty = JSON.stringify(data.points) !== savedPointsRef.current;
    setDirty(isDirty);
    if (isDirty) setSavedOk(false);
  }, []);

  const handleSave = async () => {
    if (!relativeUrl) return;
    const id = await save(relativeUrl, stateRef.current.points, stateRef.current.measurements);
    if (id) {
      setSavedOk(true);
      setSavedId(id);
      savedPointsRef.current = JSON.stringify(stateRef.current.points);
      setDirty(false);
    }
  };

  return (
    <div className="space-y-5 max-w-5xl">
      <div className="no-print flex items-center gap-2 text-sm text-gray-500">
        <Link href={returnHref} className="hover:text-clinic-blue transition">
          {orthoCaseId ? "حالة التقويم" : "السيفالومتري"}
        </Link>
        <span>/</span>
        <span className="text-gray-900 font-medium">{title}</span>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <Link href={returnHref}
            className="no-print p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <h1 className="text-2xl font-extrabold text-gray-900 flex items-center gap-2">{icon}{title}</h1>
        </div>
        <div className="flex items-center gap-2">
          {imageUrl && orthoCaseId && (
            <button
              onClick={handleSave}
              disabled={saving}
              className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60 transition">
              {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" />
                : savedOk ? <CheckCircle2 className="w-3.5 h-3.5" /> : <Save className="w-3.5 h-3.5" />}
              {savedOk ? "تم الحفظ" : "حفظ في الحالة"}
            </button>
          )}
          {savedId && !dirty && (
            <>
              <button onClick={() => reportPdf("download")} disabled={pdfBusy !== null}
                title="تحميل تقرير PDF للتحليل المحفوظ"
                className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-60 transition">
                {pdfBusy === "download" ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileDown className="w-3.5 h-3.5" />}
                تقرير PDF
              </button>
              <button onClick={() => reportPdf("print")} disabled={pdfBusy !== null}
                title="طباعة تقرير التحليل"
                className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 disabled:opacity-60 transition">
                {pdfBusy === "print" ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Printer className="w-3.5 h-3.5" />}
                طباعة
              </button>
            </>
          )}
          {imageUrl && (
            <button onClick={() => inputRef.current?.click()}
              className="no-print flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition">
              <ImagePlus className="w-3.5 h-3.5" />صورة أخرى
            </button>
          )}
        </div>
      </div>

      <nav aria-label="أنواع تحليل الصور" className="no-print flex flex-wrap gap-2 border border-gray-200 bg-white p-2">
        <Link href={profileHref} aria-current={viewType === "profile" ? "page" : undefined}
          className={cn("border px-3 py-2 text-xs font-bold", viewType === "profile" ? "border-blue-300 bg-blue-50 text-clinic-blue" : "border-gray-200 text-gray-700")}>
          تحليل البروفايل
        </Link>
        <Link href={frontalHref} aria-current={viewType === "frontal" ? "page" : undefined}
          className={cn("border px-3 py-2 text-xs font-bold", viewType === "frontal" ? "border-blue-300 bg-blue-50 text-clinic-blue" : "border-gray-200 text-gray-700")}>
          التحليل الأمامي
        </Link>
        {compareHref && saved.length >= 2 && (
          <Link href={compareHref} className="inline-flex items-center gap-1.5 border border-gray-200 px-3 py-2 text-xs font-bold text-gray-700 hover:border-blue-300 hover:text-clinic-blue">
            <GitCompareArrows className="h-4 w-4" />مقارنة قبل وبعد
          </Link>
        )}
      </nav>

      <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden"
        onChange={(e) => { const f = e.target.files?.[0]; if (f) onPick(f); e.target.value = ""; }} />

      {(error || saveError) && (
        <div className="bg-red-50 border border-red-200 text-red-700 rounded-lg p-2.5 text-xs">{error ?? saveError}</div>
      )}

      {orthoCaseId && saved.length > 0 && (
        <div className="bg-white border border-gray-200 rounded-xl p-3">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <p className="text-xs font-bold text-gray-600">تحاليل محفوظة لهذه الحالة</p>
            {compareHref && saved.length >= 2 && <Link href={compareHref} className="inline-flex items-center gap-1 text-[11px] font-bold text-clinic-blue"><GitCompareArrows className="h-3.5 w-3.5" />مقارنة طولية</Link>}
          </div>
          <div className="flex flex-wrap gap-2">
            {saved.map((s) => (
              <button key={s.id} onClick={() => loadSaved(s)}
                className="flex items-center gap-1.5 px-2.5 py-1 rounded-lg border border-gray-200 text-[11px] text-gray-600 hover:border-clinic-blue hover:text-clinic-blue transition">
                <Clock className="w-3 h-3" />{formatArabicDate(s.analysisDate)}
              </button>
            ))}
          </div>
        </div>
      )}

      {!imageUrl ? (
        <button onClick={() => inputRef.current?.click()} disabled={uploading}
          className="w-full border-2 border-dashed border-gray-300 rounded-xl py-16 flex flex-col items-center gap-3 text-gray-400 hover:border-clinic-blue hover:text-clinic-blue transition disabled:opacity-60">
          {uploading ? <Loader2 className="w-8 h-8 animate-spin" /> : <ImagePlus className="w-8 h-8" />}
          <span className="text-sm font-medium">{uploading ? "جارٍ رفع الصورة..." : uploadLabel}</span>
          <span className="text-[11px] text-gray-300">القياسات مستقلة عن المقياس لا تتطلب معايرة</span>
        </button>
      ) : (
        <div className={cn("space-y-2")}>
          {renderAnalyzer({ imageUrl, initialPoints, onChange })}
        </div>
      )}
    </div>
  );
}

export function PhotoAnalysisShell(props: Props) {
  return (
    <Suspense fallback={<div className="h-64 animate-pulse rounded-xl bg-gray-100" />}>
      <ShellInner {...props} />
    </Suspense>
  );
}
