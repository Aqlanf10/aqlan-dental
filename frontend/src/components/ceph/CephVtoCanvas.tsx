"use client";

import { useMemo } from "react";

type Pt = { x: number; y: number };

interface Props {
  /** Current tracing landmarks (image coordinates). */
  original: Record<string, Pt>;
  /** Predicted tracing after the planned incisor movements. */
  predicted: Record<string, Pt>;
}

// Same anatomical tracing connections used across the ceph module.
const SEGMENTS: [string, string][] = [
  ["S", "N"],
  ["N", "A"], ["A", "ANS"], ["ANS", "PNS"],
  ["N", "Pog"],
  ["Co", "Go"], ["Ar", "Go"], ["Go", "Me"], ["Me", "Pog"], ["Pog", "B"],
  ["U1A", "U1T"], ["L1A", "L1T"],
];

const ORIGINAL_COLOR = "#9CA3AF"; // gray-400 — current
const PREDICTED_COLOR = "#2563EB"; // blue-600 — planned objective

function Tracing({ pts, color, dashed }: { pts: Record<string, Pt>; color: string; dashed?: boolean }) {
  return (
    <g stroke={color} fill={color}>
      {SEGMENTS.map(([from, to]) => {
        const a = pts[from], b = pts[to];
        if (!a || !b) return null;
        return (
          <line key={`${from}-${to}`} x1={a.x} y1={a.y} x2={b.x} y2={b.y}
            strokeWidth={1.4} strokeLinecap="round" strokeDasharray={dashed ? "4 3" : undefined} />
        );
      })}
      {Object.entries(pts).map(([key, p]) => (
        <circle key={key} cx={p.x} cy={p.y} r={2.2} stroke="#fff" strokeWidth={0.6} />
      ))}
    </g>
  );
}

export function CephVtoCanvas({ original, predicted }: Props) {
  const viewBox = useMemo(() => {
    const all = [...Object.values(original), ...Object.values(predicted)];
    if (all.length === 0) return "0 0 800 600";
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const p of all) {
      minX = Math.min(minX, p.x); minY = Math.min(minY, p.y);
      maxX = Math.max(maxX, p.x); maxY = Math.max(maxY, p.y);
    }
    const pad = Math.max(20, (maxX - minX + maxY - minY) * 0.06);
    return `${minX - pad} ${minY - pad} ${(maxX - minX) + 2 * pad} ${(maxY - minY) + 2 * pad}`;
  }, [original, predicted]);

  return (
    <div dir="rtl">
      <div className="flex items-center gap-4 mb-2 text-xs">
        <span className="flex items-center gap-1.5">
          <span className="inline-block w-3 h-0.5 rounded-full" style={{ backgroundColor: ORIGINAL_COLOR }} />
          <span className="font-medium text-gray-500">الوضع الحالي</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block w-3 h-0.5 rounded-full" style={{ backgroundColor: PREDICTED_COLOR }} />
          <span className="font-medium" style={{ color: PREDICTED_COLOR }}>هدف العلاج المخطّط</span>
        </span>
      </div>
      <svg
        viewBox={viewBox}
        className="w-full max-h-[460px] rounded-lg border border-gray-200 bg-white"
        preserveAspectRatio="xMidYMid meet"
        role="img"
        aria-label="هدف العلاج البصري — التتبّع الحالي مقابل المخطّط"
      >
        <Tracing pts={original} color={ORIGINAL_COLOR} dashed />
        <Tracing pts={predicted} color={PREDICTED_COLOR} />
      </svg>
    </div>
  );
}
