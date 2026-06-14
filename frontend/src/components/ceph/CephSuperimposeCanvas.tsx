"use client";

import { useMemo } from "react";
import { similarityFromPairs, applySimilarity, type Similarity } from "@/lib/cephMath";

interface LM { key: string; x: number; y: number; }

interface Props {
  baseLandmarks: LM[];
  targetLandmarks: LM[];
  baseDate: string;
  targetDate: string;
  /** Landmark pair the two tracings are registered on. Defaults to S→N (cranial base). */
  registerKeys?: [string, string];
}

// Anatomical tracing connections — a recognizable skeletal outline drawn for
// each tracing from whichever landmarks are present.
const SEGMENTS: [string, string][] = [
  ["S", "N"],                 // cranial base (SN)
  ["N", "A"], ["A", "ANS"], ["ANS", "PNS"], // maxilla + palatal plane
  ["N", "Pog"],               // facial plane
  ["Co", "Go"], ["Ar", "Go"], ["Go", "Me"], ["Me", "Pog"], ["Pog", "B"], // mandible
  ["U1A", "U1T"], ["L1A", "L1T"], // incisors
];

const BASE_COLOR = "#2563EB";    // blue-600  — "قبل"
const TARGET_COLOR = "#059669";  // emerald-600 — "بعد"

function toMap(lms: LM[]): Map<string, LM> {
  const m = new Map<string, LM>();
  for (const l of lms) m.set(l.key, l);
  return m;
}

function Tracing({ pts, color, opacity }: { pts: Map<string, { x: number; y: number }>; color: string; opacity: number }) {
  return (
    <g stroke={color} fill={color} opacity={opacity}>
      {SEGMENTS.map(([from, to]) => {
        const a = pts.get(from), b = pts.get(to);
        if (!a || !b) return null;
        return <line key={`${from}-${to}`} x1={a.x} y1={a.y} x2={b.x} y2={b.y} strokeWidth={1.4} strokeLinecap="round" />;
      })}
      {[...pts.entries()].map(([key, p]) => (
        <circle key={key} cx={p.x} cy={p.y} r={2.4} stroke="#fff" strokeWidth={0.7} />
      ))}
    </g>
  );
}

export function CephSuperimposeCanvas({
  baseLandmarks, targetLandmarks, baseDate, targetDate,
  registerKeys = ["S", "N"],
}: Props) {
  const result = useMemo(() => {
    const baseMap = toMap(baseLandmarks);
    const targetMap = toMap(targetLandmarks);
    const [kA, kB] = registerKeys;

    const bA = baseMap.get(kA), bB = baseMap.get(kB);
    const tA = targetMap.get(kA), tB = targetMap.get(kB);
    if (!bA || !bB || !tA || !tB) return { ok: false as const };

    // Map the target tracing into the base coordinate frame (register on kA→kB).
    const t: Similarity = similarityFromPairs(tA, tB, bA, bB);
    const basePts = new Map([...baseMap].map(([k, p]) => [k, { x: p.x, y: p.y }]));
    const targetPts = new Map([...targetMap].map(([k, p]) => [k, applySimilarity(t, p)]));

    // viewBox bounds over both registered tracings, with padding.
    const all = [...basePts.values(), ...targetPts.values()];
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const p of all) {
      minX = Math.min(minX, p.x); minY = Math.min(minY, p.y);
      maxX = Math.max(maxX, p.x); maxY = Math.max(maxY, p.y);
    }
    const pad = Math.max(20, (maxX - minX + maxY - minY) * 0.06);
    return {
      ok: true as const,
      basePts, targetPts,
      viewBox: `${minX - pad} ${minY - pad} ${(maxX - minX) + 2 * pad} ${(maxY - minY) + 2 * pad}`,
    };
  }, [baseLandmarks, targetLandmarks, registerKeys]);

  if (!result.ok) {
    return (
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-700" dir="rtl">
        يتطلب التراكب وجود معلمي قاعدة الجمجمة (S و N) في كلا التحليلين. ضع المعلمين ثم احفظ ليظهر التراكب.
      </div>
    );
  }

  return (
    <div dir="rtl">
      <div className="flex items-center gap-4 mb-2 text-xs">
        <span className="flex items-center gap-1.5">
          <span className="inline-block w-3 h-0.5 rounded-full" style={{ backgroundColor: BASE_COLOR }} />
          <span className="font-medium" style={{ color: BASE_COLOR }}>قبل — {baseDate}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block w-3 h-0.5 rounded-full" style={{ backgroundColor: TARGET_COLOR }} />
          <span className="font-medium" style={{ color: TARGET_COLOR }}>بعد — {targetDate}</span>
        </span>
        <span className="text-gray-400 mr-auto">متراكب على قاعدة الجمجمة (SN) عند نقطة السرج S</span>
      </div>
      <svg
        viewBox={result.viewBox}
        className="w-full max-h-[460px] rounded-lg border border-gray-200 bg-white"
        preserveAspectRatio="xMidYMid meet"
        role="img"
        aria-label="تراكب سيفالومتري قبل/بعد على قاعدة الجمجمة"
      >
        <Tracing pts={result.basePts} color={BASE_COLOR} opacity={0.9} />
        <Tracing pts={result.targetPts} color={TARGET_COLOR} opacity={0.9} />
      </svg>
    </div>
  );
}
