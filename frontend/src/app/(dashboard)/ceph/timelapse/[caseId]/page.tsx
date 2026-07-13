"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowRight, BadgeCheck, Check, ChevronLeft, ChevronRight, CircleAlert, ImageIcon, Loader2, Pause, Play } from "lucide-react";
import api from "@/lib/api";
import { buildCephTimelapseFrames } from "@/lib/cephTimelapse";
import { cn, formatArabicDate } from "@/lib/utils";
import { resolveImageUrl } from "@/hooks/useClinicBranding";
import type { CephAnalysisList } from "@/types/ceph";
import type { OrthoPhoto } from "@/types/ortho";

export default function CephTimelapsePage() {
  const { caseId } = useParams<{ caseId: string }>();
  const [analyses, setAnalyses] = useState<CephAnalysisList[]>([]);
  const [photos, setPhotos] = useState<OrthoPhoto[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [currentIndex, setCurrentIndex] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [intervalMs, setIntervalMs] = useState(1600);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    Promise.allSettled([
      api.get<CephAnalysisList[]>(`/api/ceph?orthoCaseId=${caseId}`),
      api.get<OrthoPhoto[]>(`/api/ortho-cases/${caseId}/photos`),
    ])
      .then(([analysisResponse, photoResponse]) => {
        if (!active) return;
        if (analysisResponse.status === "rejected" && photoResponse.status === "rejected") {
          setError(true);
          return;
        }
        const nextAnalyses = analysisResponse.status === "fulfilled" ? analysisResponse.value.data : [];
        const nextPhotos = photoResponse.status === "fulfilled" ? photoResponse.value.data : [];
        setAnalyses(nextAnalyses);
        setPhotos(nextPhotos);
        const nextFrames = buildCephTimelapseFrames(nextAnalyses, nextPhotos);
        setSelectedIds(new Set(nextFrames.map(frame => frame.id)));
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [caseId]);

  const frames = useMemo(() => buildCephTimelapseFrames(analyses, photos), [analyses, photos]);
  const selectedFrames = useMemo(() => frames.filter(frame => selectedIds.has(frame.id)), [frames, selectedIds]);
  const current = selectedFrames[currentIndex] ?? null;

  useEffect(() => {
    if (currentIndex >= selectedFrames.length) setCurrentIndex(Math.max(0, selectedFrames.length - 1));
    if (selectedFrames.length < 2) setPlaying(false);
  }, [currentIndex, selectedFrames.length]);

  useEffect(() => {
    if (!playing || selectedFrames.length < 2) return;
    const timer = window.setInterval(() => {
      setCurrentIndex(index => (index + 1) % selectedFrames.length);
    }, intervalMs);
    return () => window.clearInterval(timer);
  }, [intervalMs, playing, selectedFrames.length]);

  const patientName = analyses[0]?.patientName ?? "حالة التقويم";
  const caseNumber = analyses[0]?.caseNumber;
  const move = (offset: number) => {
    if (!selectedFrames.length) return;
    setPlaying(false);
    setCurrentIndex(index => (index + offset + selectedFrames.length) % selectedFrames.length);
  };
  const toggleFrame = (id: string) => {
    setPlaying(false);
    setCurrentIndex(0);
    setSelectedIds(currentIds => {
      const next = new Set(currentIds);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  if (loading) return <div className="grid h-72 place-items-center"><Loader2 className="h-8 w-8 animate-spin text-clinic-blue" /></div>;

  return (
    <div className="min-h-[calc(100vh-4rem)] bg-[#f4f7fb]">
      <header className="border-b border-gray-200 bg-white px-4 py-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex min-w-0 items-center gap-3">
            <Link href="/ceph" className="rounded border border-gray-200 p-2 text-gray-500 hover:bg-gray-50" title="العودة إلى السيفالومتري"><ArrowRight className="h-4 w-4" /></Link>
            <div><h1 className="text-base font-extrabold text-gray-900">Timelapse السجلات السريرية</h1><p className="text-xs text-gray-500">{patientName}{caseNumber ? ` | ${caseNumber}` : ""}</p></div>
          </div>
          <Link href={`/ceph/case/${caseId}`} className="rounded border border-blue-200 bg-blue-50 px-3 py-2 text-xs font-bold text-clinic-blue">مراجعة الحالة</Link>
        </div>
      </header>

      {error ? <div className="m-4 flex items-center gap-2 border border-red-200 bg-red-50 p-4 text-sm text-red-700"><CircleAlert className="h-5 w-5" />تعذر تحميل سجلات الحالة.</div> : (
        <div className="grid gap-3 p-4 xl:grid-cols-[minmax(0,1fr)_320px]">
          <main className="min-w-0 border border-gray-200 bg-white">
            {current ? (
              <>
                <div className="relative aspect-[16/10] min-h-[320px] bg-gray-950">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={resolveImageUrl(current.imageUrl)} alt={`${current.label} بتاريخ ${formatArabicDate(current.date)}`} className="h-full w-full object-contain" />
                  <div className="absolute inset-x-0 bottom-0 flex flex-wrap items-end justify-between gap-2 bg-black/70 px-4 py-3 text-white">
                    <div><p className="text-sm font-bold">{current.label}</p><p className="mt-0.5 text-xs text-white/75">{formatArabicDate(current.date)} | سجل {currentIndex + 1} من {selectedFrames.length}</p></div>
                    {current.approved && <span className="inline-flex items-center gap-1 rounded bg-emerald-600 px-2 py-1 text-[10px] font-bold"><BadgeCheck className="h-3.5 w-3.5" />معتمد</span>}
                  </div>
                </div>
                <div className="border-t border-gray-200 p-3">
                  <div className="flex flex-wrap items-center justify-center gap-2">
                    <button type="button" onClick={() => move(-1)} title="السجل السابق" aria-label="السجل السابق" className="rounded border border-gray-200 p-2 text-gray-600 hover:bg-gray-50"><ChevronRight className="h-4 w-4" /></button>
                    <button type="button" disabled={selectedFrames.length < 2} onClick={() => setPlaying(value => !value)} className="inline-flex h-9 min-w-28 items-center justify-center gap-2 rounded bg-clinic-blue px-4 text-xs font-bold text-white disabled:opacity-50">{playing ? <Pause className="h-4 w-4" /> : <Play className="h-4 w-4" />}{playing ? "إيقاف" : "تشغيل"}</button>
                    <button type="button" onClick={() => move(1)} title="السجل التالي" aria-label="السجل التالي" className="rounded border border-gray-200 p-2 text-gray-600 hover:bg-gray-50"><ChevronLeft className="h-4 w-4" /></button>
                    <label className="ms-2 flex items-center gap-2 text-xs text-gray-500">السرعة<select aria-label="سرعة Timelapse" value={intervalMs} onChange={event => setIntervalMs(Number(event.target.value))} className="h-9 rounded border border-gray-200 bg-white px-2"><option value={2400}>بطيء</option><option value={1600}>متوسط</option><option value={900}>سريع</option></select></label>
                  </div>
                  <input aria-label="موضع السجل الزمني" type="range" min={0} max={Math.max(0, selectedFrames.length - 1)} value={currentIndex} onChange={event => { setPlaying(false); setCurrentIndex(Number(event.target.value)); }} className="mt-3 w-full accent-clinic-blue" />
                  <p className="mt-2 text-center text-[10px] text-gray-400">تُعرض السجلات المحفوظة بترتيبها الحقيقي دون إنشاء إطارات أو حركة علاجية وسيطة.</p>
                </div>
              </>
            ) : <div className="grid min-h-[520px] place-items-center p-8 text-center"><div><ImageIcon className="mx-auto h-10 w-10 text-gray-300" /><p className="mt-3 text-sm font-bold text-gray-600">لا توجد صورتان محددتان للتشغيل الزمني</p></div></div>}
          </main>

          <aside className="border border-gray-200 bg-white">
            <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2"><h2 className="text-xs font-extrabold text-gray-800">قائمة السجلات</h2><span className="font-mono text-[10px] text-gray-400" dir="ltr">{selectedFrames.length}/{frames.length}</span></div>
            <div className="max-h-[680px] divide-y divide-gray-100 overflow-y-auto">
              {frames.map(frame => {
                const selected = selectedIds.has(frame.id);
                return <label key={frame.id} className={cn("flex cursor-pointer gap-3 px-3 py-3 transition hover:bg-gray-50", current?.id === frame.id && "bg-blue-50")}>
                  <input type="checkbox" checked={selected} onChange={() => toggleFrame(frame.id)} className="mt-1 h-4 w-4 accent-clinic-blue" />
                  <span className="min-w-0 flex-1"><span className="flex items-center gap-1.5 text-xs font-bold text-gray-800">{frame.label}{frame.approved && <Check className="h-3.5 w-3.5 text-emerald-600" />}</span><span className="mt-1 block text-[10px] text-gray-500">{formatArabicDate(frame.date)} | {frame.source === "ceph" ? "تحليل سيفالو" : "صورة سريرية"}</span></span>
                </label>;
              })}
              {frames.length === 0 && <p className="p-8 text-center text-xs text-gray-400">لا توجد سجلات مصورة مؤرخة لهذه الحالة.</p>}
            </div>
          </aside>
        </div>
      )}
    </div>
  );
}
