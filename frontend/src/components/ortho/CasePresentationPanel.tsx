"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Presentation, CheckCircle2, Circle, FileDown, Loader2, Sparkles, ListOrdered } from "lucide-react";
import api from "@/lib/api";
import { useOrthoOverview, useOrthoPhotos } from "@/hooks/useOrtho";
import { orthoService } from "@/services/orthoService";
import { downloadPdfFromApi, extractPdfError } from "@/lib/pdfDownload";
import { cn } from "@/lib/utils";

interface Checklist {
  extraoralFrontal?: boolean; extraoralProfile?: boolean; extraoralSmile?: boolean;
  intraoralFrontal?: boolean; intraoralRight?: boolean; intraoralLeft?: boolean;
  upperOcclusal?: boolean; lowerOcclusal?: boolean; opg?: boolean; lateralCeph?: boolean; studyModels?: boolean;
}

/** Reports and case-presentation workspace: readiness, PDF reports, and PPTX export. */
export function CasePresentationPanel({ caseId }: { caseId: string }) {
  const [busy, setBusy] = useState<string | null>(null);
  const [presentationError, setPresentationError] = useState<string | null>(null);
  const [includeEmpty, setIncludeEmpty] = useState(true);
  const [showSlides, setShowSlides] = useState(false);

  const overviewQ = useOrthoOverview(caseId);
  const photosQ = useOrthoPhotos(caseId);
  const definitionQ = useQuery({
    queryKey: ["ortho-presentation-definition", caseId],
    enabled: !!caseId, retry: false,
    queryFn: async () => (await orthoService.getCasePresentationDefinition(caseId)).data,
  });
  const checklistQ = useQuery({
    queryKey: ["ortho-checklist", caseId],
    enabled: !!caseId, retry: false,
    queryFn: async () => (await api.get<Checklist>(`/api/ortho-cases/${caseId}/checklist`)).data,
  });
  const modelQ = useQuery({
    queryKey: ["ortho-model-latest", caseId],
    enabled: !!caseId, retry: false,
    queryFn: async () => {
      try { return (await api.get<{ id: string } | null>(`/api/ortho-cases/${caseId}/model-analyses/latest`)).data; }
      catch { return null; }
    },
  });

  const o = (overviewQ.data ?? {}) as Record<string, unknown>;
  const c = checklistQ.data ?? {};
  const bool = (v: unknown) => v === true;
  const num = (v: unknown) => (typeof v === "number" ? v : 0);
  const selectedPhotos = (photosQ.data ?? []).filter((photo) => photo.isSelectedForReport);
  const preparedSelectedPhotos = selectedPhotos.filter((photo) => photo.isPreparedForReport);

  const items: { label: string; ready: boolean; note?: string }[] = [
    { label: "صور خارج الفم", ready: bool(c.extraoralFrontal) && bool(c.extraoralProfile) && bool(c.extraoralSmile) },
    { label: "صور داخل الفم", ready: bool(c.intraoralFrontal) && bool(c.intraoralRight) && bool(c.intraoralLeft) && bool(c.upperOcclusal) && bool(c.lowerOcclusal) },
    { label: "أشعة بانوراما", ready: bool(c.opg) },
    { label: "أشعة سيفالو", ready: bool(c.lateralCeph) || num(o.cephAnalysesCount) > 0 },
    { label: "تحليل النماذج / Bolton", ready: Boolean(modelQ.data) },
    { label: "تشخيص معتمد", ready: bool(o.isDiagnosisApproved) },
    { label: "خطة علاج معتمدة", ready: bool(o.isTreatmentPlanApproved) },
    { label: "زيارات / صور تقدّم", ready: num(o.visitsCount) > 0 },
    { label: "خطة الاحتفاظ", ready: bool(o.hasRetention) },
    {
      label: "الصور المختارة مجهزة للعرض",
      ready: selectedPhotos.length > 0 && preparedSelectedPhotos.length === selectedPhotos.length,
      note: `${preparedSelectedPhotos.length}/${selectedPhotos.length}`,
    },
  ];
  const readyCount = items.filter((i) => i.ready).length;
  const latestCephId = o.latestCephAnalysisId as string | undefined;
  const hasModel = Boolean(modelQ.data);

  const download = async (key: string, url: string, filename: string) => {
    setBusy(key);
    try { await downloadPdfFromApi(url, filename); }
    catch { /* download helper surfaces errors; allow retry */ }
    finally { setBusy(null); }
  };

  const downloadPresentation = async () => {
    setBusy("pptx");
    setPresentationError(null);
    try {
      const response = await api.post(
        `/api/ortho-cases/${caseId}/case-presentation/pptx`,
        { includeEmptyOptionalSlides: includeEmpty },
        { responseType: "blob" }
      );
      const blob = new Blob(
        [response.data],
        { type: "application/vnd.openxmlformats-officedocument.presentationml.presentation" }
      );
      const objectUrl = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = objectUrl;
      link.download = `ortho-case-${caseId}.pptx`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(objectUrl);
    } catch (error) {
      setPresentationError(await extractPdfError(error));
    } finally {
      setBusy(null);
    }
  };

  const reports: { key: string; label: string; url: string; filename: string; enabled: boolean }[] = [
    { key: "summary", label: "ملخّص الحالة PDF", url: `/api/ortho-cases/${caseId}/case-summary/report/pdf`, filename: `case-summary-${caseId}.pdf`, enabled: true },
    { key: "model", label: "تحليل النماذج PDF", url: `/api/ortho-cases/${caseId}/model-analyses/latest/report/pdf`, filename: `model-analysis-${caseId}.pdf`, enabled: hasModel },
    { key: "ceph", label: "تقرير السيفالو PDF", url: `/api/ceph/${latestCephId}/report/pdf`, filename: `ceph-${latestCephId}.pdf`, enabled: Boolean(latestCephId) },
  ];

  const loading = overviewQ.isLoading || checklistQ.isLoading || photosQ.isLoading;

  return (
    <div className="space-y-5" dir="rtl">
      <div className="flex items-center gap-2">
        <Presentation className="h-5 w-5 text-clinic-blue" />
        <h3 className="text-sm font-bold text-clinic-navy">عرض الحالة والتقارير</h3>
      </div>

      {/* Readiness checklist */}
      <div className="rounded-lg border border-gray-200 p-4">
        <div className="mb-3 flex items-center justify-between">
          <h4 className="text-xs font-semibold text-gray-600">جاهزية عرض الحالة</h4>
          <span className={cn("rounded-full px-2 py-0.5 text-[11px] font-bold",
            readyCount === items.length ? "bg-green-50 text-green-700" : "bg-amber-50 text-amber-700")}>
            {readyCount}/{items.length} جاهز
          </span>
        </div>
        {loading ? (
          <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
        ) : (
          <ul className="grid gap-1.5 sm:grid-cols-2">
            {items.map((i) => (
              <li key={i.label} className="flex items-center gap-2 text-sm">
                {i.ready ? <CheckCircle2 className="h-4 w-4 text-green-500" /> : <Circle className="h-4 w-4 text-gray-300" />}
                <span className={i.ready ? "text-gray-800" : "text-gray-400"}>{i.label}</span>
                {i.note && <span className="text-[11px] text-gray-400">{i.note}</span>}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Ready reports */}
      <div className="rounded-lg border border-gray-200 p-4 space-y-2">
        <h4 className="text-xs font-semibold text-gray-600">التقارير الجاهزة (PDF)</h4>
        <div className="flex flex-wrap gap-2">
          {reports.map((r) => (
            <button key={r.key} onClick={() => download(r.key, r.url, r.filename)}
              disabled={!r.enabled || busy !== null}
              title={r.enabled ? undefined : "غير متاح بعد لهذه الحالة"}
              className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50">
              {busy === r.key ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileDown className="h-3.5 w-3.5" />}
              {r.label}
            </button>
          ))}
        </div>
        <span className="text-[11px] text-gray-400">تقارير تحليل الصور متاحة في تبويب «تحليل الصور».</span>
      </div>

      {/* PPTX generation */}
      <div className="rounded-lg border border-clinic-blue/30 bg-clinic-blue-50/40 p-4">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <Sparkles className="h-4 w-4 text-clinic-blue" />
            <span className="text-sm font-medium text-clinic-navy">إنشاء عرض الحالة (PowerPoint)</span>
          </div>
          <button
            type="button"
            onClick={downloadPresentation}
            disabled={busy !== null}
            className="inline-flex items-center gap-1.5 rounded-lg bg-clinic-blue px-3 py-1.5 text-xs font-medium text-white hover:bg-clinic-blue-600 disabled:cursor-not-allowed disabled:opacity-60">
            {busy === "pptx" ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <FileDown className="h-3.5 w-3.5" />}
            تنزيل PowerPoint
          </button>
        </div>
        <p className="mt-2 text-[11px] text-gray-500">
          يولّد ملفًا من بيانات الحالة الحالية والصور المختارة، ويستخدم الصور المجهزة عند توفرها دون تمديد أو تشويه.
        </p>

        <label className="mt-3 flex items-center gap-2 text-xs text-gray-600">
          <input type="checkbox" checked={includeEmpty}
            onChange={(e) => setIncludeEmpty(e.target.checked)}
            className="h-3.5 w-3.5 accent-clinic-blue" />
          تضمين الشرائح الفارغة (الأقسام بلا بيانات بعد)
        </label>

        {definitionQ.data && (() => {
          // Reflect exactly what will be in the downloaded file: when "include empty"
          // is off the generator drops optional slides without data, so the count and
          // per-slide numbering here must match that included subset.
          let position = 0;
          const rows = definitionQ.data.slides.map((s) => {
            const included = includeEmpty || s.required || s.hasData;
            if (included) position += 1;
            return { s, included, number: included ? position : null };
          });
          const includedCount = rows.filter((r) => r.included).length;
          const readyCount = definitionQ.data.slides.filter((s) => s.hasData).length;
          return (
            <div className="mt-3">
              <button type="button" onClick={() => setShowSlides((v) => !v)}
                className="inline-flex items-center gap-1.5 text-xs font-medium text-clinic-blue hover:underline">
                <ListOrdered className="h-3.5 w-3.5" />
                {showSlides ? "إخفاء" : "عرض"} محتوى العرض ({includedCount} شريحة · {readyCount} جاهزة)
              </button>
              {showSlides && (
                <ol className="mt-2 grid gap-1 sm:grid-cols-2">
                  {rows.map(({ s, included, number }, i) => (
                    <li key={`${s.type}-${i}`}
                      className={cn("flex items-center gap-2 rounded px-2 py-1 text-[11px]",
                        included ? "bg-white" : "bg-gray-50 opacity-50")}>
                      <span className="w-5 text-gray-400 tabular-nums">{number ? `${number}.` : "—"}</span>
                      {s.hasData
                        ? <CheckCircle2 className="h-3.5 w-3.5 shrink-0 text-green-500" />
                        : <Circle className="h-3.5 w-3.5 shrink-0 text-gray-300" />}
                      <span className={s.hasData ? "text-gray-800" : "text-gray-400"}>{s.title}</span>
                    </li>
                  ))}
                </ol>
              )}
            </div>
          );
        })()}

        {presentationError && (
          <p role="alert" className="mt-2 text-xs font-medium text-red-600">
            {presentationError}
          </p>
        )}
      </div>
    </div>
  );
}
