"use client";
import { useEffect, useState, useMemo, useCallback } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  Brain, Calculator, Eye, EyeOff, Play, PlayCircle, ArrowRight,
  Save, CheckCircle2, ChevronRight, Loader2, FileDown, Printer,
  Sun, Contrast, RotateCcw, ListChecks, ImageIcon, FileText, ScanLine, Target,
} from "lucide-react";
import type { CephAnalysis, CephLandmark, CephDiagnosis, AnalysisType } from "@/types/ceph";
import { ANALYSIS_GROUPS, ANALYSIS_TYPE_AR } from "@/types/ceph";
import { buildMeasurementList, applyNormOverrides, type ApiNorm } from "@/lib/cephMath";
import { CephCanvas, LANDMARK_DEFS, LANDMARK_ORDER, SIMULATION_SCENARIOS } from "@/components/ceph/CephCanvas";
import { AnalysisReport } from "@/components/ceph/AnalysisReport";
import { CephReadinessBadge } from "@/components/ceph/CephReadinessBadge";
import { cephReadinessFromAnalysis } from "@/lib/cephReadiness";
import api from "@/lib/api";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { downloadPdfFromApi, printPdfFromApi } from "@/lib/pdfDownload";
import { cn, formatArabicDate } from "@/lib/utils";

const LANDMARK_GROUPS = [
  { key: 'cranial',  label: 'قاعدة الجمجمة',  keys: ['S', 'N', 'Or', 'Po'] },
  { key: 'maxilla',  label: 'الفك العلوي',     keys: ['ANS', 'PNS', 'A'] },
  { key: 'mandible', label: 'الفك السفلي',     keys: ['B', 'Pog', 'Gn', 'Me', 'Go', 'Co', 'Ar', 'D', 'Pm'] },
  { key: 'dental',   label: 'الأسنان',         keys: ['U1T', 'U1A', 'L1T', 'L1A'] },
  { key: 'soft',     label: 'الأنسجة الرخوة',  keys: ['LS', 'LI', 'Pn', 'Cm'] },
];

type RightTab = 'report' | 'diagnosis';

export default function CephAnalysisPage() {
  const { id } = useParams<{ id: string }>();
  const [analysis, setAnalysis]       = useState<CephAnalysis | null>(null);
  const [loading, setLoading]         = useState(true);
  const [landmarks, setLandmarks]     = useState<CephLandmark[]>([]);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [showPlanes, setShowPlanes]   = useState(true);
  const [showTracing, setShowTracing] = useState(true);
  const [showSim, setShowSim]         = useState(false);
  const [simScenario, setSimScenario] = useState(Object.keys(SIMULATION_SCENARIOS)[0]);
  const [pixelsPerMm, setPixelsPerMm] = useState<number | null>(null);
  const [isDirty, setIsDirty]         = useState(false);
  const [saving, setSaving]           = useState(false);
  const [detecting, setDetecting]     = useState(false);
  const [aiTracing, setAiTracing]     = useState(false);
  const [saveStatus, setSaveStatus]   = useState<'idle' | 'saved' | 'error'>('idle');
  const [rightTab, setRightTab]       = useState<RightTab>('report');
  const [diagnosis, setDiagnosis]     = useState<CephDiagnosis | null>(null);
  const [imageSize, setImageSize]     = useState({ w: 800, h: 600 });
  const [brightness, setBrightness]   = useState(100);
  const [contrast, setContrast]       = useState(100);
  const [inverted, setInverted]       = useState(false);
  const [showMeasurements, setShowMeasurements] = useState(true);
  // Configurable norms from DB (overlaid on built-ins; built-ins are the fallback).
  const [normsVersion, setNormsVersion] = useState(0);
  // Honest-simulation state: the template tool is NOT AI and must say so.
  const [simNotice, setSimNotice]     = useState<string | null>(null);
  const [simError, setSimError]       = useState<string | null>(null);
  const [aiTraceNotice, setAiTraceNotice] = useState<string | null>(null);
  const [aiTraceError, setAiTraceError] = useState<string | null>(null);
  // C-C: Arabic ceph PDF report (download / print)
  const [pdfBusy, setPdfBusy]         = useState<'download' | 'print' | null>(null);
  const [pdfError, setPdfError]       = useState<string | null>(null);

  useEffect(() => {
    api.get<ApiNorm[]>("/api/ceph-norms")
      .then((r) => {
        if (Array.isArray(r.data) && r.data.length) {
          applyNormOverrides(r.data);
          setNormsVersion((v) => v + 1);
        }
      })
      .catch(() => { /* keep built-in norms silently */ });
  }, []);

  useEffect(() => {
    api.get<CephAnalysis>(`/api/ceph/${id}`)
      .then(r => {
        setAnalysis(r.data);
        setLandmarks(r.data.landmarks ?? []);
        setDiagnosis(r.data.diagnosis ?? null);
        const ppm = r.data.pixelsPerMm;
        setPixelsPerMm(ppm && ppm > 0 ? ppm : null);
        if (r.data.imageWidth && r.data.imageHeight)
          setImageSize({ w: r.data.imageWidth, h: r.data.imageHeight });
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [id]);

  const lmMap = useMemo(() => {
    const m: Record<string, CephLandmark> = {};
    landmarks.forEach(l => { m[l.key] = l; });
    return m;
  }, [landmarks]);

  const analysisGroups = useMemo(() => {
    if (!analysis) return ['steiner','tweed','mcnamara','ricketts','downs'] as const;
    return ANALYSIS_GROUPS[analysis.analysisType as AnalysisType] ?? ANALYSIS_GROUPS['full'];
  }, [analysis]);

  // Real-time measurements (live as user places landmarks)
  const computedMeasurements = useMemo(() => {
    // normsVersion is read to force recompute when DB norm overrides arrive
    // (buildMeasurementList reads module-level norm tables mutated by
    // applyNormOverrides, invisible to the dependency analysis).
    void normsVersion;
    const pts: Record<string, { x: number; y: number }> = {};
    landmarks.forEach(l => { pts[l.key] = { x: l.x, y: l.y }; });
    return buildMeasurementList(pts, pixelsPerMm, analysisGroups);
  }, [landmarks, pixelsPerMm, analysisGroups, normsVersion]);

  const activeReportData = (analysis?.measurements?.length && !isDirty)
    ? analysis.measurements
    : computedMeasurements;

  const placedCount = landmarks.length;
  const totalCount  = LANDMARK_ORDER.length;

  // Readiness of the SAVED record (image/calibration/points/measurements) plus
  // the live unsaved-edits flag — the same gate the PDF/VTO buttons enforce.
  const readiness = useMemo(
    () => analysis ? cephReadinessFromAnalysis(analysis, isDirty) : null,
    [analysis, isDirty],
  );

  const handleLandmarksChange = useCallback((lm: CephLandmark[]) => {
    setLandmarks(lm);
    setIsDirty(true);
  }, []);

  // Calibration edits (ruler apply or manual px/mm input) are unsaved changes
  // too: the report/VTO read the saved record, so dirty them until "حفظ وحساب".
  const handleCalibrationChange = useCallback((value: number | null) => {
    setPixelsPerMm(value);
    setIsDirty(true);
  }, []);

  const handleImageDimensions = useCallback((width: number, height: number) => {
    setImageSize(current =>
      current.w === width && current.h === height ? current : { w: width, h: height });
  }, []);

  const handleTemplateSimulation = async () => {
    setDetecting(true);
    setSimError(null);
    try {
      const res = await api.post<{ isSimulation: boolean; simulationNotice: string; landmarks: CephLandmark[] } | CephLandmark[]>(
        `/api/ceph/${id}/simulate`,
        { imageWidth: imageSize.w, imageHeight: imageSize.h, pixelsPerMm: pixelsPerMm ?? 0 },
      );
      const data = res.data;
      const lms = Array.isArray(data) ? data : data.landmarks;
      setLandmarks(lms ?? []);
      setSimNotice(
        !Array.isArray(data) && data.simulationNotice
          ? data.simulationNotice
          : "مواضع المعالم الحالية ناتجة عن محاكاة تجريبية وليست ذكاءً اصطناعيًا — يجب ضبط كل معلم يدويًا قبل الاعتماد.",
      );
      setIsDirty(true);
      setSaveStatus('idle');
    } catch (err) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setSimError(msg ?? "تعذر تشغيل المحاكاة التجريبية");
      setSimNotice(null);
    } finally { setDetecting(false); }
  };

  const handleAiTrace = async () => {
    setAiTracing(true);
    setAiTraceError(null);
    setAiTraceNotice(null);
    try {
      const { data } = await api.post<{
        landmarks: CephLandmark[];
        modelId: string;
        disclaimer: string;
        generatedAt: string;
      }>(`/api/ceph/${id}/ai/auto-trace`, {
        imageWidth: imageSize.w,
        imageHeight: imageSize.h,
      });

      const drafted = (data.landmarks ?? []).map((landmark) => ({
        ...landmark,
        name: landmark.name || LANDMARK_DEFS[landmark.key]?.nameAr || landmark.key,
        nameAr: LANDMARK_DEFS[landmark.key]?.nameAr || landmark.name || landmark.key,
        group: LANDMARK_DEFS[landmark.key]?.group as CephLandmark["group"],
        isAiPlaced: true,
      }));
      setLandmarks(drafted);
      setIsDirty(true);
      setSaveStatus("idle");
      setSelectedKey(drafted[0]?.key ?? null);
      setAiTraceNotice(`${data.disclaimer} النموذج: ${data.modelId}`);
    } catch (error) {
      const message = (error as { response?: { data?: { message?: string } } })
        .response?.data?.message;
      setAiTraceError(message ?? "تعذر توليد مسودة نقاط السيفالومتري");
    } finally {
      setAiTracing(false);
    }
  };

  const handleSaveAndCompute = async () => {
    setSaving(true);
    setSaveStatus('idle');
    try {
      const res = await api.post<CephAnalysis>(`/api/ceph/${id}/landmarks`, {
        landmarks: landmarks.map(l => ({
          key:        l.key,
          name:       l.name,
          nameAr:     l.nameAr,
          group:      l.group ?? LANDMARK_DEFS[l.key]?.group,
          x:          l.x,
          y:          l.y,
          isAiPlaced: l.isAiPlaced,
          confidence: l.confidence,
        })),
        pixelsPerMm: pixelsPerMm ?? 0,
        imageWidth:  imageSize.w,
        imageHeight: imageSize.h,
      });
      setAnalysis(res.data);
      setDiagnosis(res.data.diagnosis ?? null);
      // Re-sync calibration from the persisted record so the readiness badge
      // and gates reflect exactly what was saved (no false "unsaved" drift).
      const savedPpm = res.data.pixelsPerMm;
      setPixelsPerMm(savedPpm && savedPpm > 0 ? savedPpm : null);
      setIsDirty(false);
      setSaveStatus('saved');
      setRightTab('report');
      setTimeout(() => setSaveStatus('idle'), 3000);
    } catch {
      setSaveStatus('error');
    } finally {
      setSaving(false);
    }
  };

  const handleReportPdf = async (mode: 'download' | 'print') => {
    setPdfBusy(mode);
    setPdfError(null);
    try {
      const url = `/api/ceph/${id}/report/pdf`;
      const filename = `ceph-report-${id}.pdf`;
      if (mode === 'download') await downloadPdfFromApi(url, filename);
      else await printPdfFromApi(url, filename);
    } catch (err) {
      setPdfError(err instanceof Error ? err.message : 'تعذر إنشاء تقرير PDF');
    } finally {
      setPdfBusy(null);
    }
  };

  const handleDiagnosisChange = async (partial: Partial<CephDiagnosis>) => {
    const updated: CephDiagnosis = {
      ...diagnosis,
      ...partial,
      doctorApproved: partial.doctorApproved ?? diagnosis?.doctorApproved ?? false,
    };
    setDiagnosis(updated);
    await api.put(`/api/ceph/${id}/diagnosis`, updated).catch((e) => { console.error("[Ceph] Failed to save diagnosis:", e); });
  };

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin text-clinic-blue" />
    </div>
  );
  if (!analysis) return <div className="text-center py-20 text-gray-400">التحليل غير موجود</div>;

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)] overflow-hidden">
      {/* ── Header ── */}
      <div className="flex-shrink-0 flex items-center justify-between border-b border-gray-200 bg-white px-3 py-2 gap-2">
        <div className="flex items-center gap-3 min-w-0">
          <Link href={`/ortho/${analysis.orthoCaseId}?tab=ceph`}
            title="العودة إلى حالة التقويم"
            className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 transition text-gray-500 flex-shrink-0">
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div className="min-w-0">
            <h1 className="text-base font-extrabold text-gray-900 truncate">{analysis.patientName}</h1>
            <p className="text-[10px] text-gray-400">
              {formatArabicDate(analysis.analysisDate)} · {ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}
            </p>
          </div>
        </div>

        {/* Toolbar */}
        <div className="flex items-center gap-1.5 flex-shrink-0 flex-wrap justify-end">
          <button
            type="button"
            onClick={handleAiTrace}
            disabled={aiTracing || !analysis.xrayFileUrl}
            title="إنشاء مسودة نقاط بالذكاء الاصطناعي ثم مراجعتها يدوياً"
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-violet-300 bg-violet-50 text-violet-800 hover:bg-violet-100 disabled:opacity-50 transition">
            {aiTracing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Brain className="w-3.5 h-3.5" />}
            {aiTracing ? "جارٍ تحليل الصورة..." : "مسودة AI للنقاط"}
          </button>

          <button onClick={handleTemplateSimulation} disabled={detecting}
            title="قالب تجريبي لمواضع المعالم — ليس ذكاءً اصطناعيًا"
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-amber-300 bg-amber-50 text-amber-800 hover:bg-amber-100 disabled:opacity-60 transition">
            {detecting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <PlayCircle className="w-3.5 h-3.5" />}
            {detecting ? 'جارٍ التوليد...' : 'قالب تدريبي'}
          </button>

          <button onClick={handleSaveAndCompute} disabled={saving || !landmarks.length}
            className={cn(
              "flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-md transition",
              saveStatus === 'saved' ? "bg-green-600 text-white" :
              saveStatus === 'error' ? "bg-red-100 text-red-700 border border-red-300" :
              "bg-clinic-blue text-white hover:opacity-90 disabled:opacity-60"
            )}>
            {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> :
             saveStatus === 'saved' ? <CheckCircle2 className="w-3.5 h-3.5" /> :
             <Calculator className="w-3.5 h-3.5" />}
            {saving ? 'جارٍ الحفظ...' : saveStatus === 'saved' ? 'تم الحفظ والحساب' : 'حفظ وحساب'}
          </button>

          {/* The PDF is generated from SAVED data only — gate on a clean,
              computed state so a stale report can never be exported. */}
          <button onClick={() => handleReportPdf('download')}
            disabled={placedCount === 0 || isDirty || !analysis?.measurements?.length || pdfBusy !== null}
            title={placedCount === 0 ? 'ضع المعالم واحسب القياسات أولًا لإنشاء التقرير'
              : isDirty || !analysis?.measurements?.length ? 'اضغط «احسب» لحفظ المعالم والقياسات قبل إصدار التقرير'
              : 'تحميل تقرير التحليل السيفالومتري PDF'}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-gray-200 text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition">
            {pdfBusy === 'download' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileDown className="w-3.5 h-3.5" />}
            تحميل التقرير PDF
          </button>

          <button onClick={() => handleReportPdf('print')}
            disabled={placedCount === 0 || isDirty || !analysis?.measurements?.length || pdfBusy !== null}
            title={placedCount === 0 ? 'ضع المعالم واحسب القياسات أولًا لإنشاء التقرير'
              : isDirty || !analysis?.measurements?.length ? 'اضغط «احسب» لحفظ المعالم والقياسات قبل إصدار التقرير'
              : 'طباعة تقرير التحليل السيفالومتري'}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-gray-200 text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition">
            {pdfBusy === 'print' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Printer className="w-3.5 h-3.5" />}
            طباعة التقرير
          </button>

          {/* Visual Treatment Objective — planned incisor movements preview.
              The VTO page refetches the SAVED analysis, so it is gated on a
              clean state: unsaved landmark/calibration edits must be saved
              first or the preview would build from stale positions. */}
          {placedCount === 0 || isDirty ? (
            <span
              title={placedCount === 0
                ? 'ضع المعالم واحفظها أولًا لفتح هدف العلاج'
                : 'اضغط «احسب» لحفظ المعالم والمعايرة قبل فتح هدف العلاج'}
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-gray-200 text-gray-300 cursor-not-allowed">
              <Target className="w-3.5 h-3.5" />
              هدف العلاج VTO
            </span>
          ) : (
            <Link href={`/ceph/vto?analysisId=${id}`}
              title="هدف العلاج البصري — معاينة الحركات المخطّطة للقواطع"
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-clinic-blue/30 bg-clinic-blue/5 text-clinic-blue hover:bg-clinic-blue/10 transition">
              <Target className="w-3.5 h-3.5" />
              هدف العلاج VTO
            </Link>
          )}

          <div className="flex items-center gap-0.5 bg-gray-100 rounded-lg p-0.5">
            <button onClick={() => setShowPlanes(!showPlanes)}
              className={cn("p-1.5 rounded-md transition", showPlanes ? "bg-white shadow-sm text-clinic-blue" : "text-gray-400")}
              title="المستويات المرجعية">
              {showPlanes ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
            </button>
            <button onClick={() => setShowTracing(!showTracing)}
              className={cn("p-1.5 rounded-md transition", showTracing ? "bg-white shadow-sm text-pink-600" : "text-gray-400")}
              title="التتبّع التشريحي">
              <ScanLine className="w-3.5 h-3.5" />
            </button>
            <button onClick={() => setShowSim(!showSim)}
              className={cn("p-1.5 rounded-md transition", showSim ? "bg-white shadow-sm text-green-600" : "text-gray-400")}
              title="محاكاة العلاج">
              {showSim ? <PlayCircle className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
            </button>
          </div>

          {isDirty && (
            <span className="text-[10px] text-orange-500 font-medium flex items-center gap-0.5">
              <Save className="w-3 h-3" />غير محفوظ
            </span>
          )}
        </div>
      </div>

      <div className="flex-shrink-0 border-b border-gray-200 bg-gray-50 px-3 py-1.5">
        <div className="flex items-center gap-1 overflow-x-auto text-[11px]">
          {[
            { label: "الصورة", done: Boolean(analysis.xrayFileUrl), icon: ImageIcon },
            { label: "الترقيم", done: placedCount === totalCount, icon: ListChecks },
            { label: "التحليل", done: Boolean(analysis.measurements?.length) && !isDirty, icon: Calculator },
            { label: "التشخيص", done: Boolean(diagnosis?.finalDiagnosis), icon: Brain },
            { label: "التقرير", done: Boolean(analysis.measurements?.length) && !isDirty, icon: FileText },
          ].map((stage, index) => {
            const Icon = stage.icon;
            return (
              <div key={stage.label} className="flex items-center flex-shrink-0">
                <span className={cn(
                  "flex items-center gap-1.5 rounded-md px-2.5 py-1 font-medium",
                  stage.done ? "bg-emerald-50 text-emerald-700" :
                  index === 1 ? "bg-white text-clinic-blue shadow-sm ring-1 ring-gray-200" :
                  "text-gray-400"
                )}>
                  <Icon className="h-3.5 w-3.5" />
                  {stage.label}
                  {stage.done && <CheckCircle2 className="h-3 w-3" />}
                </span>
                {index < 4 && <ChevronRight className="mx-0.5 h-3.5 w-3.5 text-gray-300 rtl:rotate-180" />}
              </div>
            );
          })}
        </div>
      </div>

      {/* Saved-data readiness — is the report/VTO producible from saved data? */}
      {readiness && (
        <div className="flex-shrink-0 px-1 pt-2">
          <CephReadinessBadge readiness={readiness} variant="bar" />
        </div>
      )}

      {/* Honest-simulation banners */}
      {simNotice && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          <span>⚠️ {simNotice}</span>
          <button onClick={() => setSimNotice(null)} className="text-amber-600 hover:text-amber-800 font-bold flex-shrink-0">✕</button>
        </div>
      )}
      {simError && (
        <div className="flex-shrink-0 mx-1 mb-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {simError}
        </div>
      )}
      {aiTraceNotice && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-violet-300 bg-violet-50 px-3 py-2 text-xs text-violet-800">
          <span>{aiTraceNotice}</span>
          <button onClick={() => setAiTraceNotice(null)} className="font-bold text-violet-600 hover:text-violet-800">✕</button>
        </div>
      )}
      {aiTraceError && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <span>{aiTraceError}</span>
          <button onClick={() => setAiTraceError(null)} className="font-bold text-red-500 hover:text-red-700">✕</button>
        </div>
      )}
      {pdfError && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <span>فشل إنشاء تقرير PDF: {pdfError}</span>
          <button onClick={() => setPdfError(null)} className="text-red-500 hover:text-red-700 font-bold flex-shrink-0">✕</button>
        </div>
      )}

      {/* Simulation scenario bar */}
      {showSim && (
        <div className="flex-shrink-0 flex items-center gap-2 px-1 pb-2 flex-wrap">
          <span className="text-[10px] text-gray-500">سيناريو:</span>
          {Object.entries(SIMULATION_SCENARIOS).map(([k, sc]) => (
            <button key={k} onClick={() => setSimScenario(k)}
              className={cn("px-2 py-0.5 text-[10px] rounded-lg border transition",
                simScenario === k ? "bg-green-600 text-white border-green-600" : "border-gray-200 text-gray-500 hover:bg-gray-50"
              )}>
              {sc.label}
            </button>
          ))}
        </div>
      )}

      {/* Main content */}
      <div className="flex-1 flex overflow-hidden border-t border-gray-200">
        <aside className="hidden w-64 flex-shrink-0 flex-col overflow-hidden border-e border-gray-200 bg-white lg:flex">
          <div className="border-b border-gray-100 px-3 py-2">
            <div className="flex items-center justify-between gap-2">
              <div>
                <p className="text-xs font-bold text-gray-800">دليل النقاط التشريحية</p>
                <p className="text-[10px] text-gray-400">اختر نقطة ثم ضعها أو حرّكها فوق الصورة</p>
              </div>
              <span className={cn(
                "rounded-md px-2 py-1 text-[10px] font-bold tabular-nums",
                placedCount === totalCount ? "bg-emerald-50 text-emerald-700" : "bg-blue-50 text-clinic-blue",
              )}>
                {placedCount}/{totalCount}
              </span>
            </div>
            <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-gray-100">
              <div
                className="h-full rounded-full bg-clinic-blue transition-all"
                style={{ width: `${Math.min(100, (placedCount / totalCount) * 100)}%` }}
              />
            </div>
          </div>

          <div className="flex-1 overflow-y-auto px-2 py-2">
            {LANDMARK_GROUPS.map(group => (
              <section key={group.key} className="mb-2">
                <p className="px-2 py-1 text-[9px] font-bold text-gray-400">{group.label}</p>
                {group.keys.map(key => {
                  const def = LANDMARK_DEFS[key];
                  const placed = lmMap[key];
                  const isSelected = selectedKey === key;
                  return (
                    <button
                      key={key}
                      type="button"
                      onClick={() => setSelectedKey(isSelected ? null : key)}
                      className={cn(
                        "mb-0.5 flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-start text-[11px] transition",
                        isSelected
                          ? "bg-blue-50 text-clinic-blue ring-1 ring-blue-200"
                          : "text-gray-600 hover:bg-gray-50",
                      )}
                    >
                      <span
                        className="h-2.5 w-2.5 flex-shrink-0 rounded-full border"
                        style={{
                          backgroundColor: placed ? def?.color : "transparent",
                          borderColor: placed ? def?.color : "#cbd5e1",
                        }}
                      />
                      <span className="w-7 flex-shrink-0 font-mono text-[10px] font-bold text-gray-500">{key}</span>
                      <span className="min-w-0 flex-1 truncate">{def?.nameAr}</span>
                      {placed?.isAiPlaced && (
                        <span className="rounded bg-violet-50 px-1 text-[8px] font-bold text-violet-600">AI</span>
                      )}
                      {placed && <CheckCircle2 className="h-3.5 w-3.5 flex-shrink-0 text-emerald-500" />}
                    </button>
                  );
                })}
              </section>
            ))}
          </div>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col bg-slate-950">
          <div className="flex flex-shrink-0 items-center gap-3 overflow-x-auto border-b border-white/10 bg-slate-900 px-3 py-2 text-[10px] text-slate-200">
            <label className="flex flex-shrink-0 items-center gap-2">
              <Sun className="h-3.5 w-3.5" />
              <span>الإضاءة</span>
              <input
                type="range"
                min={40}
                max={180}
                value={brightness}
                onChange={e => setBrightness(Number(e.target.value))}
                className="w-24 accent-blue-400"
              />
              <span className="w-8 font-mono tabular-nums">{brightness}%</span>
            </label>
            <label className="flex flex-shrink-0 items-center gap-2">
              <Contrast className="h-3.5 w-3.5" />
              <span>التباين</span>
              <input
                type="range"
                min={40}
                max={220}
                value={contrast}
                onChange={e => setContrast(Number(e.target.value))}
                className="w-24 accent-blue-400"
              />
              <span className="w-8 font-mono tabular-nums">{contrast}%</span>
            </label>
            <button
              type="button"
              onClick={() => setInverted(value => !value)}
              className={cn(
                "flex flex-shrink-0 items-center gap-1.5 rounded-md px-2 py-1 transition",
                inverted ? "bg-blue-500 text-white" : "bg-white/10 hover:bg-white/15",
              )}
              title="عكس درجات الأشعة"
            >
              <ScanLine className="h-3.5 w-3.5" />
              عكس الصورة
            </button>
            <button
              type="button"
              onClick={() => {
                setBrightness(100);
                setContrast(100);
                setInverted(false);
              }}
              className="flex flex-shrink-0 items-center gap-1.5 rounded-md bg-white/10 px-2 py-1 hover:bg-white/15"
              title="إعادة ضبط عرض الصورة"
            >
              <RotateCcw className="h-3.5 w-3.5" />
              إعادة الضبط
            </button>
            <span className="h-4 w-px flex-shrink-0 bg-white/15" />
            <button
              type="button"
              onClick={() => setShowMeasurements(value => !value)}
              className={cn(
                "flex flex-shrink-0 items-center gap-1.5 rounded-md px-2 py-1 transition",
                showMeasurements ? "bg-emerald-500/20 text-emerald-300" : "bg-white/10 text-slate-300",
              )}
            >
              <Calculator className="h-3.5 w-3.5" />
              قيم القياسات
            </button>
          </div>

          <div className="min-h-0 flex-1">
            <CephCanvas
              imageUrl={resolveImageUrl(analysis.xrayFileUrl) || null}
              imageWidth={imageSize.w}
              imageHeight={imageSize.h}
              landmarks={landmarks}
              onLandmarksChange={handleLandmarksChange}
              selectedKey={selectedKey}
              onSelectKey={setSelectedKey}
              showPlanes={showPlanes}
              showTracing={showTracing}
              showSimulation={showSim}
              simulationScenario={simScenario}
              showMeasurements={showMeasurements}
              measurements={activeReportData}
              onCalibrate={handleCalibrationChange}
              imageAdjustments={{ brightness, contrast, inverted }}
              onImageDimensions={handleImageDimensions}
            />
          </div>

          <div className="flex flex-shrink-0 flex-wrap items-center gap-3 border-t border-white/10 bg-slate-900 px-3 py-1.5 text-[10px] text-slate-400">
            <span className="font-mono">
              <span className={cn("font-bold", placedCount >= 20 ? "text-emerald-400" : placedCount > 10 ? "text-amber-400" : "text-slate-300")}>
                {placedCount}
              </span>/{totalCount} نقطة
            </span>
            <span>·</span>
            <div className="flex items-center gap-1">
              <span>معيار الصورة:</span>
              <input
                type="number"
                value={pixelsPerMm ?? ''}
                min={0}
                step={0.01}
                onChange={e => handleCalibrationChange(e.target.value ? +e.target.value : null)}
                placeholder="px/mm"
                className="w-16 rounded border border-white/15 bg-white/10 px-1.5 py-0.5 text-[10px] text-white focus:outline-none focus:ring-1 focus:ring-blue-400"
                dir="ltr"
              />
              <span className="text-slate-500">px/mm</span>
            </div>
            {selectedKey && (
              <>
                <span>·</span>
                <span className="font-semibold text-blue-300">
                  انقر لوضع: {LANDMARK_DEFS[selectedKey]?.nameAr ?? selectedKey}
                </span>
              </>
            )}
          </div>
        </div>

        <aside className="flex w-80 flex-shrink-0 flex-col overflow-hidden border-s border-gray-200 bg-white xl:w-[23rem]">
          <div className="flex flex-shrink-0 border-b border-gray-100">
            {([
              { key: 'report', label: 'التحليل والقياسات' },
              { key: 'diagnosis', label: 'التشخيص' },
            ] as { key: RightTab; label: string }[]).map(t => (
              <button
                key={t.key}
                type="button"
                onClick={() => setRightTab(t.key)}
                className={cn(
                  "flex-1 border-b-2 py-2.5 text-[11px] font-semibold transition",
                  rightTab === t.key
                    ? "border-clinic-blue text-clinic-blue"
                    : "border-transparent text-gray-400 hover:text-gray-600",
                )}
              >
                {t.label}
              </button>
            ))}
          </div>

          <div className="flex flex-1 flex-col overflow-hidden">
            {rightTab === 'report' && (
              <div className="flex-1 overflow-hidden p-2">
                <AnalysisReport
                  measurements={activeReportData}
                  diagnosis={null}
                  patientName={analysis.patientName}
                  analysisDate={analysis.analysisDate}
                  calibrated={pixelsPerMm !== null && pixelsPerMm > 0}
                />
              </div>
            )}

            {rightTab === 'diagnosis' && (
              <div className="flex-1 overflow-hidden p-2">
                <AnalysisReport
                  measurements={activeReportData}
                  diagnosis={diagnosis ?? { doctorApproved: false }}
                  onDiagnosisChange={handleDiagnosisChange}
                  patientName={analysis.patientName}
                  analysisDate={analysis.analysisDate}
                  calibrated={pixelsPerMm !== null && pixelsPerMm > 0}
                  defaultGroup="steiner"
                  analysisId={id}
                />
              </div>
            )}
          </div>

          <div className="border-t border-gray-100 p-2">
            <Link
              href={`/ortho/${analysis.orthoCaseId}/model-analysis`}
              className="flex w-full items-center justify-between rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-xs font-bold text-clinic-blue hover:bg-blue-100"
            >
              <span>تحاليل النماذج والأسنان</span>
              <ChevronRight className="h-4 w-4 rtl:rotate-180" />
            </Link>
          </div>
        </aside>
      </div>
    </div>
  );
}
