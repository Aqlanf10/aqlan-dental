"use client";
import { useEffect, useState, useMemo, useCallback, useRef } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  Brain, Calculator, Eye, EyeOff, Play, PlayCircle, ArrowRight,
  Save, CheckCircle2, ChevronRight, Loader2, GitCompare,
  Cpu,
} from "lucide-react";
import type { CephAnalysis, CephLandmark, CephDiagnosis, AnalysisType } from "@/types/ceph";
import { ANALYSIS_GROUPS, ANALYSIS_TYPE_AR } from "@/types/ceph";
import { buildMeasurementList } from "@/lib/cephMath";
import { CephCanvas, LANDMARK_DEFS, LANDMARK_ORDER, SIMULATION_SCENARIOS, type CephCanvasExportRef } from "@/components/ceph/CephCanvas";
import { AnalysisReport } from "@/components/ceph/AnalysisReport";
import { CephPdfExport } from "@/components/ceph/CephPdfExport";
import { CephComparison } from "@/components/ceph/CephComparison";
import { useAiModelStatus } from "@/hooks/useCephAi";
import api from "@/lib/api";
import { cn, formatArabicDate } from "@/lib/utils";

const LANDMARK_GROUPS = [
  { key: 'cranial',  label: 'قاعدة الجمجمة',  keys: ['S', 'N', 'Or', 'Po'] },
  { key: 'maxilla',  label: 'الفك العلوي',     keys: ['ANS', 'PNS', 'A'] },
  { key: 'mandible', label: 'الفك السفلي',     keys: ['B', 'Pog', 'Gn', 'Me', 'Go', 'Co', 'Ar', 'D', 'Pm'] },
  { key: 'dental',   label: 'الأسنان',         keys: ['U1T', 'U1A', 'L1T', 'L1A'] },
  { key: 'soft',     label: 'الأنسجة الرخوة',  keys: ['LS', 'LI', 'Pn', 'Cm'] },
];

type RightTab = 'landmarks' | 'report' | 'diagnosis';

export default function CephAnalysisPage() {
  const { id } = useParams<{ id: string }>();
  const [analysis, setAnalysis]       = useState<CephAnalysis | null>(null);
  const [loading, setLoading]         = useState(true);
  const [landmarks, setLandmarks]     = useState<CephLandmark[]>([]);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [showPlanes, setShowPlanes]   = useState(true);
  const [showSim, setShowSim]         = useState(false);
  const [simScenario, setSimScenario] = useState(Object.keys(SIMULATION_SCENARIOS)[0]);
  const [pixelsPerMm, setPixelsPerMm] = useState<number | null>(null);
  const [isDirty, setIsDirty]         = useState(false);
  const [saving, setSaving]           = useState(false);
  const [detecting, setDetecting]     = useState(false);
  const [saveStatus, setSaveStatus]   = useState<'idle' | 'saved' | 'error'>('idle');
  const [rightTab, setRightTab]       = useState<RightTab>('landmarks');
  const [diagnosis, setDiagnosis]     = useState<CephDiagnosis | null>(null);
  const [imageSize, setImageSize]     = useState({ w: 800, h: 600 });
  const [showComparison, setShowComparison] = useState(false);

  const canvasExportRef = useRef<CephCanvasExportRef | null>(null);
  const aiModelStatus = useAiModelStatus();

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

  const computedMeasurements = useMemo(() => {
    const pts: Record<string, { x: number; y: number }> = {};
    landmarks.forEach(l => { pts[l.key] = { x: l.x, y: l.y }; });
    return buildMeasurementList(pts, pixelsPerMm, analysisGroups);
  }, [landmarks, pixelsPerMm, analysisGroups]);

  const activeReportData = (analysis?.measurements?.length && !isDirty)
    ? analysis.measurements
    : computedMeasurements;

  const placedCount = landmarks.length;
  const totalCount  = LANDMARK_ORDER.length;

  const handleLandmarksChange = useCallback((lm: CephLandmark[]) => {
    setLandmarks(lm);
    setIsDirty(true);
  }, []);

  const handleAiDetect = async () => {
    setDetecting(true);
    try {
      const res = await api.post<CephLandmark[]>(`/api/ceph/${id}/simulate`, {
        imageWidth: imageSize.w, imageHeight: imageSize.h, pixelsPerMm: pixelsPerMm ?? 0,
      });
      setLandmarks(res.data);
      setIsDirty(true);
      setSaveStatus('idle');
    } catch { /* ignore */ } finally { setDetecting(false); }
  };

  const handleSaveAndCompute = async () => {
    setSaving(true);
    setSaveStatus('idle');
    try {
      const res = await api.post<CephAnalysis>(`/api/ceph/${id}/landmarks`, {
        landmarks: landmarks.map(l => ({
          key: l.key, name: l.name, nameAr: l.nameAr,
          group: l.group ?? LANDMARK_DEFS[l.key]?.group,
          x: l.x, y: l.y, isAiPlaced: l.isAiPlaced, confidence: l.confidence,
        })),
        pixelsPerMm: pixelsPerMm ?? 0, imageWidth: imageSize.w, imageHeight: imageSize.h,
      });
      setAnalysis(res.data);
      setDiagnosis(res.data.diagnosis ?? null);
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

  const handleDiagnosisChange = async (partial: Partial<CephDiagnosis>) => {
    const updated: CephDiagnosis = {
      ...diagnosis,
      ...partial,
      doctorApproved: partial.doctorApproved ?? diagnosis?.doctorApproved ?? false,
    };
    setDiagnosis(updated);
    await api.put(`/api/ceph/${id}/diagnosis`, updated).catch(() => {});
  };

  const getCanvasDataUrl = useCallback((): string | undefined => {
    return canvasExportRef.current?.getDataUrl() ?? undefined;
  }, []);

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin" style={{ color: "#3d7ab5" }} />
    </div>
  );
  if (!analysis) return <div className="text-center py-20" style={{ color: "#94a3b8" }}>التحليل غير موجود</div>;

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)] overflow-hidden" dir="rtl">
      {/* ── Header ── */}
      <div className="flex-shrink-0 flex items-center justify-between py-3 px-1 gap-2">
        <div className="flex items-center gap-3 min-w-0">
          <Link href="/ceph"
            className="p-1.5 rounded-xl border hover:opacity-80 transition flex-shrink-0"
            style={{ borderColor: "#e8f0f9", color: "#64748b" }}>
            <ArrowRight className="w-4 h-4" />
          </Link>
          <div className="min-w-0">
            <h1 className="text-base font-extrabold truncate" style={{ color: "#0d2137" }}>{analysis.patientName}</h1>
            <div className="flex items-center gap-2 text-[10px]" style={{ color: "#94a3b8" }}>
              <span>{formatArabicDate(analysis.analysisDate)} · {ANALYSIS_TYPE_AR[analysis.analysisType] ?? analysis.analysisType}</span>
              {aiModelStatus.data && (
                <span
                  className="flex items-center gap-0.5 px-1.5 py-0.5 rounded-full text-[8px] font-bold"
                  style={{
                    backgroundColor: aiModelStatus.data.isModelLoaded ? "#22c55e18" : "#94a3b818",
                    color: aiModelStatus.data.isModelLoaded ? "#22c55e" : "#94a3b8",
                  }}
                >
                  <Cpu className="w-2.5 h-2.5" />
                  {aiModelStatus.data.isModelLoaded ? "ONNX" : "Template"}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Toolbar */}
        <div className="flex items-center gap-1.5 flex-shrink-0 flex-wrap justify-end">
          <button onClick={handleAiDetect} disabled={detecting}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-xl text-white hover:opacity-90 disabled:opacity-60 transition"
            style={{ backgroundColor: "#a855f7" }}>
            {detecting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Brain className="w-3.5 h-3.5" />}
            {detecting ? 'جارٍ الكشف...' : 'كشف AI'}
          </button>

          <button onClick={handleSaveAndCompute} disabled={saving || !landmarks.length}
            className={cn("flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-xl transition",
              saveStatus === 'saved' ? "text-white" : saveStatus === 'error' ? "text-white" : "text-white hover:opacity-90 disabled:opacity-60"
            )}
            style={{
              backgroundColor: saveStatus === 'saved' ? "#22c55e" : saveStatus === 'error' ? "#ef4444" : "#3d7ab5",
            }}>
            {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> :
             saveStatus === 'saved' ? <CheckCircle2 className="w-3.5 h-3.5" /> :
             <Calculator className="w-3.5 h-3.5" />}
            {saving ? 'حساب...' : saveStatus === 'saved' ? 'تم' : 'احسب'}
          </button>

          <div className="flex items-center gap-0.5 rounded-xl p-0.5" style={{ backgroundColor: "#f7fafd" }}>
            <button onClick={() => setShowPlanes(!showPlanes)}
              className="p-1.5 rounded-lg transition"
              style={showPlanes ? { backgroundColor: "#ffffff", color: "#3d7ab5", boxShadow: "0 1px 2px rgba(0,0,0,0.05)" } : { color: "#94a3b8" }}
              title="الخطوط التشريحية">
              {showPlanes ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
            </button>
            <button onClick={() => setShowSim(!showSim)}
              className="p-1.5 rounded-lg transition"
              style={showSim ? { backgroundColor: "#ffffff", color: "#22c55e", boxShadow: "0 1px 2px rgba(0,0,0,0.05)" } : { color: "#94a3b8" }}
              title="محاكاة العلاج">
              {showSim ? <PlayCircle className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5" />}
            </button>
          </div>

          <CephPdfExport
            analysis={analysis}
            canvasDataUrl={getCanvasDataUrl()}
            patientName={analysis.patientName}
          />

          <button onClick={() => setShowComparison(true)}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-xl text-white hover:opacity-90 transition"
            style={{ backgroundColor: "#3d7ab5" }}
            title="مقارنة التحاليل">
            <GitCompare className="w-3.5 h-3.5" />
            مقارنة
          </button>

          {isDirty && (
            <span className="text-[10px] font-medium flex items-center gap-0.5" style={{ color: "#f5922e" }}>
              <Save className="w-3 h-3" />غير محفوظ
            </span>
          )}
        </div>
      </div>

      {/* Simulation scenario bar */}
      {showSim && (
        <div className="flex-shrink-0 flex items-center gap-2 px-1 pb-2 flex-wrap">
          <span className="text-[10px]" style={{ color: "#64748b" }}>سيناريو:</span>
          {Object.entries(SIMULATION_SCENARIOS).map(([k, sc]) => (
            <button key={k} onClick={() => setSimScenario(k)}
              className="px-2 py-0.5 text-[10px] rounded-xl border transition"
              style={simScenario === k
                ? { backgroundColor: "#22c55e", color: "#ffffff", borderColor: "#22c55e" }
                : { borderColor: "#e8f0f9", color: "#64748b" }
              }>
              {sc.label}
            </button>
          ))}
        </div>
      )}

      {/* Main content: 1fr canvas + 260px sidebar */}
      <div className="flex-1 flex gap-3 overflow-hidden">
        {/* ── Canvas side (1fr) ── */}
        <div className="flex-1 min-w-0 flex flex-col gap-2">
          <CephCanvas
            imageUrl={analysis.xrayFileUrl ?? null}
            imageWidth={imageSize.w}
            imageHeight={imageSize.h}
            landmarks={landmarks}
            onLandmarksChange={handleLandmarksChange}
            selectedKey={selectedKey}
            onSelectKey={setSelectedKey}
            showPlanes={showPlanes}
            showSimulation={showSim}
            simulationScenario={simScenario}
            exportRef={canvasExportRef}
          />

          {/* Info bar */}
          <div className="flex-shrink-0 flex items-center gap-3 text-[10px] px-1 flex-wrap" style={{ color: "#94a3b8" }}>
            <span className="font-mono">
              <span className="font-bold" style={{ color: placedCount >= 20 ? "#22c55e" : placedCount > 10 ? "#f59e0b" : "#64748b" }}>
                {placedCount}
              </span>/{totalCount} نقطة
            </span>
            <span>·</span>
            <div className="flex items-center gap-1">
              <span>معيار الصورة:</span>
              <input
                type="number"
                value={pixelsPerMm ?? ''}
                min={0} step={0.01}
                onChange={e => setPixelsPerMm(e.target.value ? +e.target.value : null)}
                placeholder="px/mm"
                className="w-16 text-[10px] px-1.5 py-0.5 border rounded focus:outline-none focus:ring-1 focus:ring-[#3d7ab5]"
                style={{ borderColor: "#dce8f5", color: "#0d2137" }}
                dir="ltr"
              />
              <span>px/mm</span>
            </div>
            {selectedKey && (
              <>
                <span>·</span>
                <span className="font-semibold" style={{ color: "#3d7ab5" }}>
                  ▶ انقر لوضع: {LANDMARK_DEFS[selectedKey]?.nameAr ?? selectedKey}
                </span>
              </>
            )}
          </div>
        </div>

        {/* ── Right panel (260px sidebar) ── */}
        <div className="w-[260px] flex-shrink-0 flex flex-col overflow-hidden rounded-xl border shadow-sm bg-white"
          style={{ borderColor: "#e8f0f9", boxShadow: "0 1px 3px rgba(13,33,55,0.06)" }}>
          {/* Tab bar: border-bottom 2px #e8f0f9, active tab: text #3d7ab5, font-weight 700 */}
          <div className="flex flex-shrink-0" style={{ borderBottom: "2px solid #e8f0f9" }}>
            {([
              { key: 'landmarks', label: 'النقاط' },
              { key: 'report',    label: 'القياسات' },
              { key: 'diagnosis', label: 'التشخيص' },
            ] as { key: RightTab; label: string }[]).map(t => (
              <button key={t.key} onClick={() => setRightTab(t.key)}
                className="flex-1 py-2.5 text-[11px] border-b-2 transition -mb-[2px]"
                style={rightTab === t.key
                  ? { color: "#3d7ab5", fontWeight: 700, borderBottomColor: "#3d7ab5" }
                  : { color: "#94a3b8", fontWeight: 500, borderBottomColor: "transparent" }
                }>
                {t.label}
              </button>
            ))}
          </div>

          <div className="flex-1 overflow-hidden flex flex-col">
            {/* ── Landmarks tab ── */}
            {rightTab === 'landmarks' && (
              <div className="flex-1 overflow-y-auto p-3 space-y-2">
                {LANDMARK_GROUPS.map(group => (
                  <div key={group.key}>
                    <p className="text-[9px] font-bold uppercase tracking-widest px-1 mb-1" style={{ color: "#94a3b8" }}>
                      {group.label}
                    </p>
                    <div className="space-y-0.5">
                      {group.keys.map(key => {
                        const def    = LANDMARK_DEFS[key];
                        const placed = lmMap[key];
                        const isSel  = selectedKey === key;
                        return (
                          <button key={key} onClick={() => setSelectedKey(isSel ? null : key)}
                            className="w-full flex items-center gap-2 px-2 py-1.5 rounded-lg text-[11px] transition text-start"
                            style={isSel
                              ? { backgroundColor: "#3d7ab510", border: "1px solid #3d7ab530", color: "#3d7ab5" }
                              : placed
                                ? { color: "#0d2137" }
                                : { color: "#94a3b8" }
                            }
                          >
                            <div className="w-2 h-2 rounded-full flex-shrink-0 border"
                              style={{
                                backgroundColor: placed ? def?.color : 'transparent',
                                borderColor: placed ? def?.color : '#dce8f5',
                              }} />
                            <span className="font-mono font-bold text-[9px] w-5" style={{ color: "#64748b" }}>{key}</span>
                            <span className="flex-1 truncate">{def?.nameAr}</span>
                            {placed?.isAiPlaced && (
                              <span className="text-[8px] font-bold" style={{ color: "#a855f7" }}>AI</span>
                            )}
                            {placed?.confidence !== undefined && (
                              <span className="text-[8px] font-mono" style={{ color: "#94a3b8" }}>
                                {Math.round(placed.confidence * 100)}%
                              </span>
                            )}
                            {isSel && <ChevronRight className="w-3 h-3 flex-shrink-0" />}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* ── Report tab ── */}
            {rightTab === 'report' && (
              <div className="flex-1 overflow-hidden p-3">
                <AnalysisReport
                  measurements={activeReportData}
                  diagnosis={null}
                  patientName={analysis.patientName}
                  analysisDate={analysis.analysisDate}
                  calibrated={pixelsPerMm !== null && pixelsPerMm > 0}
                />
              </div>
            )}

            {/* ── Diagnosis tab ── */}
            {rightTab === 'diagnosis' && (
              <div className="flex-1 overflow-hidden p-3">
                <AnalysisReport
                  measurements={activeReportData}
                  diagnosis={diagnosis ?? { doctorApproved: false }}
                  onDiagnosisChange={handleDiagnosisChange}
                  patientName={analysis.patientName}
                  analysisDate={analysis.analysisDate}
                  calibrated={pixelsPerMm !== null && pixelsPerMm > 0}
                  defaultGroup="steiner"
                />
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Comparison Dialog ── */}
      <CephComparison
        orthoCaseId={analysis.orthoCaseId}
        open={showComparison}
        onClose={() => setShowComparison(false)}
      />
    </div>
  );
}
