
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Hand, Maximize2, Ruler, Trash2, ZoomIn, ZoomOut } from "lucide-react";
import type { CephLandmark, CephMeasurement } from "@/types/ceph";
import { PA_LANDMARK_DEFS } from "@/lib/cephPa";
import { cn } from "@/lib/utils";

type Point = { x: number; y: number };
type View = { zoom: number; panX: number; panY: number };

interface Props {
  imageUrl: string | null;
  imageWidth: number;
  imageHeight: number;
  landmarks: CephLandmark[];
  onLandmarksChange: (landmarks: CephLandmark[]) => void;
  selectedKey: string | null;
  onSelectKey: (key: string | null) => void;
  showPlanes: boolean;
  showMeasurements: boolean;
  measurements: CephMeasurement[];
  pixelsPerMm: number | null;
  onCalibrate: (pixelsPerMm: number) => void;
  imageAdjustments: { brightness: number; contrast: number; inverted: boolean };
  onImageDimensions?: (width: number, height: number) => void;
}

const HIT_RADIUS = 12;

export function CephPaCanvas({
  imageUrl, imageWidth, imageHeight, landmarks, onLandmarksChange,
  selectedKey, onSelectKey, showPlanes, showMeasurements, measurements,
  pixelsPerMm, onCalibrate, imageAdjustments, onImageDimensions,
}: Props) {
  const wrapRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [size, setSize] = useState({ w: 1, h: 1 });
  const [image, setImage] = useState<HTMLImageElement | null>(null);
  const [view, setView] = useState<View>({ zoom: 1, panX: 0, panY: 0 });
  const [panMode, setPanMode] = useState(false);
  const [dragKey, setDragKey] = useState<string | null>(null);
  const [panStart, setPanStart] = useState<{ x: number; y: number; panX: number; panY: number } | null>(null);
  const [calibrating, setCalibrating] = useState(false);
  const [calPoints, setCalPoints] = useState<Point[]>([]);
  const [calMm, setCalMm] = useState("10");

  useEffect(() => {
    const wrap = wrapRef.current;
    if (!wrap) return;
    const observer = new ResizeObserver(([entry]) => {
      setSize({ w: Math.max(1, Math.floor(entry.contentRect.width)), h: Math.max(1, Math.floor(entry.contentRect.height)) });
    });
    observer.observe(wrap);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!imageUrl) { setImage(null); return; }
    const next = new window.Image();
    next.onload = () => {
      setImage(next);
      onImageDimensions?.(next.naturalWidth, next.naturalHeight);
    };
    next.onerror = () => setImage(null);
    next.src = imageUrl;
  }, [imageUrl, onImageDimensions]);

  const dims = {
    w: image?.naturalWidth || imageWidth || 800,
    h: image?.naturalHeight || imageHeight || 1000,
  };
  const fit = Math.min(size.w / dims.w, size.h / dims.h);
  const scale = fit * view.zoom;
  const origin = {
    x: (size.w - dims.w * scale) / 2 + view.panX,
    y: (size.h - dims.h * scale) / 2 + view.panY,
  };
  const toCanvas = useCallback((p: Point): Point => ({ x: origin.x + p.x * scale, y: origin.y + p.y * scale }), [origin.x, origin.y, scale]);
  const toImage = useCallback((p: Point): Point => ({ x: (p.x - origin.x) / scale, y: (p.y - origin.y) / scale }), [origin.x, origin.y, scale]);
  const map = useMemo(() => Object.fromEntries(landmarks.map((p) => [p.key, p])) as Record<string, CephLandmark>, [landmarks]);
  const measurementMap = useMemo(() => Object.fromEntries(measurements.map((m) => [m.name, m])) as Record<string, CephMeasurement>, [measurements]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.floor(size.w * dpr);
    canvas.height = Math.floor(size.h * dpr);
    canvas.style.width = `${size.w}px`;
    canvas.style.height = `${size.h}px`;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.direction = "ltr";
    ctx.textAlign = "left";
    ctx.clearRect(0, 0, size.w, size.h);
    ctx.fillStyle = "#020617";
    ctx.fillRect(0, 0, size.w, size.h);

    if (image) {
      ctx.save();
      ctx.filter = `brightness(${imageAdjustments.brightness}%) contrast(${imageAdjustments.contrast}%)${imageAdjustments.inverted ? " invert(1)" : ""}`;
      ctx.drawImage(image, origin.x, origin.y, dims.w * scale, dims.h * scale);
      ctx.restore();
    }

    const line = (aKey: string, bKey: string, color: string, dash: number[] = [], width = 1.5) => {
      const a = map[aKey]; const b = map[bKey];
      if (!a || !b) return;
      const ca = toCanvas(a); const cb = toCanvas(b);
      ctx.beginPath(); ctx.moveTo(ca.x, ca.y); ctx.lineTo(cb.x, cb.y);
      ctx.strokeStyle = color; ctx.lineWidth = width; ctx.setLineDash(dash); ctx.stroke(); ctx.setLineDash([]);
    };

    if (showPlanes) {
      line("ZR", "ZL", "#C4B5FD", [8, 5], 2);
      line("JR", "JL", "#FB923C", [], 2);
      line("AgR", "AgL", "#F87171", [], 2);
      line("U6R", "U6L", "#2DD4BF", [], 2);
      line("L6R", "L6L", "#14B8A6", [], 2);
      const cg = map.Cg; const zr = map.ZR; const zl = map.ZL;
      if (cg && zr && zl) {
        const dx = zl.x - zr.x; const dy = zl.y - zr.y; const len = Math.hypot(dx, dy);
        if (len > 0) {
          const v = { x: -dy / len, y: dx / len };
          const extent = Math.max(dims.w, dims.h) * 1.5;
          const a = toCanvas({ x: cg.x - v.x * extent, y: cg.y - v.y * extent });
          const b = toCanvas({ x: cg.x + v.x * extent, y: cg.y + v.y * extent });
          ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y);
          ctx.strokeStyle = "#FCD34D"; ctx.lineWidth = 1.5; ctx.setLineDash([7, 5]); ctx.stroke(); ctx.setLineDash([]);
          ctx.fillStyle = "#FCD34D"; ctx.font = "bold 11px sans-serif"; ctx.fillText("MSR", b.x + 6, Math.min(size.h - 8, b.y));
        }
      }
    }

    if (calPoints.length > 0) {
      const a = toCanvas(calPoints[0]); const b = toCanvas(calPoints[1] ?? calPoints[0]);
      ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y);
      ctx.strokeStyle = "#FDE047"; ctx.lineWidth = 2; ctx.setLineDash([5, 4]); ctx.stroke(); ctx.setLineDash([]);
    }

    for (const landmark of landmarks) {
      const p = toCanvas(landmark);
      const def = PA_LANDMARK_DEFS[landmark.key];
      ctx.beginPath(); ctx.arc(p.x, p.y, landmark.key === selectedKey ? 6 : 4.5, 0, Math.PI * 2);
      ctx.fillStyle = def?.color ?? "#fff"; ctx.fill();
      ctx.strokeStyle = "#fff"; ctx.lineWidth = 1.5; ctx.stroke();
      ctx.fillStyle = "#fff"; ctx.font = "bold 10px sans-serif"; ctx.fillText(landmark.key, p.x + 7, p.y - 7);
    }

    if (showMeasurements) {
      const labels: Array<[string, string]> = [
        ["PA-JJ-Width", "J–J"], ["PA-AgAg-Width", "Ag–Ag"],
        ["PA-Maxillary-Cant", "U cant"], ["PA-Mandibular-Cant", "L cant"],
        ["PA-Me-MSR", "Me→MSR"],
      ];
      let y = 20;
      ctx.font = "bold 11px sans-serif";
      for (const [key, label] of labels) {
        const m = measurementMap[key]; if (!m || m.value === null) continue;
        const text = `${label}: ${m.value}${m.unit}`;
        const width = ctx.measureText(text).width + 14;
        ctx.fillStyle = "rgba(2, 6, 23, .82)"; ctx.fillRect(12, y - 13, width, 20);
        ctx.fillStyle = "#E2E8F0"; ctx.fillText(text, 19, y + 1); y += 24;
      }
    }
  }, [calPoints, dims.h, dims.w, image, imageAdjustments, landmarks, map, measurementMap, origin.x, origin.y, scale, selectedKey, showMeasurements, showPlanes, size.h, size.w, toCanvas]);

  const eventPoint = (event: React.PointerEvent<HTMLCanvasElement>): Point => {
    const rect = event.currentTarget.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };
  const nearestKey = (canvasPoint: Point) => landmarks.find((p) => {
    const c = toCanvas(p); return Math.hypot(c.x - canvasPoint.x, c.y - canvasPoint.y) <= HIT_RADIUS;
  })?.key ?? null;

  const onPointerDown = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const c = eventPoint(event);
    event.currentTarget.setPointerCapture(event.pointerId);
    if (panMode || event.button === 1) {
      setPanStart({ x: c.x, y: c.y, panX: view.panX, panY: view.panY }); return;
    }
    const p = toImage(c);
    if (calibrating) {
      setCalPoints((current) => current.length >= 2 ? [p] : [...current, p]); return;
    }
    const hit = nearestKey(c);
    if (hit) { setDragKey(hit); onSelectKey(hit); return; }
    if (selectedKey && p.x >= 0 && p.y >= 0 && p.x <= dims.w && p.y <= dims.h) {
      const def = PA_LANDMARK_DEFS[selectedKey];
      const next: CephLandmark = { key: selectedKey, name: def?.nameAr ?? selectedKey, nameAr: def?.nameAr ?? selectedKey, group: def?.group, x: p.x, y: p.y, isAiPlaced: false };
      onLandmarksChange([...landmarks.filter((item) => item.key !== selectedKey), next]);
    }
  };
  const onPointerMove = (event: React.PointerEvent<HTMLCanvasElement>) => {
    const c = eventPoint(event);
    if (panStart) {
      setView((current) => ({ ...current, panX: panStart.panX + c.x - panStart.x, panY: panStart.panY + c.y - panStart.y })); return;
    }
    if (dragKey) {
      const p = toImage(c);
      onLandmarksChange(landmarks.map((item) => item.key === dragKey ? { ...item, x: p.x, y: p.y, isAiPlaced: false } : item));
    }
  };
  const endPointer = () => { setDragKey(null); setPanStart(null); };
  const zoom = (factor: number) => setView((current) => ({ ...current, zoom: Math.max(.25, Math.min(8, current.zoom * factor)) }));
  const applyCalibration = () => {
    const mm = Number(calMm);
    if (calPoints.length !== 2 || !Number.isFinite(mm) || mm <= 0) return;
    const px = Math.hypot(calPoints[1].x - calPoints[0].x, calPoints[1].y - calPoints[0].y);
    if (px > 0) onCalibrate(px / mm);
    setCalibrating(false); setCalPoints([]);
  };

  return (
    <div ref={wrapRef} className="relative h-full w-full overflow-hidden bg-slate-950">
      <canvas
        ref={canvasRef}
        className={cn("block touch-none", panMode ? "cursor-grab active:cursor-grabbing" : selectedKey ? "cursor-crosshair" : "cursor-default")}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endPointer}
        onPointerCancel={endPointer}
        onWheel={(event) => { event.preventDefault(); zoom(event.deltaY < 0 ? 1.12 : .89); }}
      />
      <div className="absolute start-3 top-3 flex items-center gap-1 rounded-md border border-white/10 bg-slate-900/90 p-1 shadow-lg">
        <button type="button" onClick={() => zoom(1.2)} className="rounded p-1.5 text-slate-200 hover:bg-white/10" title="تكبير"><ZoomIn className="h-4 w-4" /></button>
        <button type="button" onClick={() => zoom(.8)} className="rounded p-1.5 text-slate-200 hover:bg-white/10" title="تصغير"><ZoomOut className="h-4 w-4" /></button>
        <button type="button" onClick={() => setView({ zoom: 1, panX: 0, panY: 0 })} className="rounded p-1.5 text-slate-200 hover:bg-white/10" title="ملاءمة الصورة"><Maximize2 className="h-4 w-4" /></button>
        <button type="button" onClick={() => setPanMode((value) => !value)} className={cn("rounded p-1.5", panMode ? "bg-blue-500 text-white" : "text-slate-200 hover:bg-white/10")} title="تحريك الصورة"><Hand className="h-4 w-4" /></button>
        <button type="button" onClick={() => { setCalibrating((value) => !value); setCalPoints([]); }} className={cn("rounded p-1.5", calibrating ? "bg-yellow-400 text-slate-950" : "text-slate-200 hover:bg-white/10")} title="معايرة بمسطرة نقطتين"><Ruler className="h-4 w-4" /></button>
        <button type="button" onClick={() => { onLandmarksChange([]); onSelectKey(null); }} className="rounded p-1.5 text-red-300 hover:bg-red-500/15" title="مسح جميع النقاط"><Trash2 className="h-4 w-4" /></button>
      </div>
      {calibrating && (
        <div className="absolute bottom-3 start-1/2 flex -translate-x-1/2 items-center gap-2 rounded-md border border-yellow-300/30 bg-slate-900/95 px-3 py-2 text-xs text-white shadow-xl">
          <span>{calPoints.length < 2 ? `حدد ${2 - calPoints.length} نقطة على المسطرة` : "أدخل المسافة الحقيقية"}</span>
          <input value={calMm} onChange={(e) => setCalMm(e.target.value)} type="number" min="0.1" step="0.1" className="w-16 rounded border border-white/20 bg-white/10 px-2 py-1 text-white" dir="ltr" />
          <span>mm</span>
          <button type="button" onClick={applyCalibration} disabled={calPoints.length !== 2} className="rounded bg-yellow-400 px-2 py-1 font-bold text-slate-950 disabled:opacity-40">تطبيق</button>
        </div>
      )}
      {pixelsPerMm && pixelsPerMm > 0 && (
        <span className="absolute bottom-3 end-3 rounded bg-emerald-500/15 px-2 py-1 text-[10px] font-medium text-emerald-300">معاير: {pixelsPerMm.toFixed(3)} px/mm</span>
      )}
    </div>
  );
}
