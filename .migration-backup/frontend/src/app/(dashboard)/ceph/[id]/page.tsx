"use client";
import { useEffect, useState, useMemo, useCallback } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  Brain, Calculator, Eye, EyeOff, Play, PlayCircle, ArrowRight,
  Save, CheckCircle2, ChevronRight, ChevronDown, Loader2, FileDown, Printer,
  Sun, Contrast, RotateCcw, ListChecks, ImageIcon, FileText, ScanLine, Target,
  User, FolderOpen, History, Camera, Lock, X, ArrowLeftRight, ShieldCheck,
  AlertTriangle, Clock3, FileSearch,
} from "lucide-react";
import type {
  CephAnalysis, CephLandmark, CephDiagnosis, AnalysisType,
  CephVersionListItem, CephVersionDetail,
} from "@/types/ceph";
import { ANALYSIS_GROUPS, ANALYSIS_TYPE_AR } from "@/types/ceph";
import { buildMeasurementList, applyNormOverrides, type ApiNorm } from "@/lib/cephMath";
import { CephCanvas, LANDMARK_DEFS, LANDMARK_ORDER, SIMULATION_SCENARIOS } from "@/components/ceph/CephCanvas";
import { CephPaCanvas } from "@/components/ceph/CephPaCanvas";
import { buildPaMeasurements, PA_LANDMARK_DEFS, PA_LANDMARK_GROUPS, PA_LANDMARK_ORDER } from "@/lib/cephPa";
import { AnalysisReport } from "@/components/ceph/AnalysisReport";
import { CephReadinessBadge } from "@/components/ceph/CephReadinessBadge";
import { CephQualityPanel } from "@/components/ceph/CephQualityPanel";
import {
  cephReadinessFromAnalysis,
  computeCephQuality,
} from "@/lib/cephReadiness";
import api from "@/lib/api";
import { extractApiError } from "@/lib/apiClient";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import { downloadPdfFromApi, printPdfFromApi } from "@/lib/pdfDownload";
import { CephMeasurementExportButton } from "@/components/ceph/CephMeasurementExportButton";
import { CephAssessmentPanel } from "@/components/ceph/CephAssessmentPanel";
import {
  CephWebCephImportDialog,
  type WebCephImportSummary,
} from "@/components/ceph/CephWebCephImportDialog";
import { cn, formatArabicDate } from "@/lib/utils";

const LANDMARK_GROUPS = [
  { key: 'cranial',  label: 'قاعدة الجمجمة',  keys: ['S', 'N', 'Or', 'Po'] },
  { key: 'maxilla',  label: 'الفك العلوي',     keys: ['ANS', 'PNS', 'A'] },
  { key: 'mandible', label: 'الفك السفلي',     keys: ['B', 'Pog', 'Gn', 'Me', 'Go', 'Co', 'Ar', 'D', 'Pm'] },
  { key: 'dental',   label: 'الأسنان',         keys: ['U1T', 'U1A', 'L1T', 'L1A', 'U6', 'L6'] },
  { key: 'soft',     label: 'الأنسجة الرخوة',  keys: ['LS', 'LI', 'Pn', 'Cm', 'SPog'] },
];

const isExternalPlacement = (landmark?: CephLandmark) =>
  landmark?.placementSource === "ai" || landmark?.placementSource === "webceph-import";

type RightTab = 'report' | 'diagnosis' | 'assessment';

export default function CephAnalysisPage() {
  const { id } = useParams<{ id: string }>();
  const [analysis, setAnalysis]       = useState<CephAnalysis | null>(null);
  const [loading, setLoading]         = useState(true);
  const [loadError, setLoadError]     = useState<string | null>(null);
  const [loadAttempt, setLoadAttempt] = useState(0);
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
  // AI per-landmark refinement state — the canvas shows a spinner on the
  // context-menu item while a refine request is in flight.
  const [refiningKey, setRefiningKey] = useState<string | null>(null);
  // C-C: Arabic ceph PDF report (download / print)
  const [pdfBusy, setPdfBusy]         = useState<'download' | 'print' | null>(null);
  const [pdfError, setPdfError]       = useState<string | null>(null);
  // CEPH-EPIC: clinical approval gate — the final report is blocked until an
  // authorized doctor/admin approves the analysis.
  const [approving, setApproving]     = useState(false);
  const [approveError, setApproveError] = useState<string | null>(null);
  // C-B: analysis VERSION snapshots — list + detail viewer.
  const [versions, setVersions]           = useState<CephVersionListItem[]>([]);
  const [versionsOpen, setVersionsOpen]   = useState(false);
  const [versionSaving, setVersionSaving] = useState(false);
  const [versionError, setVersionError]   = useState<string | null>(null);
  const [viewedVersion, setViewedVersion] = useState<CephVersionDetail | null>(null);
  const [versionLoading, setVersionLoading] = useState(false);

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
    setLoading(true);
    setLoadError(null);
    setAnalysis(null);
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
      .catch((error) => {
        setLoadError(extractApiError(
          error,
          "تعذر تحميل تحليل السيفالومتري. تحقق من الاتصال ثم أعد المحاولة.",
        ));
      })
      .finally(() => setLoading(false));
  }, [id, loadAttempt]);

  // C-B: load the list of saved version snapshots for this analysis.
  // Best-effort — a missing list (e.g. 404 on a fresh DB before migration)
  // silently keeps the toolbar button visible but empty.
  const refreshVersions = useCallback(() => {
    api.get<CephVersionListItem[]>(`/api/ceph/${id}/versions`)
      .then(r => setVersions(Array.isArray(r.data) ? r.data : []))
      .catch(() => setVersions([]));
  }, [id]);

  useEffect(() => {
    refreshVersions();
  }, [refreshVersions]);

  const handleSaveVersion = async () => {
    const label = window.prompt("اسم النسخة (مثال: قبل العلاج، بعد 6 أشهر):", "");
    if (!label || !label.trim()) return;
    setVersionSaving(true);
    setVersionError(null);
    try {
      await api.post(`/api/ceph/${id}/versions`, { label: label.trim() });
      refreshVersions();
    } catch (err) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setVersionError(msg ?? "تعذر حفظ النسخة");
    } finally {
      setVersionSaving(false);
    }
  };

  const handleLoadVersion = async (versionId: string) => {
    setVersionsOpen(false);
    setVersionLoading(true);
    setVersionError(null);
    try {
      const { data } = await api.get<CephVersionDetail>(`/api/ceph/${id}/versions/${versionId}`);
      setViewedVersion(data);
    } catch (err) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setVersionError(msg ?? "تعذر تحميل النسخة");
    } finally {
      setVersionLoading(false);
    }
  };

  const lmMap = useMemo(() => {
    const m: Record<string, CephLandmark> = {};
    landmarks.forEach(l => { m[l.key] = l; });
    return m;
  }, [landmarks]);

  const isPa = analysis?.analysisType === "pa";
  const landmarkDefs = isPa ? PA_LANDMARK_DEFS : LANDMARK_DEFS;
  const landmarkOrder = isPa ? PA_LANDMARK_ORDER : LANDMARK_ORDER;
  const landmarkGroups = isPa ? PA_LANDMARK_GROUPS : LANDMARK_GROUPS;
  const selectedLandmark = selectedKey ? lmMap[selectedKey] : undefined;
  const extraImportedLandmarks = useMemo(
    () => landmarks.filter((landmark) => landmark.key.startsWith("WC_") || !landmarkDefs[landmark.key]),
    [landmarks, landmarkDefs],
  );

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
    return isPa
      ? buildPaMeasurements(landmarks, pixelsPerMm)
      : buildMeasurementList(pts, pixelsPerMm, analysisGroups);
  }, [landmarks, pixelsPerMm, analysisGroups, normsVersion, isPa]);

  const activeReportData = (analysis?.measurements?.length && !isDirty)
    ? analysis.measurements
    : computedMeasurements;

  const placedCount = landmarkOrder.filter((key) => Boolean(lmMap[key])).length;
  const totalCount  = landmarkOrder.length;
  const requiredPointsComplete = !isPa || PA_LANDMARK_ORDER.every((key) => Boolean(lmMap[key]));

  // Readiness of the SAVED record (image/calibration/points/measurements) plus
  // the live unsaved-edits flag — the same gate the PDF/VTO buttons enforce.
  const readiness = useMemo(
    () => analysis ? cephReadinessFromAnalysis(analysis, isDirty) : null,
    [analysis, isDirty],
  );

  // Audit §12: advisory data-quality signals computed from the LIVE canvas
  // state (landmarks + calibration) plus the saved measurement count. Drives
  // the warnings banner and the per-landmark low-confidence highlight. These
  // are advisory only — they never weaken the approval/PDF gate above.
  const quality = useMemo(
    () =>
      computeCephQuality({
        pixelsPerMm,
        landmarks,
        measurementCount: analysis?.measurements?.length ?? 0,
        isDirty,
        requiredLandmarkKeys: isPa ? PA_LANDMARK_ORDER : undefined,
      }),
    [pixelsPerMm, landmarks, analysis?.measurements?.length, isDirty, isPa],
  );
  const lowConfidenceSet = useMemo(
    () => new Set(quality.lowConfidenceKeys),
    [quality.lowConfidenceKeys],
  );

  const handleLandmarksChange = useCallback((lm: CephLandmark[]) => {
    setLandmarks(lm);
    setIsDirty(true);
  }, []);

  const handleReviewSelectedLandmark = useCallback(() => {
    if (!selectedKey) return;
    setLandmarks((current) => current.map((landmark) =>
      landmark.key === selectedKey && isExternalPlacement(landmark)
        ? { ...landmark, isAiPlaced: false, isReviewed: true }
        : landmark));
    setIsDirty(true);
    setSaveStatus("idle");
  }, [selectedKey]);

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

  const handleAiTrace = async (precision: "draft" | "high" = "draft") => {
    setAiTracing(true);
    setAiTraceError(null);
    setAiTraceNotice(null);
    try {
      const { data } = await api.post<{
        landmarks: CephLandmark[];
        modelId: string;
        inferenceRunId: string;
        modelRegistryKey: string;
        preprocessingVersion: string;
        landmarkDefinitionVersion: string;
        disclaimer: string;
        generatedAt: string;
      }>(`/api/ceph/${id}/ai/auto-trace`, {
        imageWidth: imageSize.w,
        imageHeight: imageSize.h,
        precision,
      });

      const drafted = (data.landmarks ?? []).map((landmark) => ({
        ...landmark,
        name: landmark.name || LANDMARK_DEFS[landmark.key]?.nameAr || landmark.key,
        nameAr: LANDMARK_DEFS[landmark.key]?.nameAr || landmark.name || landmark.key,
        group: LANDMARK_DEFS[landmark.key]?.group as CephLandmark["group"],
        isAiPlaced: true,
        placementSource: "ai" as const,
        sourceModelId: data.modelId,
        sourceInferenceRunId: data.inferenceRunId,
        aiProposalX: landmark.x,
        aiProposalY: landmark.y,
        isReviewed: false,
      }));
      setLandmarks(drafted);
      setIsDirty(true);
      setSaveStatus("idle");
      setSelectedKey(drafted[0]?.key ?? null);
      const modeLabel = precision === "high" ? " (تتبع متأنٍ)" : "";
      setAiTraceNotice(`${data.disclaimer}${modeLabel} النموذج: ${data.modelId}`);
    } catch (error) {
      const message = (error as { response?: { data?: { message?: string } } })
        .response?.data?.message;
      setAiTraceError(message ?? "تعذر توليد مسودة نقاط السيفالومتري");
    } finally {
      setAiTracing(false);
    }
  };

  const handleRefineLandmark = async (key: string) => {
    const lm = landmarks.find(l => l.key === key);
    if (!lm) return;
    setRefiningKey(key);
    setAiTraceError(null);
    try {
      const { data } = await api.post<{
        landmark: CephLandmark | null;
        modelId: string;
        inferenceRunId: string;
        modelRegistryKey: string;
        preprocessingVersion: string;
        landmarkDefinitionVersion: string;
        disclaimer: string;
        generatedAt: string;
      }>(`/api/ceph/${id}/ai/refine-landmark`, {
        landmarkKey: key,
        imageWidth: imageSize.w,
        imageHeight: imageSize.h,
        currentX: lm.x,
        currentY: lm.y,
      });

      if (!data.landmark) {
        // The model declined to refine — keep the current position, tell the
        // orthodontist honestly instead of silently doing nothing.
        setAiTraceNotice(`لم يستطع النموذج تحسين موضع النقطة ${key} بثقة كافية — ابقَ الموضع الحالي وراجعه يدويًا. ${data.disclaimer}`);
        return;
      }
      const refined = {
        ...data.landmark,
        name: data.landmark.name || LANDMARK_DEFS[data.landmark.key]?.nameAr || data.landmark.key,
        nameAr: LANDMARK_DEFS[data.landmark.key]?.nameAr || data.landmark.name || data.landmark.key,
        group: LANDMARK_DEFS[data.landmark.key]?.group as CephLandmark["group"],
        isAiPlaced: true,
        placementSource: "ai" as const,
        sourceModelId: data.modelId,
        sourceInferenceRunId: data.inferenceRunId,
        aiProposalX: data.landmark.x,
        aiProposalY: data.landmark.y,
        isReviewed: false,
      };
      setLandmarks(prev => prev.map(l => l.key === key ? refined : l));
      setIsDirty(true);
      setSaveStatus("idle");
      setAiTraceNotice(`تم تحسين موضع ${key} بواسطة الذكاء الاصطناعي. ${data.disclaimer}`);
    } catch (error) {
      const message = (error as { response?: { data?: { message?: string } } })
        .response?.data?.message;
      setAiTraceError(message ?? `تعذر تحسين موضع النقطة ${key}`);
    } finally {
      setRefiningKey(null);
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
          group:      l.group ?? landmarkDefs[l.key]?.group,
          x:          l.x,
          y:          l.y,
          isAiPlaced: l.isAiPlaced,
          confidence: l.confidence,
          reasoning: l.reasoning,
          placementSource: l.placementSource,
          sourceLandmarkKey: l.sourceLandmarkKey,
          sourceModelId: l.sourceModelId,
          sourceInferenceRunId: l.sourceInferenceRunId,
          aiProposalX: l.aiProposalX,
          aiProposalY: l.aiProposalY,
          isReviewed: l.isReviewed,
        })),
        pixelsPerMm: pixelsPerMm ?? 0,
        imageWidth:  imageSize.w,
        imageHeight: imageSize.h,
      });
      setAnalysis(res.data);
      setLandmarks(res.data.landmarks ?? []);
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

  const handleWebCephImported = (
    importedAnalysis: CephAnalysis,
    summary: WebCephImportSummary,
  ) => {
    setAnalysis(importedAnalysis);
    setLandmarks(importedAnalysis.landmarks ?? []);
    setDiagnosis(importedAnalysis.diagnosis ?? null);
    setPixelsPerMm(
      importedAnalysis.pixelsPerMm && importedAnalysis.pixelsPerMm > 0
        ? importedAnalysis.pixelsPerMm
        : null,
    );
    setIsDirty(false);
    setSaveStatus("saved");
    setAiTraceNotice(
      `تم استيراد ${summary.imported} نقطة من WebCeph. راجع النقاط المستوردة يدويًا قبل اعتماد التحليل.`,
    );
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

  const handleApprove = async () => {
    setApproving(true);
    setApproveError(null);
    try {
      const res = await api.post(`/api/ceph/${id}/approve`, {});
      // The endpoint returns the refreshed analysis; fall back to a local flag flip.
      const updated = res.data?.analysis as CephAnalysis | undefined;
      if (updated) {
        setAnalysis(updated);
        setDiagnosis(updated.diagnosis ?? null);
      } else {
        setAnalysis((prev) => (prev ? { ...prev, isApproved: true } : prev));
      }
    } catch (err) {
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setApproveError(message ?? 'تعذر اعتماد التحليل');
    } finally {
      setApproving(false);
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
  if (!analysis) return (
    <div className="mx-auto flex min-h-72 max-w-lg flex-col items-center justify-center px-6 py-16 text-center">
      <AlertTriangle className="mb-3 h-8 w-8 text-amber-500" aria-hidden="true" />
      <h1 className="text-base font-bold text-gray-900">تعذر فتح تحليل السيفالومتري</h1>
      <p className="mt-2 text-sm text-gray-600">{loadError ?? "التحليل غير موجود"}</p>
      <div className="mt-5 flex flex-wrap items-center justify-center gap-2">
        <button
          type="button"
          onClick={() => setLoadAttempt((attempt) => attempt + 1)}
          className="inline-flex items-center gap-2 rounded-md bg-clinic-blue px-3 py-2 text-xs font-bold text-white hover:opacity-90"
        >
          <RotateCcw className="h-4 w-4" aria-hidden="true" />
          إعادة المحاولة
        </button>
        <Link
          href="/ceph"
          className="inline-flex items-center gap-2 rounded-md border border-gray-200 px-3 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50"
        >
          <ArrowRight className="h-4 w-4" aria-hidden="true" />
          قائمة التحليلات
        </Link>
      </div>
    </div>
  );

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
            {/* Direct link to the patient file — the doctor jumps straight from
                the cephalometric workspace into the full record. */}
            <Link href={`/patients/${analysis.patientId}`}
              title="فتح ملف المريض"
              className="group flex min-w-0 items-center gap-1.5">
              <User className="h-3.5 w-3.5 flex-shrink-0 text-gray-400 group-hover:text-clinic-blue" />
              <h1 className="truncate text-base font-extrabold text-gray-900 group-hover:text-clinic-blue group-hover:underline">
                {analysis.patientName}
              </h1>
            </Link>
            <div className="flex items-center gap-1.5 text-[10px] text-gray-400">
              <span>{formatArabicDate(analysis.analysisDate)} · {ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}</span>
              {analysis.caseNumber && (
                <Link href={`/ortho/${analysis.orthoCaseId}?tab=ceph`}
                  title="فتح حالة التقويم"
                  className="inline-flex items-center gap-0.5 rounded bg-gray-100 px-1.5 py-0.5 font-mono text-gray-500 hover:bg-clinic-blue-50 hover:text-clinic-blue">
                  <FolderOpen className="h-2.5 w-2.5" />
                  {analysis.caseNumber}
                </Link>
              )}
            </div>
          </div>
        </div>

        {/* Toolbar */}
        <div className="flex items-center gap-1.5 flex-shrink-0 flex-wrap justify-end">
          {!isPa && <button
            type="button"
            onClick={() => handleAiTrace("draft")}
            disabled={aiTracing || !analysis.xrayFileUrl}
            title="مسودة نقاط بنموذج رؤية (Gemini Vision) — تتطلب مراجعة أخصائي التقويم وتحريك كل نقطة يدويًا قبل الحفظ"
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-violet-300 bg-violet-50 text-violet-800 hover:bg-violet-100 disabled:opacity-50 transition">
            {aiTracing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Brain className="w-3.5 h-3.5" />}
            {aiTracing ? "جارٍ تحليل الصورة..." : "مسودة AI للنقاط"}
          </button>}

          {/* Deliberate auto-trace: same Gemini endpoint, precision=high. The model
              takes a slower, deliberate pass and omits any landmark it cannot
              place with confidence > 0.5. The result is STILL an unsaved AI
              draft — the disclaimer banner remains mandatory. */}
          {!isPa && <button
            type="button"
            onClick={() => handleAiTrace("high")}
            disabled={aiTracing || !analysis.xrayFileUrl}
            title="تتبع متأنٍ — تمريرة أبطأ وأكثر حرصًا، لكنه يبقى مسودة غير معتمدة وتجب مراجعة كل نقطة."
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-indigo-300 bg-indigo-50 text-indigo-800 hover:bg-indigo-100 disabled:opacity-50 transition">
            {aiTracing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Target className="w-3.5 h-3.5" />}
            {aiTracing ? "جارٍ التتبع المتأني..." : "تتبع متأنٍ (مسودة)"}
          </button>}

          {!isPa && (
            <CephWebCephImportDialog
              analysisId={id}
              pixelsPerMm={pixelsPerMm}
              imageWidth={imageSize.w}
              imageHeight={imageSize.h}
              sLandmark={lmMap.S}
              onImported={handleWebCephImported}
            />
          )}

          {!isPa && <button onClick={handleTemplateSimulation} disabled={detecting}
            title="محاكاة (تجريبية) — قالب تدريبي لمواضع المعالم وليس ذكاءً اصطناعيًا حقيقيًا"
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-amber-300 bg-amber-50 text-amber-800 hover:bg-amber-100 disabled:opacity-60 transition">
            {detecting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <PlayCircle className="w-3.5 h-3.5" />}
            {detecting ? 'جارٍ التوليد...' : 'محاكاة (تجريبية)'}
          </button>}

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

          {/* C-B: save a named SNAPSHOT of the current analysis (landmarks +
              measurements + diagnosis) for longitudinal progress tracking.
              Disabled until the analysis has at least one landmark + computed
              measurements AND there are no unsaved edits. */}
          <button
            type="button"
            onClick={handleSaveVersion}
            disabled={versionSaving || placedCount === 0 || isDirty || !analysis?.measurements?.length}
            title={placedCount === 0 ? 'ضع المعالم واحفظها أولًا قبل حفظ نسخة'
              : isDirty ? 'احفظ التحليل الحالي قبل إنشاء نسخة'
              : !analysis?.measurements?.length ? 'احسب القياسات أولًا قبل حفظ نسخة'
              : 'حفظ نسخة من التحليل الحالي (المعالم + القياسات + التشخيص) لتتبع التقدم'}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-teal-300 bg-teal-50 text-teal-800 hover:bg-teal-100 disabled:opacity-50 disabled:cursor-not-allowed transition">
            {versionSaving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Camera className="w-3.5 h-3.5" />}
            حفظ نسخة
          </button>

          {/* C-B: versions dropdown — list saved snapshots; click to view. */}
          <div className="relative">
            <button
              type="button"
              onClick={() => setVersionsOpen(v => !v)}
              title="النسخ المحفوظة لهذا التحليل"
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-gray-200 text-gray-700 hover:bg-gray-50 disabled:opacity-50 transition">
              <History className="w-3.5 h-3.5" />
              النسخ
              {versions.length > 0 && (
                <span className="inline-flex items-center justify-center min-w-[1.25rem] h-5 px-1 rounded-full bg-clinic-blue text-white text-[9px] font-bold">
                  {versions.length}
                </span>
              )}
              <ChevronDown className="w-3 h-3" />
            </button>
            {versionsOpen && (
              <>
                <div
                  className="fixed inset-0 z-10"
                  onClick={() => setVersionsOpen(false)}
                  aria-hidden="true"
                />
                <div className="absolute z-20 mt-1 end-0 min-w-[18rem] max-w-[24rem] rounded-md border border-gray-200 bg-white shadow-lg overflow-hidden">
                  <div className="px-3 py-2 border-b border-gray-100 bg-gray-50 text-[11px] font-bold text-gray-700">
                    النسخ المحفوظة ({versions.length})
                  </div>
                  <div className="max-h-72 overflow-y-auto">
                    {versions.length === 0 ? (
                      <div className="px-3 py-6 text-center text-xs text-gray-400">
                        لا توجد نسخ محفوظة بعد.<br />
                        استخدم زر «حفظ نسخة» لتسجيل حالة التحليل الحالية.
                      </div>
                    ) : (
                      versions.map(v => (
                        <div
                          key={v.id}
                          className="flex w-full items-stretch border-b border-gray-50 last:border-b-0 text-start hover:bg-gray-50">
                          <button
                            type="button"
                            onClick={() => handleLoadVersion(v.id)}
                            title="عرض النسخة (للقراءة فقط)"
                            className="flex min-w-0 flex-1 items-center justify-between gap-2 px-3 py-2 text-xs text-start">
                            <div className="min-w-0 flex-1">
                              <div className="font-semibold text-gray-800 truncate">{v.label}</div>
                              <div className="text-[10px] text-gray-400">
                                {formatArabicDate(v.snapshotDate)} · {new Date(v.createdAt).toLocaleString('ar', { hour: '2-digit', minute: '2-digit' })}
                              </div>
                            </div>
                            <ChevronRight className="w-3.5 h-3.5 text-gray-400 rtl:rotate-180 flex-shrink-0" />
                          </button>
                          {/* C-B: compare the live analysis against this saved
                              version snapshot — opens /ceph/compare with
                              ?baseId=&versionId=, which renders the structural
                              superimposition (base landmarks vs version
                              landmarks) + a client-side measurements diff. */}
                          <Link
                            href={`/ceph/compare?baseId=${id}&versionId=${v.id}`}
                            title="قارن التحليل الحالي مع هذه النسخة"
                            className="flex flex-shrink-0 items-center gap-1 border-s border-gray-100 px-2.5 text-[10px] font-medium text-teal-700 hover:bg-teal-50 transition">
                            <ArrowLeftRight className="w-3 h-3" />
                            قارن
                          </Link>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </>
            )}
          </div>

          {/* CEPH-EPIC clinical approval gate. The final report cannot be
              issued until an authorized doctor/admin approves the analysis. */}
          {analysis?.isApproved ? (
            <span
              title={analysis.approvedByName
                ? `اعتمده ${analysis.approvedByName}${analysis.approvedAt ? ` — ${analysis.approvedAt}` : ''}`
                : 'تم اعتماد التحليل'}
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-green-200 bg-green-50 text-green-700">
              <ShieldCheck className="w-3.5 h-3.5" />
              تم اعتماد التحليل
              {analysis.approvedByName ? ` — ${analysis.approvedByName}` : ''}
            </span>
          ) : (
            <button onClick={handleApprove}
              disabled={approving || placedCount === 0 || !requiredPointsComplete || isDirty || !analysis?.measurements?.length || quality.unreviewedExternalKeys.length > 0}
              title={quality.unreviewedExternalKeys.length > 0
                ? `راجع نقاط الذكاء الاصطناعي أو WebCeph المتبقية (${quality.unreviewedExternalKeys.length}) قبل الاعتماد`
                : placedCount === 0 || !requiredPointsComplete || isDirty || !analysis?.measurements?.length
                ? 'ضع المعالم واحسب القياسات واحفظها أولًا ثم اعتمد التحليل'
                : 'اعتماد التحليل لإتاحة إصدار التقرير النهائي'}
              className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-green-300 bg-green-50 text-green-700 hover:bg-green-100 disabled:opacity-50 disabled:cursor-not-allowed transition">
              {approving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <ShieldCheck className="w-3.5 h-3.5" />}
              اعتماد التحليل
            </button>
          )}

          {/* The PDF is generated from SAVED data only — gate on a clean,
              computed state so a stale report can never be exported. The final
              report is additionally blocked until the analysis is approved. */}
          <button onClick={() => handleReportPdf('download')}
            disabled={!analysis?.isApproved || placedCount === 0 || !requiredPointsComplete || isDirty || !analysis?.measurements?.length || pdfBusy !== null}
            title={!analysis?.isApproved ? 'لا يمكن إصدار التقرير النهائي قبل اعتماد الطبيب للتحليل'
              : placedCount === 0 ? 'ضع المعالم واحسب القياسات أولًا لإنشاء التقرير'
              : isDirty || !analysis?.measurements?.length ? 'اضغط «احسب» لحفظ المعالم والقياسات قبل إصدار التقرير'
              : 'تحميل تقرير التحليل السيفالومتري PDF'}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-gray-200 text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition">
            {pdfBusy === 'download' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileDown className="w-3.5 h-3.5" />}
            تحميل التقرير PDF
          </button>

          <button onClick={() => handleReportPdf('print')}
            disabled={!analysis?.isApproved || placedCount === 0 || !requiredPointsComplete || isDirty || !analysis?.measurements?.length || pdfBusy !== null}
            title={!analysis?.isApproved ? 'لا يمكن إصدار التقرير النهائي قبل اعتماد الطبيب للتحليل'
              : placedCount === 0 ? 'ضع المعالم واحسب القياسات أولًا لإنشاء التقرير'
              : isDirty || !analysis?.measurements?.length ? 'اضغط «احسب» لحفظ المعالم والقياسات قبل إصدار التقرير'
              : 'طباعة تقرير التحليل السيفالومتري'}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md border border-gray-200 text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed transition">
            {pdfBusy === 'print' ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Printer className="w-3.5 h-3.5" />}
            طباعة التقرير
          </button>

          <CephMeasurementExportButton
            analysis={analysis}
            placedCount={placedCount}
            hasUnsavedEdits={isDirty}
            pointsComplete={requiredPointsComplete}
          />

          {/* Visual Treatment Objective — planned incisor movements preview.
              The VTO page refetches the SAVED analysis, so it is gated on a
              clean state: unsaved landmark/calibration edits must be saved
              first or the preview would build from stale positions. */}
          {!isPa && (placedCount === 0 || isDirty ? (
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
          ))}

          <div className="flex items-center gap-0.5 bg-gray-100 rounded-lg p-0.5">
            <button onClick={() => setShowPlanes(!showPlanes)}
              className={cn("p-1.5 rounded-md transition", showPlanes ? "bg-white shadow-sm text-clinic-blue" : "text-gray-400")}
              title="المستويات المرجعية">
              {showPlanes ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
            </button>
            {!isPa && <button onClick={() => setShowTracing(!showTracing)}
              className={cn("p-1.5 rounded-md transition", showTracing ? "bg-white shadow-sm text-pink-600" : "text-gray-400")}
              title="التتبّع التشريحي">
              <ScanLine className="w-3.5 h-3.5" />
            </button>}
            {!isPa && <button onClick={() => setShowSim(!showSim)}
              className={cn("p-1.5 rounded-md transition", showSim ? "bg-white shadow-sm text-green-600" : "text-gray-400")}
              title="محاكاة العلاج">
              {showSim ? <PlayCircle className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
            </button>}
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

      {/* Audit §12: data-quality warnings (calibration, low-confidence AI
          points, incomplete landmarks, unsaved edits, stale measurements). */}
      <div className="flex-shrink-0 px-1 pt-2">
        <CephQualityPanel quality={quality} />
      </div>

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
      {approveError && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <span>{approveError}</span>
          <button onClick={() => setApproveError(null)} className="text-red-500 hover:text-red-700 font-bold flex-shrink-0">✕</button>
        </div>
      )}
      {analysis && !analysis.isApproved && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800">
          <Lock className="w-3.5 h-3.5 mt-0.5 flex-shrink-0" />
          <span>لا يمكن إصدار التقرير النهائي قبل اعتماد الطبيب للتحليل</span>
        </div>
      )}
      {pdfError && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <span>فشل إنشاء تقرير PDF: {pdfError}</span>
          <button onClick={() => setPdfError(null)} className="text-red-500 hover:text-red-700 font-bold flex-shrink-0">✕</button>
        </div>
      )}
      {versionError && (
        <div className="flex-shrink-0 mx-1 mb-2 flex items-start justify-between gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          <span>{versionError}</span>
          <button onClick={() => setVersionError(null)} className="text-red-500 hover:text-red-700 font-bold flex-shrink-0">✕</button>
        </div>
      )}

      {/* C-B: version snapshot viewer modal. Read-only display of a saved
          snapshot's measurements + diagnosis. The modal is dismissed by
          clicking the backdrop or the close button; the live analysis state
          on the canvas is untouched (the snapshot is immutable JSON). */}
      {(viewedVersion || versionLoading) && (
        <div
          className="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-4"
          onClick={() => { if (!versionLoading) setViewedVersion(null); }}
        >
          <div
            className="relative w-full max-w-2xl max-h-[85vh] overflow-hidden rounded-lg bg-white shadow-xl flex flex-col"
            onClick={e => e.stopPropagation()}
          >
            <div className="flex flex-shrink-0 items-center justify-between border-b border-gray-200 px-4 py-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <History className="w-4 h-4 text-teal-600 flex-shrink-0" />
                  <h3 className="truncate text-sm font-bold text-gray-900">
                    {viewedVersion ? viewedVersion.label : "جارٍ التحميل..."}
                  </h3>
                </div>
                {viewedVersion && (
                  <p className="mt-0.5 text-[11px] text-gray-500">
                    نسخة محفوظة · {formatArabicDate(viewedVersion.snapshotDate)} · {new Date(viewedVersion.createdAt).toLocaleString('ar', { dateStyle: 'medium', timeStyle: 'short' })}
                  </p>
                )}
              </div>
              <button
                type="button"
                onClick={() => setViewedVersion(null)}
                disabled={versionLoading}
                className="rounded-md p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 disabled:opacity-50"
                title="إغلاق"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-3">
              {versionLoading ? (
                <div className="flex items-center justify-center py-12">
                  <Loader2 className="w-6 h-6 animate-spin text-clinic-blue" />
                </div>
              ) : viewedVersion ? (
                <div className="space-y-3">
                  <div className="rounded-md border border-teal-100 bg-teal-50 px-3 py-2 text-[11px] text-teal-800">
                    هذه نسخة محفوظة من التحليل وقت حفظها — للقراءة فقط. القياسات والتشخيص المعروض هنا هي ما تم تسجيله في تلك اللحظة ولا تتأثر بالتعديلات اللاحقة على التحليل الحالي.
                  </div>
                  <AnalysisReport
                    measurements={viewedVersion.measurements}
                    diagnosis={viewedVersion.diagnosis ?? null}
                    patientName={analysis.patientName}
                    analysisDate={viewedVersion.snapshotDate}
                    calibrated={pixelsPerMm !== null && pixelsPerMm > 0}
                  />
                </div>
              ) : null}
            </div>
          </div>
        </div>
      )}

      {/* Simulation scenario bar */}
      {!isPa && showSim && (
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
            {selectedLandmark && isExternalPlacement(selectedLandmark) && !selectedLandmark.isReviewed && (
              <button
                type="button"
                onClick={handleReviewSelectedLandmark}
                className="mt-2 flex w-full items-center justify-center gap-1.5 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-1.5 text-[10px] font-bold text-emerald-700 hover:bg-emerald-100"
                title="تأكيد أن الطبيب فحص موضع النقطة المحددة ووافق عليه"
              >
                <CheckCircle2 className="h-3.5 w-3.5" />
                تأكيد مراجعة {selectedLandmark.key}
              </button>
            )}
          </div>

          <div className="flex-1 overflow-y-auto px-2 py-2">
            {landmarkGroups.map(group => (
              <section key={group.key} className="mb-2">
                <p className="px-2 py-1 text-[9px] font-bold text-gray-400">{group.label}</p>
                {group.keys.map(key => {
                  const def = landmarkDefs[key];
                  const placed = lmMap[key];
                  const isSelected = selectedKey === key;
                  // Audit §12: per-landmark quality. AI-placed points stay a
                  // "مسودة" (draft) until reviewed; a confidence below the
                  // threshold (or unknown) is flagged with a warning style.
                  const source = placed?.placementSource ?? (placed?.isAiPlaced ? "ai" : "manual");
                  const isAiDraft = source === "ai" && !placed?.isReviewed;
                  const isLowConfidence = isAiDraft && lowConfidenceSet.has(key);
                  const confidencePct =
                    typeof placed?.confidence === "number"
                      ? Math.round(placed.confidence * 100)
                      : null;
                  const reasoning = placed?.reasoning;
                  return (
                    <button
                      key={key}
                      type="button"
                      onClick={() => setSelectedKey(isSelected ? null : key)}
                      title={
                        reasoning
                          ? `${def?.nameAr ?? key}\nسبب وضع النموذج للنقطة هنا: ${reasoning}`
                          : undefined
                      }
                      className={cn(
                        "mb-0.5 flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-start text-[11px] transition",
                        isSelected
                          ? "bg-blue-50 text-clinic-blue ring-1 ring-blue-200"
                          : isLowConfidence
                            ? "text-amber-800 ring-1 ring-amber-200 hover:bg-amber-50"
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
                      {source === "ai" && placed ? (
                        <span
                          title={
                            isLowConfidence
                              ? `نقطة AI بثقة منخفضة${confidencePct !== null ? ` (${confidencePct}%)` : " (غير معروفة)"} — راجعها يدويًا`
                              : placed.isReviewed
                                ? "نقطة AI راجعها الطبيب"
                                : `نقطة AI (مسودة) — راجعها يدويًا${confidencePct !== null ? ` · الثقة ${confidencePct}%` : ""}`
                          }
                          className={cn(
                            "inline-flex flex-shrink-0 items-center gap-0.5 rounded px-1 text-[8px] font-bold",
                            isLowConfidence
                              ? "bg-amber-100 text-amber-700"
                              : placed.isReviewed
                                ? "bg-emerald-50 text-emerald-700"
                                : "bg-violet-50 text-violet-600",
                          )}
                        >
                          {isLowConfidence && <AlertTriangle className="h-2.5 w-2.5" />}
                          AI · {placed.isReviewed ? "مراجع" : "مسودة"}
                          {!placed.isReviewed && confidencePct !== null && (
                            <span className="font-mono">{confidencePct}%</span>
                          )}
                        </span>
                      ) : source === "webceph-import" && placed ? (
                        <span
                          title={placed.isReviewed ? "نقطة WebCeph راجعها الطبيب" : "نقطة مستوردة من WebCeph وتحتاج مراجعة الطبيب"}
                          className={cn(
                            "rounded px-1 text-[8px] font-bold",
                            placed.isReviewed ? "bg-emerald-50 text-emerald-700" : "bg-cyan-50 text-cyan-700",
                          )}
                        >
                          WebCeph · {placed.isReviewed ? "مراجع" : "راجع"}
                        </span>
                      ) : (
                        placed && (
                          <span
                            title="نقطة موضوعة يدويًا"
                            className="rounded bg-emerald-50 px-1 text-[8px] font-bold text-emerald-600"
                          >
                            يدوي
                          </span>
                        )
                      )}
                      {placed && (
                        <CheckCircle2
                          className={cn(
                            "h-3.5 w-3.5 flex-shrink-0",
                            isLowConfidence ? "text-amber-500" : "text-emerald-500",
                          )}
                        />
                      )}
                    </button>
                  );
                })}
              </section>
            ))}
            {extraImportedLandmarks.length > 0 && (
              <section className="mb-2 border-t border-gray-100 pt-2">
                <p className="px-2 py-1 text-[9px] font-bold text-cyan-700">
                  نقاط WebCeph الإضافية ({extraImportedLandmarks.length})
                </p>
                {extraImportedLandmarks.map((landmark) => {
                  const selected = selectedKey === landmark.key;
                  return (
                    <button
                      key={landmark.key}
                      type="button"
                      onClick={() => setSelectedKey(selected ? null : landmark.key)}
                      className={cn(
                        "mb-0.5 flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-start text-[11px] transition",
                        selected ? "bg-cyan-50 text-cyan-800 ring-1 ring-cyan-200" : "text-gray-600 hover:bg-gray-50",
                      )}
                    >
                      <span className="h-2.5 w-2.5 shrink-0 rounded-full border border-cyan-500 bg-cyan-400" />
                      <span className="w-16 shrink-0 truncate font-mono text-[9px] font-bold text-gray-500">{landmark.key}</span>
                      <span className="min-w-0 flex-1 truncate" title={landmark.sourceLandmarkKey ?? landmark.name}>
                        {landmark.sourceLandmarkKey ?? landmark.name ?? landmark.key}
                      </span>
                      <span className={cn(
                        "rounded px-1 text-[8px] font-bold",
                        landmark.isReviewed ? "bg-emerald-50 text-emerald-700" : "bg-cyan-50 text-cyan-700",
                      )}>
                        {landmark.isReviewed ? "مراجع" : "راجع"}
                      </span>
                    </button>
                  );
                })}
              </section>
            )}
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
            {isPa ? <CephPaCanvas
              imageUrl={resolveImageUrl(analysis.xrayFileUrl) || null}
              imageWidth={imageSize.w}
              imageHeight={imageSize.h}
              landmarks={landmarks}
              onLandmarksChange={handleLandmarksChange}
              selectedKey={selectedKey}
              onSelectKey={setSelectedKey}
              showPlanes={showPlanes}
              showMeasurements={showMeasurements}
              measurements={activeReportData}
              onCalibrate={handleCalibrationChange}
              pixelsPerMm={pixelsPerMm}
              imageAdjustments={{ brightness, contrast, inverted }}
              onImageDimensions={handleImageDimensions}
            /> : <CephCanvas
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
              pixelsPerMm={pixelsPerMm}
              imageAdjustments={{ brightness, contrast, inverted }}
              onResetImageAdjustments={() => {
                setBrightness(100);
                setContrast(100);
                setInverted(false);
              }}
              onImageDimensions={handleImageDimensions}
              onRefineLandmark={handleRefineLandmark}
              refining={refiningKey !== null}
            />}
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
                  انقر لوضع: {landmarkDefs[selectedKey]?.nameAr ?? selectedKey}
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
              { key: 'assessment', label: 'التقييم' },
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
                  defaultGroup={isPa ? "pa" : "steiner"}
                  analysisId={id}
                />
              </div>
            )}

            {rightTab === 'assessment' && (
              <div className="flex-1 overflow-hidden p-2">
                <CephAssessmentPanel
                  analysisId={id}
                  orthoCaseId={analysis.orthoCaseId}
                  analysisApproved={analysis.isApproved}
                  hasUnsavedChanges={isDirty}
                  diagnosis={diagnosis}
                  measurements={analysis.measurements ?? []}
                />
              </div>
            )}
          </div>

          <div className="grid gap-2 border-t border-gray-100 p-2">
            <div className="grid grid-cols-2 gap-2">
              <Link href={`/ceph/case/${analysis.orthoCaseId}`} className="flex items-center justify-center gap-2 rounded-md border border-gray-200 px-2 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50"><FileSearch className="h-4 w-4" />مراجعة الحالة</Link>
              <Link href={`/ceph/timelapse/${analysis.orthoCaseId}`} className="flex items-center justify-center gap-2 rounded-md border border-gray-200 px-2 py-2 text-xs font-bold text-gray-700 hover:bg-gray-50"><Clock3 className="h-4 w-4" />Timelapse</Link>
            </div>
            <Link
              href={`/ortho/${analysis.orthoCaseId}/model-analysis`}
              className="flex w-full items-center justify-between rounded-md border border-blue-200 bg-blue-50 px-3 py-2 text-xs font-bold text-clinic-blue hover:bg-blue-100"
            >
              <span>Occlusogram وتحليل النماذج</span>
              <ChevronRight className="h-4 w-4 rtl:rotate-180" />
            </Link>
          </div>
        </aside>
      </div>
    </div>
  );
}
