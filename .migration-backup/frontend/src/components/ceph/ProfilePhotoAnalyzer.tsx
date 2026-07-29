"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { CheckCircle, AlertTriangle, XCircle, RotateCcw } from "lucide-react";
import {
  PROFILE_LANDMARKS, computePhotoMeasurements, profileType,
  type PhotoSeverity, type PhotoMeasurement,
} from "@/lib/photoAnalysis";
import { cn } from "@/lib/utils";

type Pt = { x: number; y: number };

interface Props {
  imageUrl: string;
  /** Pre-placed landmarks when reopening a saved analysis. */
  initialPoints?: Record<string, Pt>;
  /** Reports the current placed points + computed measurements (for saving). */
  onChange?: (data: { points: Record<string, Pt>; measurements: PhotoMeasurement[] }) => void;
}

const SEVERITY = {
  normal: { icon: CheckCircle, cls: "text-green-600", bg: "bg-green-50 text-green-700", label: "طبيعي" },
  mild: { icon: AlertTriangle, cls: "text-amber-500", bg: "bg-amber-50 text-amber-700", label: "خفيف" },
  severe: { icon: XCircle, cls: "text-red-500", bg: "bg-red-50 text-red-700", label: "واضح" },
} as const;

export function ProfilePhotoAnalyzer({ imageUrl, initialPoints, onChange }: Props) {
  const svgRef = useRef<SVGSVGElement>(null);
  const [dim, setDim] = useState<{ w: number; h: number } | null>(null);
  const [points, setPoints] = useState<Record<string, Pt>>(initialPoints ?? {});
  const [selected, setSelected] = useState<string>(PROFILE_LANDMARKS[0].key);

  useEffect(() => {
    setPoints(initialPoints ?? {});
    const img = new window.Image();
    img.onload = () => setDim({ w: img.naturalWidth, h: img.naturalHeight });
    img.onerror = () => setDim(null);
    img.src = imageUrl;
  }, [imageUrl, initialPoints]);

  const measurements = useMemo(() => computePhotoMeasurements(points), [points]);
  const profile = profileType(measurements);

  useEffect(() => { onChange?.({ points, measurements }); }, [points, measurements, onChange]);
  const r = dim ? Math.max(4, dim.w / 120) : 6;

  const placeAt = (e: React.MouseEvent<SVGSVGElement>) => {
    const svg = svgRef.current;
    if (!svg) return;
    const p = svg.createSVGPoint();
    p.x = e.clientX;
    p.y = e.clientY;
    const ctm = svg.getScreenCTM();
    if (!ctm) return;
    const loc = p.matrixTransform(ctm.inverse());
    setPoints((prev) => ({ ...prev, [selected]: { x: loc.x, y: loc.y } }));
    // Auto-advance to the next unplaced landmark for a fast workflow.
    const order = PROFILE_LANDMARKS.map((l) => l.key);
    const next = order.find((k) => k !== selected && !points[k]);
    if (next) setSelected(next);
  };

  return (
    <div className="grid lg:grid-cols-3 gap-4">
      {/* Image + points */}
      <div className="lg:col-span-2 bg-slate-900 rounded-xl border border-gray-200 overflow-hidden">
        {dim ? (
          <svg
            ref={svgRef}
            viewBox={`0 0 ${dim.w} ${dim.h}`}
            className="w-full max-h-[560px] cursor-crosshair"
            onClick={placeAt}
            role="img"
            aria-label="تحليل صورة البروفايل الجانبية"
          >
            <image href={imageUrl} x={0} y={0} width={dim.w} height={dim.h} />
            {Object.entries(points).map(([key, pt]) => (
              <g key={key}>
                <circle cx={pt.x} cy={pt.y} r={r}
                  fill={key === selected ? "#F59E0B" : "#22D3EE"} stroke="#fff" strokeWidth={r * 0.25} />
                <text x={pt.x + r * 1.4} y={pt.y - r * 0.6} fontSize={r * 2.2}
                  fontWeight="bold" fill="#67E8F9">{key}</text>
              </g>
            ))}
          </svg>
        ) : (
          <div className="h-80 flex items-center justify-center text-slate-400 text-sm">
            تعذّر تحميل الصورة — أعد رفعها
          </div>
        )}
      </div>

      {/* Controls + results */}
      <div className="space-y-3">
        {/* Landmark picker */}
        <div className="bg-white rounded-xl border border-gray-200 p-3">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-bold text-gray-700">المعالم الرخوة</h3>
            <button onClick={() => setPoints({})}
              className="flex items-center gap-1 text-[10px] text-gray-500 hover:text-clinic-blue transition">
              <RotateCcw className="w-3 h-3" />تصفير
            </button>
          </div>
          <p className="text-[10px] text-gray-400 mb-2">اختر المعلم ثم انقر على الصورة لوضعه.</p>
          <div className="grid grid-cols-1 gap-1">
            {PROFILE_LANDMARKS.map((l) => {
              const placed = Boolean(points[l.key]);
              return (
                <button key={l.key} onClick={() => setSelected(l.key)}
                  className={cn(
                    "flex items-center justify-between gap-2 px-2 py-1 rounded-md text-[11px] border transition text-start",
                    selected === l.key
                      ? "bg-clinic-blue text-white border-clinic-blue"
                      : "bg-white text-gray-600 border-gray-200 hover:border-clinic-blue",
                  )}>
                  <span className="truncate">{l.nameAr}</span>
                  <span className={cn("text-[9px] font-mono", placed ? "" : "opacity-40")}>
                    {l.key}{placed ? " ✓" : ""}
                  </span>
                </button>
              );
            })}
          </div>
        </div>

        {/* Measurements */}
        <div className="bg-white rounded-xl border border-gray-200 p-3 space-y-2">
          <h3 className="text-sm font-bold text-gray-700">القياسات الرخوة</h3>
          {profile && (
            <div className="text-[11px] bg-clinic-blue-50/60 border border-clinic-blue-100 rounded-lg px-2 py-1.5 text-clinic-navy font-medium">
              {profile}
            </div>
          )}
          {measurements.map((m) => {
            const cfg = SEVERITY[m.severity as PhotoSeverity];
            const Icon = cfg.icon;
            return (
              <div key={m.key} className="border-b border-gray-100 last:border-0 pb-2 last:pb-0">
                <div className="flex items-center justify-between gap-2">
                  <span className="text-[11px] font-medium text-gray-700">{m.nameAr}</span>
                  <span className="text-sm font-bold font-mono text-gray-900" dir="ltr">
                    {m.value !== null ? `${m.value}°` : <span className="text-gray-300 text-xs">—</span>}
                  </span>
                </div>
                <div className="flex items-center justify-between mt-0.5">
                  <span className="text-[9px] text-gray-400 font-mono" dir="ltr">{m.normal}° ± {m.sd}</span>
                  {m.value !== null && (
                    <span className={cn("text-[9px] px-1.5 py-0.5 rounded-full flex items-center gap-0.5", cfg.bg)}>
                      <Icon className="w-2.5 h-2.5" />{cfg.label}
                    </span>
                  )}
                </div>
                {m.value !== null && m.severity !== "normal" && m.interpretationAr && (
                  <p className="text-[10px] text-gray-500 mt-0.5">{m.interpretationAr}</p>
                )}
              </div>
            );
          })}
          <p className="text-[9px] text-gray-400 leading-relaxed pt-1">
            قياسات زاويّة مستقلة عن المقياس (لا تتطلب معايرة) — أداة مساعدة سريرية تتطلب مراجعة الأخصائي.
          </p>
        </div>
      </div>
    </div>
  );
}
