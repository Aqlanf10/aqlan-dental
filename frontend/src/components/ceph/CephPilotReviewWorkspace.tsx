"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Check,
  CheckCircle2,
  EyeOff,
  Loader2,
  LockKeyhole,
  Redo2,
  Save,
  Send,
  ShieldCheck,
  Undo2,
} from "lucide-react";
import api from "@/lib/api";
import { extractErrorMessage } from "@/lib/errors";
import { PILOT_CORE_LANDMARKS, PILOT_LANDMARK_BY_KEY, PILOT_LANDMARKS } from "@/lib/cephPilotLandmarks";
import type { CephLandmark } from "@/types/ceph";
import type {
  CephPilotCase,
  CephPilotContourDecision,
  CephPilotProject,
  CephPilotReviewPoint,
  CephPilotReviewSession,
} from "@/types/cephPilot";
import { CephCanvas } from "./CephCanvas";

type DecisionMap = Record<string, CephPilotReviewPoint>;

const contourOptions: ReadonlyArray<[CephPilotContourDecision, string]> = [
  ["NotApplicable", "غير منطبق / نقطة منشأة"],
  ["SingleContour", "محيط واحد واضح"],
  ["ReceptorSideContour", "المحيط الأقرب للمستقبل"],
];

const inputClass = "h-10 w-full rounded-md border border-gray-300 bg-white px-3 text-sm outline-none focus:border-clinic-blue focus:ring-2 focus:ring-blue-100";

function decisionMap(points: CephPilotReviewPoint[]): DecisionMap {
  return Object.fromEntries(points.map(point => [point.landmarkKey, point]));
}

function cloneDecisions(value: DecisionMap): DecisionMap {
  return Object.fromEntries(Object.entries(value).map(([key, point]) => [key, { ...point }]));
}

function statusCode(error: unknown): number | undefined {
  return (error as { response?: { status?: number } })?.response?.status;
}

export default function CephPilotReviewWorkspace() {
  const [projects, setProjects] = useState<CephPilotProject[]>([]);
  const [cases, setCases] = useState<CephPilotCase[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState("");
  const [selectedCaseId, setSelectedCaseId] = useState("");
  const [session, setSession] = useState<CephPilotReviewSession | null>(null);
  const [decisions, setDecisions] = useState<DecisionMap>({});
  const [selectedKey, setSelectedKey] = useState<string | null>(PILOT_CORE_LANDMARKS[0].key);
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const undoRef = useRef<DecisionMap[]>([]);
  const redoRef = useRef<DecisionMap[]>([]);
  const [historyVersion, setHistoryVersion] = useState(0);

  const assignedProjects = useMemo(
    () => projects.filter(project => project.myReviewerSlot === "A" || project.myReviewerSlot === "B"),
    [projects],
  );
  const selectedProject = assignedProjects.find(project => project.id === selectedProjectId) ?? null;
  const selectedCase = cases.find(item => item.id === selectedCaseId) ?? null;
  const readOnly = session?.status === "Submitted";
  const decidedCoreCount = PILOT_CORE_LANDMARKS.filter(item => decisions[item.key]).length;
  const coreComplete = decidedCoreCount === PILOT_CORE_LANDMARKS.length;
  const selectedDefinition = selectedKey ? PILOT_LANDMARK_BY_KEY.get(selectedKey) : undefined;
  const selectedDecision = selectedKey ? decisions[selectedKey] : undefined;

  const landmarks = useMemo<CephLandmark[]>(() => PILOT_LANDMARKS.flatMap(definition => {
    const point = decisions[definition.key];
    if (!point || point.visibility !== "Visible" || point.xCoordPx == null || point.yCoordPx == null) return [];
    return [{
      key: definition.key,
      name: definition.key,
      nameAr: definition.nameAr,
      x: point.xCoordPx,
      y: point.yCoordPx,
      isAiPlaced: false,
      isReviewed: true,
      placementSource: "manual" as const,
      group: definition.group,
    }];
  }), [decisions]);

  const clearHistory = useCallback(() => {
    undoRef.current = [];
    redoRef.current = [];
    setHistoryVersion(current => current + 1);
  }, []);

  const replaceFromSession = useCallback((next: CephPilotReviewSession) => {
    setSession(next);
    setDecisions(decisionMap(next.points));
    setDirty(false);
    clearHistory();
    const firstUnresolved = PILOT_CORE_LANDMARKS.find(item => !next.points.some(point => point.landmarkKey === item.key));
    setSelectedKey(firstUnresolved?.key ?? PILOT_CORE_LANDMARKS[0].key);
  }, [clearHistory]);

  const loadImage = useCallback(async (caseId: string) => {
    const response = await api.get<Blob>(`/api/ceph-pilot/cases/${encodeURIComponent(caseId)}/image`, { responseType: "blob" });
    const nextUrl = URL.createObjectURL(response.data);
    setImageUrl(current => {
      if (current) URL.revokeObjectURL(current);
      return nextUrl;
    });
  }, []);

  const loadReview = useCallback(async (caseId: string) => {
    setBusy(true);
    setError("");
    setNotice("");
    setSession(null);
    setDecisions({});
    setDirty(false);
    clearHistory();
    setImageUrl(current => {
      if (current) URL.revokeObjectURL(current);
      return null;
    });
    try {
      const response = await api.get<CephPilotReviewSession>(`/api/ceph-pilot/cases/${encodeURIComponent(caseId)}/review`);
      replaceFromSession(response.data);
      await loadImage(caseId);
    } catch (requestError) {
      if (statusCode(requestError) !== 404) {
        setError(extractErrorMessage(requestError, "تعذر فتح جلسة المراجعة المعمّاة."));
      }
    } finally {
      setBusy(false);
    }
  }, [clearHistory, loadImage, replaceFromSession]);

  const loadCases = useCallback(async (projectId: string) => {
    if (!projectId) {
      setCases([]);
      setSelectedCaseId("");
      return;
    }
    const response = await api.get<CephPilotCase[]>(`/api/ceph-pilot/projects/${encodeURIComponent(projectId)}/cases`);
    setCases(response.data);
    const initial = response.data.find(item => item.status !== "Uploaded") ?? response.data[0];
    setSelectedCaseId(current => response.data.some(item => item.id === current) ? current : initial?.id ?? "");
  }, []);

  useEffect(() => {
    let active = true;
    setLoading(true);
    api.get<CephPilotProject[]>("/api/ceph-pilot/projects")
      .then(response => {
        if (!active) return;
        setProjects(response.data);
        const available = response.data.find(project => project.myReviewerSlot === "A" || project.myReviewerSlot === "B");
        setSelectedProjectId(available?.id ?? "");
      })
      .catch(requestError => active && setError(extractErrorMessage(requestError, "تعذر تحميل مشاريع المراجعة.")))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, []);

  useEffect(() => {
    void loadCases(selectedProjectId).catch(requestError => {
      setError(extractErrorMessage(requestError, "تعذر تحميل حالات المشروع."));
    });
  }, [loadCases, selectedProjectId]);

  useEffect(() => {
    if (!selectedCaseId) {
      setSession(null);
      return;
    }
    void loadReview(selectedCaseId);
  }, [loadReview, selectedCaseId]);

  useEffect(() => () => {
    if (imageUrl) URL.revokeObjectURL(imageUrl);
  }, [imageUrl]);

  const applyDecisionChange = useCallback((change: (current: DecisionMap) => DecisionMap) => {
    if (readOnly) return;
    setDecisions(current => {
      const next = change(current);
      undoRef.current = [...undoRef.current.slice(-99), cloneDecisions(current)];
      redoRef.current = [];
      setHistoryVersion(version => version + 1);
      return next;
    });
    setDirty(true);
  }, [readOnly]);

  const advanceAfter = useCallback((key: string, nextDecisions: DecisionMap) => {
    const currentIndex = PILOT_LANDMARKS.findIndex(item => item.key === key);
    const later = PILOT_LANDMARKS.slice(currentIndex + 1).find(item => !nextDecisions[item.key]);
    const earlier = PILOT_LANDMARKS.slice(0, currentIndex).find(item => !nextDecisions[item.key]);
    setSelectedKey((later ?? earlier)?.key ?? key);
  }, []);

  const handleLandmarksChange = useCallback((nextLandmarks: CephLandmark[]) => {
    if (readOnly) return;
    const nextByKey = new Map(nextLandmarks.map(item => [item.key, item]));
    const placedSelected = selectedKey && !decisions[selectedKey] && nextByKey.has(selectedKey);
    let resulting: DecisionMap = decisions;
    applyDecisionChange(current => {
      const next = cloneDecisions(current);
      for (const [key, point] of Object.entries(current)) {
        if (point.visibility === "Visible" && !nextByKey.has(key)) delete next[key];
      }
      for (const landmark of nextLandmarks) {
        const definition = PILOT_LANDMARK_BY_KEY.get(landmark.key);
        if (!definition) continue;
        next[landmark.key] = {
          landmarkKey: landmark.key,
          isCore: definition.core,
          visibility: "Visible",
          xCoordPx: landmark.x,
          yCoordPx: landmark.y,
          contourDecision: current[landmark.key]?.contourDecision === "Unresolvable"
            ? "NotApplicable"
            : current[landmark.key]?.contourDecision ?? "NotApplicable",
        };
      }
      resulting = next;
      return next;
    });
    if (placedSelected && selectedKey) advanceAfter(selectedKey, resulting);
  }, [advanceAfter, applyDecisionChange, decisions, readOnly, selectedKey]);

  const markNotVisible = () => {
    if (!selectedDefinition) return;
    let resulting = decisions;
    applyDecisionChange(current => {
      const next = cloneDecisions(current);
      next[selectedDefinition.key] = {
        landmarkKey: selectedDefinition.key,
        isCore: selectedDefinition.core,
        visibility: "NotVisible",
        xCoordPx: null,
        yCoordPx: null,
        contourDecision: "Unresolvable",
      };
      resulting = next;
      return next;
    });
    advanceAfter(selectedDefinition.key, resulting);
  };

  const changeContour = (contourDecision: CephPilotContourDecision) => {
    if (!selectedKey || !selectedDecision || selectedDecision.visibility !== "Visible") return;
    applyDecisionChange(current => ({
      ...current,
      [selectedKey]: { ...current[selectedKey], contourDecision },
    }));
  };

  const undo = useCallback(() => {
    const previous = undoRef.current.pop();
    if (!previous || readOnly) return;
    redoRef.current.push(cloneDecisions(decisions));
    setDecisions(previous);
    setDirty(true);
    setHistoryVersion(current => current + 1);
  }, [decisions, readOnly]);

  const redo = useCallback(() => {
    const next = redoRef.current.pop();
    if (!next || readOnly) return;
    undoRef.current.push(cloneDecisions(decisions));
    setDecisions(next);
    setDirty(true);
    setHistoryVersion(current => current + 1);
  }, [decisions, readOnly]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target && (["INPUT", "TEXTAREA", "SELECT", "BUTTON"].includes(target.tagName) || target.isContentEditable)) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
        event.preventDefault();
        if (event.shiftKey) redo(); else undo();
        return;
      }
      if (!selectedKey || readOnly || !["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.key)) return;
      const selected = decisions[selectedKey];
      if (!selected || selected.visibility !== "Visible" || selected.xCoordPx == null || selected.yCoordPx == null) return;
      event.preventDefault();
      const step = event.shiftKey ? 2 : 0.5;
      const dx = event.key === "ArrowLeft" ? -step : event.key === "ArrowRight" ? step : 0;
      const dy = event.key === "ArrowUp" ? -step : event.key === "ArrowDown" ? step : 0;
      applyDecisionChange(current => ({
        ...current,
        [selectedKey]: {
          ...current[selectedKey],
          xCoordPx: Math.max(0, Math.min(session?.imageWidth ?? Infinity, selected.xCoordPx! + dx)),
          yCoordPx: Math.max(0, Math.min(session?.imageHeight ?? Infinity, selected.yCoordPx! + dy)),
        },
      }));
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [applyDecisionChange, decisions, readOnly, redo, selectedKey, session?.imageHeight, session?.imageWidth, undo]);

  const startReview = async () => {
    if (!selectedCase) return;
    setBusy(true);
    setError("");
    try {
      const response = await api.post<CephPilotReviewSession>(`/api/ceph-pilot/cases/${encodeURIComponent(selectedCase.id)}/review/start`);
      replaceFromSession(response.data);
      await loadImage(selectedCase.id);
      setNotice(`بدأت مراجعة ${selectedCase.caseCode} بصورة معمّاة عن المراجع الآخر ونتائج AI وWebCeph.`);
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر بدء المراجعة المعمّاة."));
    } finally {
      setBusy(false);
    }
  };

  const saveReview = async () => {
    if (!session || readOnly) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const points = PILOT_LANDMARKS.flatMap(item => decisions[item.key] ? [decisions[item.key]] : []);
      const response = await api.put<CephPilotReviewSession>(`/api/ceph-pilot/cases/${encodeURIComponent(session.caseId)}/review`, {
        expectedRevision: session.revision,
        points: points.map(point => ({
          landmarkKey: point.landmarkKey,
          visibility: point.visibility,
          xCoordPx: point.xCoordPx ?? null,
          yCoordPx: point.yCoordPx ?? null,
          contourDecision: point.contourDecision,
        })),
      });
      replaceFromSession(response.data);
      setNotice(`حُفظت مسودة ${session.caseCode}. الإحداثيات لا تظهر للمراجع الآخر.`);
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر حفظ مسودة المراجعة."));
    } finally {
      setBusy(false);
    }
  };

  const submitReview = async () => {
    if (!session || readOnly || dirty || !coreComplete) return;
    if (!window.confirm("بعد التسليم لن تستطيع تعديل هذه الجلسة. هل تريد المتابعة؟")) return;
    setBusy(true);
    setError("");
    try {
      const response = await api.post<CephPilotReviewSession>(`/api/ceph-pilot/cases/${encodeURIComponent(session.caseId)}/review/submit`, {
        expectedRevision: session.revision,
      });
      replaceFromSession(response.data);
      await loadCases(selectedProjectId);
      setNotice("تم تسليم المراجعة وقفلها. ستبقى إحداثيات المراجع الآخر مخفية.");
    } catch (requestError) {
      setError(extractErrorMessage(requestError, "تعذر تسليم المراجعة."));
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return <div className="flex min-h-80 items-center justify-center text-sm text-gray-500"><Loader2 className="me-2 h-5 w-5 animate-spin" />جاري تحميل المراجعات...</div>;
  }

  if (!assignedProjects.length) {
    return (
      <div className="mx-auto max-w-3xl border border-amber-200 bg-amber-50 p-5 text-amber-900" dir="rtl">
        <div className="flex items-start gap-3"><LockKeyhole className="mt-0.5 h-5 w-5" /><div><h1 className="font-bold">لا توجد مراجعة مسندة لهذا الحساب</h1><p className="mt-1 text-sm">يجب أن يعيّنك المدير Reviewer A أو Reviewer B ثم يفتح المشروع للمراجعة.</p></div></div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1600px] space-y-4" dir="rtl">
      <header className="flex flex-col gap-3 border-b border-gray-200 pb-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-extrabold text-gray-900"><ShieldCheck className="h-5 w-5 text-emerald-700" />المراجعة السيفالومترية المعمّاة</h1>
          <p className="mt-1 text-sm text-gray-500">ADP-LM-LAT-v1.0 · لا تظهر نقاط المراجع الآخر أو AI أو WebCeph أثناء التحديد.</p>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <label className="min-w-60 text-xs font-bold text-gray-700">المشروع<select value={selectedProjectId} onChange={event => setSelectedProjectId(event.target.value)} className={`${inputClass} mt-1`}>{assignedProjects.map(project => <option key={project.id} value={project.id}>{project.code} · Reviewer {project.myReviewerSlot}</option>)}</select></label>
          <label className="min-w-48 text-xs font-bold text-gray-700">الحالة<select value={selectedCaseId} onChange={event => setSelectedCaseId(event.target.value)} className={`${inputClass} mt-1`}><option value="">اختر الحالة</option>{cases.map(item => <option key={item.id} value={item.id}>{item.caseCode} · {item.status}</option>)}</select></label>
          <Link href="/ceph" className="inline-flex h-10 items-center gap-2 rounded-md border border-gray-300 px-3 text-xs font-bold text-gray-700 hover:bg-gray-50"><ArrowRight className="h-4 w-4" />السيفالو</Link>
        </div>
      </header>

      {error ? <div role="alert" className="flex items-start gap-2 border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{error}</div> : null}
      {notice ? <div role="status" className="flex items-start gap-2 border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900"><CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />{notice}</div> : null}

      {!selectedCase ? <div className="border border-gray-200 p-8 text-center text-sm text-gray-500">لا توجد حالة جاهزة في هذا المشروع.</div> : null}

      {selectedCase && !session ? (
        <div className="flex min-h-72 flex-col items-center justify-center border border-gray-200 bg-white p-8 text-center">
          {busy ? <Loader2 className="mb-3 h-7 w-7 animate-spin text-clinic-blue" /> : <LockKeyhole className="mb-3 h-7 w-7 text-gray-500" />}
          <h2 className="font-bold text-gray-900">{selectedCase.caseCode}</h2>
          <p className="mt-2 max-w-lg text-sm text-gray-500">بدء الجلسة يثبت هوية المراجع ونسخة التعريف والأداة. لن تُحمّل نقاط أي مصدر آخر.</p>
          <button type="button" disabled={busy || !selectedProject || !["ReadyForAnnotation", "AnnotationInProgress"].includes(selectedProject.status) || selectedCase.status === "Uploaded"} onClick={() => void startReview()} className="mt-4 inline-flex h-10 items-center gap-2 rounded-md bg-clinic-blue px-4 text-sm font-bold text-white disabled:opacity-45"><ArrowLeft className="h-4 w-4" />بدء المراجعة المعمّاة</button>
        </div>
      ) : null}

      {session ? (
        <div className="grid min-h-[720px] gap-4 xl:grid-cols-[260px_minmax(0,1fr)_320px]">
          <aside className="order-3 min-h-0 border border-gray-200 bg-white xl:order-none">
            <div className="border-b border-gray-200 p-3">
              <div className="flex items-center justify-between gap-2 text-sm font-bold"><span>{session.caseCode}</span><span className="text-xs text-gray-500">Reviewer {session.reviewerSlot}</span></div>
              <div className="mt-2 h-2 overflow-hidden rounded bg-gray-100"><div className="h-full bg-emerald-600" style={{ width: `${(decidedCoreCount / PILOT_CORE_LANDMARKS.length) * 100}%` }} /></div>
              <p className="mt-1 text-xs text-gray-500">{decidedCoreCount}/24 نقطة أساسية محسومة</p>
            </div>
            <div className="max-h-[650px] overflow-y-auto p-2">
              {PILOT_LANDMARKS.map(definition => {
                const point = decisions[definition.key];
                const active = selectedKey === definition.key;
                return (
                  <button key={definition.key} type="button" onClick={() => setSelectedKey(definition.key)} className={`mb-1 flex h-10 w-full items-center gap-2 rounded px-2 text-start text-xs ${active ? "bg-blue-50 text-blue-900 ring-1 ring-blue-200" : "hover:bg-gray-50"}`}>
                    <span className={`flex h-6 w-9 shrink-0 items-center justify-center rounded font-mono font-bold ${point?.visibility === "Visible" ? "bg-emerald-100 text-emerald-800" : point?.visibility === "NotVisible" ? "bg-amber-100 text-amber-800" : "bg-gray-100 text-gray-500"}`}>{point?.visibility === "Visible" ? <Check className="h-3.5 w-3.5" /> : point?.visibility === "NotVisible" ? <EyeOff className="h-3.5 w-3.5" /> : definition.key}</span>
                    <span className="min-w-0 flex-1 truncate">{definition.key} · {definition.nameAr}</span>
                    {!definition.core ? <span className="text-[10px] text-gray-400">اختياري</span> : null}
                  </button>
                );
              })}
            </div>
          </aside>

          <main className="order-1 relative min-w-0 overflow-hidden border border-gray-200 bg-gray-950 xl:order-none">
            {imageUrl ? (
              <CephCanvas
                imageUrl={imageUrl}
                imageWidth={session.imageWidth}
                imageHeight={session.imageHeight}
                landmarks={landmarks}
                onLandmarksChange={handleLandmarksChange}
                selectedKey={selectedKey}
                onSelectKey={setSelectedKey}
                showPlanes={false}
                showTracing={false}
                showSimulation={false}
                simulationScenario=""
                pixelsPerMm={session.mmPerPixel ? 1 / session.mmPerPixel : null}
              />
            ) : <div className="flex h-full items-center justify-center text-sm text-gray-300"><Loader2 className="me-2 h-5 w-5 animate-spin" />تحميل الصورة المنزوعة الهوية...</div>}
            {readOnly ? <div className="pointer-events-none absolute inset-x-0 top-2 z-20 mx-auto w-fit border border-emerald-300 bg-emerald-50 px-3 py-2 text-xs font-bold text-emerald-900"><LockKeyhole className="me-1 inline h-3.5 w-3.5" />الجلسة مسلّمة ومقفلة</div> : null}
          </main>

          <aside className="order-2 space-y-4 border border-gray-200 bg-white p-4 xl:order-none">
            <section>
              <div className="flex items-start justify-between gap-3">
                <div><p className="font-mono text-xl font-extrabold text-clinic-blue">{selectedDefinition?.key ?? "—"}</p><h2 className="text-sm font-bold text-gray-900">{selectedDefinition?.nameAr ?? "اختر نقطة"}</h2></div>
                {selectedDefinition ? <span className={`rounded px-2 py-1 text-[10px] font-bold ${selectedDefinition.core ? "bg-gray-900 text-white" : "bg-gray-100 text-gray-600"}`}>{selectedDefinition.core ? "أساسية" : "اختيارية"}</span> : null}
              </div>
              <p className="mt-3 border-t border-gray-100 pt-3 text-xs leading-6 text-gray-600">{selectedDefinition?.instructionAr}</p>
            </section>

            <section className="border-t border-gray-200 pt-4">
              <p className="mb-2 text-xs font-bold text-gray-700">قرار الرؤية</p>
              <div className="grid grid-cols-2 gap-2">
                <button type="button" disabled={readOnly || !selectedDefinition} onClick={markNotVisible} className={`inline-flex h-10 items-center justify-center gap-2 rounded-md border text-xs font-bold disabled:opacity-45 ${selectedDecision?.visibility === "NotVisible" ? "border-amber-400 bg-amber-50 text-amber-900" : "border-gray-300 text-gray-700"}`}><EyeOff className="h-4 w-4" />غير مرئية</button>
                <div className={`flex h-10 items-center justify-center gap-2 rounded-md border text-xs font-bold ${selectedDecision?.visibility === "Visible" ? "border-emerald-400 bg-emerald-50 text-emerald-900" : "border-gray-200 text-gray-400"}`}><CheckCircle2 className="h-4 w-4" />محددة</div>
              </div>
            </section>

            <section className="border-t border-gray-200 pt-4">
              <label className="text-xs font-bold text-gray-700">قرار المحيط<select disabled={readOnly || selectedDecision?.visibility !== "Visible"} value={selectedDecision?.visibility === "Visible" ? selectedDecision.contourDecision : "NotApplicable"} onChange={event => changeContour(event.target.value as CephPilotContourDecision)} className={`${inputClass} mt-1 disabled:bg-gray-50 disabled:text-gray-400`}>{contourOptions.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
              {selectedDecision?.visibility === "Visible" ? <p className="mt-2 font-mono text-xs text-gray-500">X {selectedDecision.xCoordPx?.toFixed(2)} · Y {selectedDecision.yCoordPx?.toFixed(2)} px</p> : null}
            </section>

            <section className="border-t border-gray-200 pt-4">
              <div className="flex gap-2" data-history-version={historyVersion}>
                <button type="button" title="تراجع" aria-label="تراجع" disabled={readOnly || undoRef.current.length === 0} onClick={undo} className="flex h-9 w-9 items-center justify-center rounded-md border border-gray-300 text-gray-700 disabled:opacity-35"><Undo2 className="h-4 w-4" /></button>
                <button type="button" title="إعادة" aria-label="إعادة" disabled={readOnly || redoRef.current.length === 0} onClick={redo} className="flex h-9 w-9 items-center justify-center rounded-md border border-gray-300 text-gray-700 disabled:opacity-35"><Redo2 className="h-4 w-4" /></button>
              </div>
              <p className="mt-2 text-[11px] leading-5 text-gray-500">الأسهم تحرك النقطة 0.5 px، ومع Shift تتحرك 2 px.</p>
            </section>

            <section className="space-y-2 border-t border-gray-200 pt-4">
              <button type="button" disabled={busy || readOnly || !dirty} onClick={() => void saveReview()} className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-md bg-gray-900 px-3 text-sm font-bold text-white disabled:opacity-40">{busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}حفظ المسودة</button>
              <button type="button" disabled={busy || readOnly || dirty || !coreComplete} onClick={() => void submitReview()} className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-md bg-emerald-700 px-3 text-sm font-bold text-white disabled:opacity-40"><Send className="h-4 w-4" />تسليم وقفل المراجعة</button>
              {!coreComplete ? <p className="text-[11px] text-amber-700">يلزم حسم {24 - decidedCoreCount} نقطة أساسية قبل التسليم.</p> : dirty ? <p className="text-[11px] text-amber-700">احفظ المسودة قبل التسليم.</p> : null}
            </section>
          </aside>
        </div>
      ) : null}
    </div>
  );
}
