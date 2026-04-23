"use client";
import { useEffect, useState, useMemo, useCallback } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  Brain, Calculator, Eye, EyeOff, Play, PlayCircle, ArrowRight,
  Save, CheckCircle2, ChevronRight, Loader2,
} from "lucide-react";
import type { CephAnalysis, CephLandmark, CephDiagnosis, AnalysisType } from "@/types/ceph";
import { ANALYSIS_GROUPS, ANALYSIS_TYPE_AR } from "@/types/ceph";
import { buildMeasurementList } from "@/lib/cephMath";
import { CephCanvas, LANDMARK_DEFS, LANDMARK_ORDER, SIMULATION_SCENARIOS } from "@/components/ceph/CephCanvas";
import { AnalysisReport } from "@/components/ceph/AnalysisReport";
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

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <Loader2 className="w-8 h-8 animate-spin text-clinic-teal" />
    </div>
  );
  if (!analysis) return <div className="text-center py-20 text-gray-400">التحليل غير موجود</div>;

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)] overflow-hidden">
      {/* ── Header ── */}
      <div className="flex-shrink-0 flex items-center justify-between py-3 px-1 gap-2">
        <div className="flex items-center gap-3 min-w-0">
          <Link href="/ceph"
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
          <button onClick={handleAiDetect} disabled={detecting}
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-lg bg-purple-600 text-white hover:bg-purple-700 disabled:opacity-60 transition">
            {detecting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Brain className="w-3.5 h-3.5" />}
            {detecting ? 'جارٍ الكشف...' : 'كشف AI'}
          </button>

          <button onClick={handleSaveAndCompute} disabled={saving || !landmarks.length}
            className={cn(
              "flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-lg transition",
              saveStatus === 'saved' ? "bg-green-600 text-white" :
              saveStatus === 'error' ? "bg-red-100 text-red-700 border border-red-300" :
              "bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60"
            )}>
            {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> :
             saveStatus === 'saved' ? <CheckCircle2 className="w-3.5 h-3.5" /> :
             <Calculator className="w-3.5 h-3.5" />}
            {saving ? 'حساب...' : saveStatus === 'saved' ? 'تم' : 'احسب'}
          </button>

          <div className="flex items-center gap-0.5 bg-gray-100 rounded-lg p-0.5">
            <button onClick={() => setShowPlanes(!showPlanes)}
              className={cn("p-1.5 rounded-md transition", showPlanes ? "bg-white shadow-sm text-clinic-teal" : "text-gray-400")}
              title="الخطوط التشريحية">
              {showPlanes ? <Eye className="w-3.5 h-3.5" /> : <EyeOff className="w-3.5 h-3.5" />}
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
      <div className="flex-1 flex gap-3 overflow-hidden">
        {/* ── Canvas side ── */}
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
          />

          {/* Info bar */}
          <div className="flex-shrink-0 flex items-center gap-3 text-[10px] text-gray-400 px-1 flex-wrap">
            <span className="font-mono">
              <span className={cn("font-bold", placedCount >= 20 ? "text-green-600" : placedCount > 10 ? "text-amber-500" : "text-gray-500")}>
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
                className="w-16 text-[10px] px-1.5 py-0.5 border border-gray-200 rounded text-gray-700 focus:outline-none focus:ring-1 focus:ring-clinic-teal"
                dir="ltr"
              />
              <span className="text-gray-300">px/mm</span>
            </div>
            {selectedKey && (
              <>
                <span>·</span>
                <span className="text-clinic-teal font-semibold">
                  ▶ انقر لوضع: {LANDMARK_DEFS[selectedKey]?.nameAr ?? selectedKey}
                </span>
              </>
            )}
          </div>
        </div>

        {/* ── Right panel ── */}
        <div className="w-80 flex-shrink-0 flex flex-col overflow-hidden rounded-xl border border-gray-200 shadow-sm bg-white">
          {/* Tab bar */}
          <div className="flex border-b border-gray-100 flex-shrink-0">
            {([
              { key: 'landmarks', label: 'النقاط' },
              { key: 'report',    label: 'القياسات' },
              { key: 'diagnosis', label: 'التشخيص' },
            ] as { key: RightTab; label: string }[]).map(t => (
              <button key={t.key} onClick={() => setRightTab(t.key)}
                className={cn(
                  "flex-1 py-2.5 text-[11px] font-semibold border-b-2 transition",
                  rightTab === t.key
                    ? "border-clinic-teal text-clinic-teal"
                    : "border-transparent text-gray-400 hover:text-gray-600"
                )}>
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
                    <p className="text-[9px] font-bold text-gray-400 uppercase tracking-widest px-1 mb-1">
                      {group.label}
                    </p>
                    <div className="space-y-0.5">
                      {group.keys.map(key => {
                        const def    = LANDMARK_DEFS[key];
                        const placed = lmMap[key];
                        const isSel  = selectedKey === key;
                        return (
                          <button key={key} onClick={() => setSelectedKey(isSel ? null : key)}
                            className={cn(
                              "w-full flex items-center gap-2 px-2 py-1.5 rounded-lg text-[11px] transition text-start",
                              isSel   ? "bg-clinic-teal/10 border border-clinic-teal/30 text-clinic-teal" :
                              placed  ? "hover:bg-gray-50 text-gray-700" :
                                        "hover:bg-gray-50 text-gray-400"
                            )}>
                            <div className="w-2 h-2 rounded-full flex-shrink-0 border"
                              style={{
                                backgroundColor: placed ? def?.color : 'transparent',
                                borderColor: placed ? def?.color : '#d1d5db',
                              }} />
                            <span className="font-mono font-bold text-[9px] w-5 text-gray-500">{key}</span>
                            <span className="flex-1 truncate">{def?.nameAr}</span>
                            {placed?.isAiPlaced && (
                              <span className="text-[8px] text-purple-400 font-bold">AI</span>
                            )}
                            {placed?.confidence !== undefined && (
                              <span className="text-[8px] text-gray-300 font-mono">
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

            {/* ── Report tab (scientist tabs) ── */}
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
    </div>
  );
}
