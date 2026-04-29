"use client";

import { useState, useMemo, useRef, useEffect } from "react";
import {
  X, ArrowLeftRight, Layers, Table2,
  ArrowUp, ArrowDown, Minus, TrendingUp,
  Loader2,
} from "lucide-react";
import type {
  CephLandmark,
  MeasurementGroup,
  AnalysisType,
} from "@/types/ceph";
import { ANALYSIS_TYPE_AR } from "@/types/ceph";
import { useCephComparison, type SuperimpositionRef } from "@/hooks/useCephComparison";
import { cn, formatArabicDate } from "@/lib/utils";

// ─── Props ──────────────────────────────────────────────────────────────────

interface Props {
  orthoCaseId: string;
  open: boolean;
  onClose: () => void;
}

// ─── Landmark colors for overlay ────────────────────────────────────────────

const FIRST_COLOR  = "#60A5FA"; // blue
const SECOND_COLOR = "#4ADE80"; // green

type ViewMode = "side-by-side" | "superimposition" | "table";

// ─── Component ──────────────────────────────────────────────────────────────

export function CephComparison({ orthoCaseId, open, onClose }: Props) {
  const {
    analyses,
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    analysesLoading: _analysesLoading,
    analysis1,
    analysis2,
    comparison,
    analysisId1,
    analysisId2,
    setAnalysisId1,
    setAnalysisId2,
  } = useCephComparison(open ? orthoCaseId : null);

  const [viewMode, setViewMode] = useState<ViewMode>("side-by-side");
  const [superimRef, setSuperimRef] = useState<SuperimpositionRef>("SN");
  const [activeGroup, setActiveGroup] = useState<MeasurementGroup>("steiner");

  // ── Superimposition canvas ──
  const superCanvasRef = useRef<HTMLCanvasElement>(null);

  // Compute superimposition transform
  const superimpositionTransform = useMemo(() => {
    if (!analysis1?.landmarks?.length || !analysis2?.landmarks?.length) return null;

    const lm1 = new Map<string, CephLandmark>();
    analysis1.landmarks.forEach((l) => lm1.set(l.key, l));

    // For SN superimposition: align on S and N points
    // For BaN superimposition: align on S (approximate Ba) and N
    const refPt1Key = "S";
    const refPt2Key = "N";

    const s1 = lm1.get(refPt1Key);
    const n1 = lm1.get(refPt2Key);

    if (!s1 || !n1) return null;

    // Translation: move analysis2's S to match analysis1's S
    const dx = s1.x;
    const dy = s1.y;

    // Rotation: align SN line
    const angle1 = Math.atan2(n1.y - s1.y, n1.x - s1.x);

    return { dx, dy, angle1, s1, n1 };
  }, [analysis1, analysis2]);

  // Draw superimposition on canvas
  useEffect(() => {
    if (viewMode !== "superimposition" || !superCanvasRef.current || !comparison) return;

    const canvas = superCanvasRef.current;
    const ctx = canvas.getContext("2d")!;
    const w = canvas.width;
    const h = canvas.height;

    ctx.clearRect(0, 0, w, h);
    ctx.fillStyle = "#0F172A";
    ctx.fillRect(0, 0, w, h);

    const transform = superimpositionTransform;
    if (!transform) return;

    const { s1, angle1 } = transform;

    // Scale to fit
    const imgW1 = analysis1?.imageWidth ?? 800;
    const imgH1 = analysis1?.imageHeight ?? 600;
    const scale = Math.min(w / imgW1, h / imgH1) * 0.85;
    const offsetX = (w - imgW1 * scale) / 2;
    const offsetY = (h - imgH1 * scale) / 2;

    // Draw analysis1 landmarks (blue)
    const lm1 = new Map<string, CephLandmark>();
    analysis1?.landmarks?.forEach((l) => lm1.set(l.key, l));

    ctx.save();
    ctx.translate(offsetX, offsetY);
    ctx.scale(scale, scale);

    // Draw planes for analysis 1
    const drawPlanes = (landmarks: CephLandmark[], color: string, alpha: number) => {
      const lmMap = new Map<string, CephLandmark>();
      landmarks.forEach((l) => lmMap.set(l.key, l));

      const planePairs: [string, string][] = [
        ["S", "N"], ["Po", "Or"], ["Go", "Me"],
        ["N", "A"], ["N", "B"], ["ANS", "PNS"],
      ];

      planePairs.forEach(([k1, k2]) => {
        const p1 = lmMap.get(k1);
        const p2 = lmMap.get(k2);
        if (!p1 || !p2) return;

        const dx = p2.x - p1.x;
        const dy = p2.y - p1.y;
        const ext = 2;

        ctx.beginPath();
        ctx.moveTo(p1.x - dx * ext, p1.y - dy * ext);
        ctx.lineTo(p2.x + dx * ext, p2.y + dy * ext);
        ctx.strokeStyle = color;
        ctx.globalAlpha = alpha;
        ctx.lineWidth = 1.2 / scale;
        ctx.stroke();
        ctx.globalAlpha = 1;
      });
    };

    if (analysis1?.landmarks) drawPlanes(analysis1.landmarks, FIRST_COLOR, 0.5);

    // Draw analysis2 landmarks (green) with transform
    ctx.save();
    // Align analysis2 to analysis1's SN plane
    if (analysis2?.landmarks) {
      const lm2S = analysis2.landmarks.find((l) => l.key === "S");
      const lm2N = analysis2.landmarks.find((l) => l.key === "N");

      if (lm2S && lm2N) {
        const angle2 = Math.atan2(lm2N.y - lm2S.y, lm2N.x - lm2S.x);
        const rotation = angle1 - angle2;

        // Translate so S is at origin, rotate, then translate to S position in analysis1
        ctx.translate(s1.x, s1.y);
        ctx.rotate(rotation);
        ctx.translate(-lm2S.x, -lm2S.y);

        drawPlanes(analysis2.landmarks, SECOND_COLOR, 0.5);

        // Draw landmarks
        analysis2.landmarks.forEach((lm) => {
          ctx.beginPath();
          ctx.arc(lm.x, lm.y, 4 / scale, 0, Math.PI * 2);
          ctx.fillStyle = SECOND_COLOR + "80";
          ctx.fill();
          ctx.strokeStyle = SECOND_COLOR;
          ctx.lineWidth = 1.2 / scale;
          ctx.stroke();

          // Label
          ctx.fillStyle = SECOND_COLOR;
          ctx.font = `${9 / scale}px monospace`;
          ctx.textAlign = "left";
          ctx.fillText(lm.key, lm.x + 5 / scale, lm.y - 3 / scale);
        });
      }
    }
    ctx.restore();

    // Draw analysis1 landmarks (blue) on top
    analysis1?.landmarks?.forEach((lm) => {
      ctx.beginPath();
      ctx.arc(lm.x, lm.y, 4 / scale, 0, Math.PI * 2);
      ctx.fillStyle = FIRST_COLOR + "80";
      ctx.fill();
      ctx.strokeStyle = FIRST_COLOR;
      ctx.lineWidth = 1.2 / scale;
      ctx.stroke();

      // Label
      ctx.fillStyle = FIRST_COLOR;
      ctx.font = `${9 / scale}px monospace`;
      ctx.textAlign = "left";
      ctx.fillText(lm.key, lm.x + 5 / scale, lm.y - 3 / scale);
    });

    // Draw movement vectors between corresponding landmarks
    if (analysis2?.landmarks && comparison) {
      const lm2 = new Map<string, CephLandmark>();
      analysis2.landmarks.forEach((l) => lm2.set(l.key, l));
      const lm2S = analysis2.landmarks.find((l) => l.key === "S");
      const lm2N = analysis2.landmarks.find((l) => l.key === "N");

      if (lm2S && lm2N) {
        const angle2 = Math.atan2(lm2N.y - lm2S.y, lm2N.x - lm2S.x);
        const rotation = angle1 - angle2;
        const cos = Math.cos(rotation);
        const sin = Math.sin(rotation);

        comparison.landmarkDeltas.forEach((delta) => {
          const l1 = lm1.get(delta.key);
          const l2 = lm2.get(delta.key);
          if (!l1 || !l2) return;
          if (delta.distance < 3) return; // Skip very small movements

          // Transform l2 position to analysis1 coordinate space
          const relX = l2.x - lm2S.x;
          const relY = l2.y - lm2S.y;
          const tx = relX * cos - relY * sin + s1.x;
          const ty = relX * sin + relY * cos + s1.y;

          // Draw displacement vector
          ctx.beginPath();
          ctx.moveTo(l1.x, l1.y);
          ctx.lineTo(tx, ty);
          ctx.strokeStyle = "#FBBF24";
          ctx.lineWidth = 1.5 / scale;
          ctx.setLineDash([4 / scale, 3 / scale]);
          ctx.stroke();
          ctx.setLineDash([]);

          // Arrow head
          const ang = Math.atan2(ty - l1.y, tx - l1.x);
          const arrowLen = 6 / scale;
          ctx.beginPath();
          ctx.moveTo(tx, ty);
          ctx.lineTo(tx - arrowLen * Math.cos(ang - 0.4), ty - arrowLen * Math.sin(ang - 0.4));
          ctx.moveTo(tx, ty);
          ctx.lineTo(tx - arrowLen * Math.cos(ang + 0.4), ty - arrowLen * Math.sin(ang + 0.4));
          ctx.strokeStyle = "#FBBF24";
          ctx.lineWidth = 1.5 / scale;
          ctx.stroke();
        });
      }
    }

    ctx.restore();

    // Legend
    ctx.fillStyle = "#FFFFFF";
    ctx.font = "11px Tajawal, sans-serif";
    ctx.textAlign = "right";

    const legendX = w - 15;
    let legendY = 20;

    // Analysis 1 label
    ctx.fillStyle = FIRST_COLOR;
    ctx.fillText("● التحليل الأول (T1)", legendX, legendY);
    legendY += 16;

    // Analysis 2 label
    ctx.fillStyle = SECOND_COLOR;
    ctx.fillText("● التحليل الثاني (T2)", legendX, legendY);
    legendY += 16;

    // Vector label
    ctx.fillStyle = "#FBBF24";
    ctx.fillText("→ متجه الحركة", legendX, legendY);

  }, [viewMode, comparison, analysis1, analysis2, superimpositionTransform]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl w-[95vw] max-w-[1200px] max-h-[90vh] flex flex-col overflow-hidden" dir="rtl">
        {/* ── Header ── */}
        <div className="flex items-center justify-between px-5 py-3 border-b border-gray-200 flex-shrink-0">
          <h2 className="text-base font-bold text-gray-900">مقارنة التحاليل السيفالومترية</h2>
          <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-gray-100 transition text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* ── Analysis selectors ── */}
        <div className="flex items-center gap-4 px-5 py-3 border-b border-gray-100 bg-gray-50 flex-shrink-0">
          <div className="flex-1">
            <label className="text-[10px] font-semibold text-gray-500 mb-1 block">التحليل الأول (T1)</label>
            <select
              value={analysisId1 ?? ""}
              onChange={(e) => setAnalysisId1(e.target.value || null)}
              className="w-full text-xs border border-gray-200 rounded-lg px-3 py-2 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            >
              <option value="">اختر التحليل...</option>
              {analyses.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.patientName} — {ANALYSIS_TYPE_AR[a.analysisType as AnalysisType] ?? a.analysisType} — {formatArabicDate(a.analysisDate)}
                </option>
              ))}
            </select>
          </div>

          <div className="flex items-center justify-center pt-4">
            <ArrowLeftRight className="w-5 h-5 text-gray-300" />
          </div>

          <div className="flex-1">
            <label className="text-[10px] font-semibold text-gray-500 mb-1 block">التحليل الثاني (T2)</label>
            <select
              value={analysisId2 ?? ""}
              onChange={(e) => setAnalysisId2(e.target.value || null)}
              className="w-full text-xs border border-gray-200 rounded-lg px-3 py-2 bg-white focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            >
              <option value="">اختر التحليل...</option>
              {analyses.filter(a => a.id !== analysisId1).map((a) => (
                <option key={a.id} value={a.id}>
                  {a.patientName} — {ANALYSIS_TYPE_AR[a.analysisType as AnalysisType] ?? a.analysisType} — {formatArabicDate(a.analysisDate)}
                </option>
              ))}
            </select>
          </div>

          {/* View mode tabs */}
          <div className="flex items-center gap-1 bg-white rounded-lg border border-gray-200 p-1 flex-shrink-0">
            {([
              { key: "side-by-side" as ViewMode, icon: ArrowLeftRight, label: "جنبًا إلى جنب" },
              { key: "superimposition" as ViewMode, icon: Layers, label: "تراكب" },
              { key: "table" as ViewMode, icon: Table2, label: "جدول" },
            ]).map((tab) => (
              <button
                key={tab.key}
                onClick={() => setViewMode(tab.key)}
                className={cn(
                  "flex items-center gap-1 px-2.5 py-1.5 text-[10px] font-medium rounded-md transition",
                  viewMode === tab.key
                    ? "bg-clinic-teal text-white shadow-sm"
                    : "text-gray-500 hover:bg-gray-50"
                )}
              >
                <tab.icon className="w-3 h-3" />
                <span className="hidden sm:inline">{tab.label}</span>
              </button>
            ))}
          </div>
        </div>

        {/* ── Content ── */}
        <div className="flex-1 overflow-auto">
          {!analysisId1 || !analysisId2 ? (
            <div className="flex items-center justify-center h-64 text-gray-400">
              <div className="text-center">
                <Layers className="w-10 h-10 mx-auto mb-3 opacity-30" />
                <p className="text-sm font-medium">اختر تحليلين للمقارنة</p>
                <p className="text-xs mt-1">يجب أن ينتمي التحليلان لنفس الحالة</p>
              </div>
            </div>
          ) : !comparison ? (
            <div className="flex items-center justify-center h-64">
              <Loader2 className="w-6 h-6 animate-spin text-clinic-teal" />
            </div>
          ) : (
            <div className="p-4">
              {/* ── Side-by-side view ── */}
              {viewMode === "side-by-side" && (
                <div className="grid grid-cols-2 gap-4">
                  {/* Analysis 1 */}
                  <div className="border border-blue-200 rounded-xl overflow-hidden">
                    <div className="bg-blue-50 px-3 py-2 flex items-center justify-between">
                      <span className="text-[11px] font-bold text-blue-800">
                        T1: {ANALYSIS_TYPE_AR[comparison.analysis1.analysisType as AnalysisType]}
                      </span>
                      <span className="text-[9px] text-blue-600">
                        {formatArabicDate(comparison.analysis1.analysisDate)}
                      </span>
                    </div>
                    <div className="p-3 max-h-96 overflow-y-auto">
                      <LandmarkList landmarks={comparison.analysis1.landmarks ?? []} color={FIRST_COLOR} />
                    </div>
                  </div>

                  {/* Analysis 2 */}
                  <div className="border border-green-200 rounded-xl overflow-hidden">
                    <div className="bg-green-50 px-3 py-2 flex items-center justify-between">
                      <span className="text-[11px] font-bold text-green-800">
                        T2: {ANALYSIS_TYPE_AR[comparison.analysis2.analysisType as AnalysisType]}
                      </span>
                      <span className="text-[9px] text-green-600">
                        {formatArabicDate(comparison.analysis2.analysisDate)}
                      </span>
                    </div>
                    <div className="p-3 max-h-96 overflow-y-auto">
                      <LandmarkList landmarks={comparison.analysis2.landmarks ?? []} color={SECOND_COLOR} />
                    </div>
                  </div>
                </div>
              )}

              {/* ── Superimposition view ── */}
              {viewMode === "superimposition" && (
                <div>
                  {/* Reference plane selector */}
                  <div className="flex items-center gap-3 mb-3">
                    <span className="text-[11px] font-semibold text-gray-600">خط المرجع:</span>
                    <div className="flex gap-1">
                      {(["SN", "BaN"] as SuperimpositionRef[]).map((ref) => (
                        <button
                          key={ref}
                          onClick={() => setSuperimRef(ref)}
                          className={cn(
                            "px-3 py-1 text-[10px] rounded-lg border transition font-medium",
                            superimRef === ref
                              ? "bg-clinic-teal text-white border-clinic-teal"
                              : "border-gray-200 text-gray-500 hover:border-clinic-teal"
                          )}
                        >
                          {ref === "SN" ? "خط SN" : "خط Ba-N"}
                        </button>
                      ))}
                    </div>
                    <span className="text-[9px] text-gray-400">تراكب على أساس الخط المرجعي المشترك</span>
                  </div>

                  <canvas
                    ref={superCanvasRef}
                    width={1100}
                    height={600}
                    className="w-full rounded-lg border border-gray-700"
                    style={{ background: "#0F172A" }}
                  />

                  {/* Movement vectors legend */}
                  <div className="mt-3 bg-gray-50 rounded-lg p-3">
                    <p className="text-[11px] font-bold text-gray-700 mb-2">متجهات الحركة (أكبر من 3 بكسل)</p>
                    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-2">
                      {comparison.landmarkDeltas
                        .filter((d) => d.distance >= 3)
                        .sort((a, b) => b.distance - a.distance)
                        .map((d) => (
                          <div key={d.key} className="flex items-center gap-2 text-[10px] bg-white rounded-lg px-2 py-1.5 border border-gray-100">
                            <span className="font-mono font-bold text-gray-700 w-6">{d.key}</span>
                            <span className="text-gray-500">{d.nameAr}</span>
                            <span className="mr-auto font-mono text-amber-600 font-bold">
                              {d.distanceMm ? `${d.distanceMm} mm` : `${d.distance.toFixed(0)} px`}
                            </span>
                          </div>
                        ))}
                    </div>
                  </div>
                </div>
              )}

              {/* ── Table view ── */}
              {viewMode === "table" && (
                <div>
                  {/* Summary card */}
                  <div className="grid grid-cols-4 gap-3 mb-4">
                    <SummaryCard label="إجمالي القياسات" value={comparison.summary.totalMeasurements} color="gray" />
                    <SummaryCard label="تحسّن" value={comparison.summary.improved} color="green" />
                    <SummaryCard label="تدهور" value={comparison.summary.worsened} color="red" />
                    <SummaryCard label="بدون تغيير" value={comparison.summary.unchanged} color="gray" />
                  </div>

                  {/* Growth direction */}
                  <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 mb-4">
                    <div className="flex items-center gap-2">
                      <TrendingUp className="w-4 h-4 text-blue-600" />
                      <span className="text-[11px] font-bold text-blue-800">اتجاه النمو: {comparison.summary.growthDirection}</span>
                    </div>
                    {comparison.summary.keyFindings.length > 0 && (
                      <ul className="mt-2 space-y-1">
                        {comparison.summary.keyFindings.map((f, i) => (
                          <li key={i} className="text-[10px] text-blue-700 flex items-start gap-1">
                            <span className="text-blue-400 mt-0.5">•</span>
                            {f}
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>

                  {/* Group tabs */}
                  <div className="flex gap-1 mb-3 flex-wrap">
                    {(["steiner", "tweed", "mcnamara", "ricketts", "downs", "wits", "jarabak", "softtissue"] as MeasurementGroup[]).map((g) => {
                      const cnt = comparison.measurementDeltas.filter(
                        (d) => d.analysisGroup === g && d.changeDirection !== "na"
                      ).length;
                      if (cnt === 0) return null;
                      return (
                        <button
                          key={g}
                          onClick={() => setActiveGroup(g)}
                          className={cn(
                            "px-2.5 py-1 text-[10px] rounded-lg font-medium border transition",
                            activeGroup === g
                              ? "bg-clinic-teal text-white border-clinic-teal"
                              : "bg-white text-gray-500 border-gray-200 hover:border-clinic-teal"
                          )}
                        >
                          {g === "steiner" ? "ستاينر" : g === "tweed" ? "تويد" : g === "mcnamara" ? "ماكنامارا" : g === "ricketts" ? "ريكتس" : g === "downs" ? "داونز" : g === "wits" ? "وتس" : g === "jarabak" ? "جاراباك" : "الأنسجة الرخوة"}
                          <span className="mr-1 text-[8px] opacity-60">({cnt})</span>
                        </button>
                      );
                    })}
                  </div>

                  {/* Comparison table */}
                  <div className="bg-white rounded-xl border border-gray-200 overflow-hidden">
                    <table className="w-full text-[11px]">
                      <thead className="bg-gray-50 border-b border-gray-200">
                        <tr>
                          <th className="text-start px-3 py-2 font-semibold text-gray-600">القياس</th>
                          <th className="text-center px-3 py-2 font-semibold text-blue-600">T1</th>
                          <th className="text-center px-3 py-2 font-semibold text-green-600">T2</th>
                          <th className="text-center px-3 py-2 font-semibold text-amber-600">التغير</th>
                          <th className="text-center px-3 py-2 font-semibold text-gray-600">التقييم</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50">
                        {comparison.measurementDeltas
                          .filter((d) => d.analysisGroup === activeGroup)
                          .map((d) => {
                            const improved = d.interpretation.includes("تحسّن");
                            const worsened = d.interpretation.includes("تدهور");
                            return (
                              <tr key={d.name} className={cn(
                                worsened ? "bg-red-50/50" : improved ? "bg-green-50/50" : "hover:bg-gray-50"
                              )}>
                                <td className="px-3 py-2 font-medium text-gray-800">{d.nameAr}</td>
                                <td className="px-3 py-2 text-center font-mono text-blue-700">
                                  {d.value1 !== null ? `${d.value1}${d.unit}` : "—"}
                                </td>
                                <td className="px-3 py-2 text-center font-mono text-green-700">
                                  {d.value2 !== null ? `${d.value2}${d.unit}` : "—"}
                                </td>
                                <td className="px-3 py-2 text-center">
                                  {d.change !== null ? (
                                    <span className={cn(
                                      "font-mono font-bold flex items-center justify-center gap-0.5",
                                      improved ? "text-green-600" : worsened ? "text-red-600" : "text-gray-500"
                                    )}>
                                      {d.changeDirection === "up" && <ArrowUp className="w-3 h-3" />}
                                      {d.changeDirection === "down" && <ArrowDown className="w-3 h-3" />}
                                      {d.changeDirection === "same" && <Minus className="w-3 h-3" />}
                                      {d.change > 0 ? "+" : ""}{d.change}{d.unit}
                                    </span>
                                  ) : "—"}
                                </td>
                                <td className="px-3 py-2 text-center">
                                  <span className={cn(
                                    "text-[9px] px-1.5 py-0.5 rounded-full font-medium",
                                    improved ? "bg-green-100 text-green-700" :
                                    worsened ? "bg-red-100 text-red-700" :
                                    "bg-gray-100 text-gray-600"
                                  )}>
                                    {d.interpretation}
                                  </span>
                                </td>
                              </tr>
                            );
                          })}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Sub-components ─────────────────────────────────────────────────────────

function LandmarkList({ landmarks, color }: { landmarks: CephLandmark[]; color: string }) {
  return (
    <div className="space-y-0.5">
      {landmarks.map((lm) => (
        <div key={lm.key} className="flex items-center gap-2 text-[10px] px-2 py-1 rounded hover:bg-gray-50">
          <span
            className="w-2 h-2 rounded-full flex-shrink-0"
            style={{ backgroundColor: color }}
          />
          <span className="font-mono font-bold text-gray-500 w-6">{lm.key}</span>
          <span className="flex-1 text-gray-700">{lm.nameAr}</span>
          <span className="text-gray-400 font-mono">
            ({lm.x.toFixed(0)}, {lm.y.toFixed(0)})
          </span>
          {lm.isAiPlaced && (
            <span className="text-[8px] text-purple-400 font-bold">AI</span>
          )}
        </div>
      ))}
    </div>
  );
}

function SummaryCard({ label, value, color }: { label: string; value: number; color: "green" | "red" | "gray" }) {
  return (
    <div className={cn(
      "rounded-lg p-3 text-center",
      color === "green" ? "bg-green-50 border border-green-200" :
      color === "red" ? "bg-red-50 border border-red-200" :
      "bg-gray-50 border border-gray-200"
    )}>
      <div className={cn(
        "text-xl font-bold",
        color === "green" ? "text-green-600" :
        color === "red" ? "text-red-600" :
        "text-gray-700"
      )}>
        {value}
      </div>
      <div className="text-[10px] text-gray-500 mt-0.5">{label}</div>
    </div>
  );
}
