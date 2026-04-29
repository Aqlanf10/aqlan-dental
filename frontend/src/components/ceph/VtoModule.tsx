"use client";
import { useState, useRef, useEffect, useCallback } from "react";
import { Sparkles, Play, RotateCcw, AlertTriangle, Info } from "lucide-react";
import { useVtoSuggestion, useOrthoCaseDataForAi } from "@/hooks/useAiAdvisory";
import type { VtoSuggestion as VtoSuggestionType } from "@/types/ai";

interface LandmarkPosition { x: number; y: number; key: string; nameAr: string; }

interface VtoModuleProps {
  caseId: string;
  landmarks: LandmarkPosition[];
  imageWidth: number;
  imageHeight: number;
  imageUrl?: string;
}

// Before profile: #3d7ab5 (dashed), After profile: #22c55e
const VTO_COLORS = {
  original: "#3d7ab5",    // before profile (dashed)
  target: "#22c55e",      // after profile
  arrow: "#f5922e",       // amber
  arrowHover: "#ef4444",  // red
};

export function VtoModule({ caseId, landmarks, imageWidth, imageHeight, imageUrl }: VtoModuleProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [vtoData, setVtoData] = useState<VtoSuggestionType | null>(null);
  const [error, setError] = useState("");
  const [showOriginal, setShowOriginal] = useState(true);
  const [showTarget, setShowTarget] = useState(true);
  const [showArrows, setShowArrows] = useState(true);
  const [animProgress, setAnimProgress] = useState(1);
  const { data: caseData } = useOrthoCaseDataForAi(caseId);
  const vtoMutation = useVtoSuggestion();

  const handleGenerateVto = async () => {
    if (!caseData) return;
    setError("");
    vtoMutation.mutate(
      { caseData },
      {
        onSuccess: (data) => {
          setVtoData(data);
          setAnimProgress(0);
          startAnimation();
        },
        onError: () => setError("فشل إنشاء VTO. حاول مرة أخرى."),
      }
    );
  };

  const startAnimation = useCallback(() => {
    setAnimProgress(0);
    let frame = 0;
    const totalFrames = 60;
    const animate = () => {
      frame++;
      const progress = Math.min(frame / totalFrames, 1);
      setAnimProgress(1 - Math.pow(1 - progress, 3));
      if (progress < 1) requestAnimationFrame(animate);
    };
    requestAnimationFrame(animate);
  }, []);

  const handleReset = () => {
    setVtoData(null);
    setAnimProgress(1);
  };

  // Draw canvas
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    canvas.width = imageWidth;
    canvas.height = imageHeight;
    ctx.clearRect(0, 0, imageWidth, imageHeight);

    if (imageUrl) {
      const img = new Image();
      img.crossOrigin = "anonymous";
      img.onload = () => {
        ctx.globalAlpha = 0.3;
        ctx.drawImage(img, 0, 0, imageWidth, imageHeight);
        ctx.globalAlpha = 1;
        drawOverlays(ctx);
      };
      img.src = imageUrl;
    } else {
      drawOverlays(ctx);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [landmarks, vtoData, showOriginal, showTarget, showArrows, animProgress, imageWidth, imageHeight, imageUrl]);

  function drawOverlays(ctx: CanvasRenderingContext2D) {
    const landmarkMap = new Map(landmarks.map((l) => [l.key, l]));

    // Draw original landmarks (before profile - #3d7ab5 dashed)
    if (showOriginal) {
      landmarks.forEach((lm) => {
        ctx.beginPath();
        ctx.arc(lm.x, lm.y, 4, 0, Math.PI * 2);
        ctx.fillStyle = VTO_COLORS.original;
        ctx.fill();
        ctx.strokeStyle = "white";
        ctx.lineWidth = 1;
        ctx.stroke();

        // Label
        ctx.fillStyle = VTO_COLORS.original;
        ctx.font = "10px Tajawal, sans-serif";
        ctx.textAlign = "center";
        ctx.fillText(lm.key, lm.x, lm.y - 8);
      });

      // Draw dashed connecting line for before profile
      if (landmarks.length > 1) {
        ctx.beginPath();
        ctx.setLineDash([4, 3]);
        ctx.strokeStyle = VTO_COLORS.original;
        ctx.lineWidth = 1.5;
        ctx.globalAlpha = 0.5;
        // Connect soft tissue landmarks if available
        const softKeys = ['Pn', 'Cm', 'LS', 'LI'];
        softKeys.forEach((k, i) => {
          const lm = landmarkMap.get(k);
          if (!lm) return;
          if (i === 0) ctx.moveTo(lm.x, lm.y);
          else ctx.lineTo(lm.x, lm.y);
        });
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.globalAlpha = 1;
      }
    }

    if (!vtoData) return;

    // Draw target positions and arrows
    vtoData.targetMovements.forEach((mv) => {
      const origLm = landmarkMap.get(mv.landmark);
      if (!origLm) return;

      const targetX = origLm.x + mv.deltaX * animProgress;
      const targetY = origLm.y + mv.deltaY * animProgress;

      // Draw arrow from original to target
      if (showArrows && (mv.deltaX !== 0 || mv.deltaY !== 0)) {
        ctx.beginPath();
        ctx.setLineDash([4, 3]);
        ctx.moveTo(origLm.x, origLm.y);
        ctx.lineTo(targetX, targetY);
        ctx.strokeStyle = VTO_COLORS.arrow;
        ctx.lineWidth = 1.5;
        ctx.stroke();
        ctx.setLineDash([]);

        // Arrow head
        const angle = Math.atan2(targetY - origLm.y, targetX - origLm.x);
        const headLen = 6;
        ctx.beginPath();
        ctx.moveTo(targetX, targetY);
        ctx.lineTo(
          targetX - headLen * Math.cos(angle - Math.PI / 6),
          targetY - headLen * Math.sin(angle - Math.PI / 6)
        );
        ctx.moveTo(targetX, targetY);
        ctx.lineTo(
          targetX - headLen * Math.cos(angle + Math.PI / 6),
          targetY - headLen * Math.sin(angle + Math.PI / 6)
        );
        ctx.strokeStyle = VTO_COLORS.arrow;
        ctx.lineWidth = 1.5;
        ctx.stroke();
      }

      // Draw target position (#22c55e - after profile)
      if (showTarget) {
        ctx.beginPath();
        ctx.arc(targetX, targetY, 4, 0, Math.PI * 2);
        ctx.fillStyle = VTO_COLORS.target;
        ctx.fill();
        ctx.strokeStyle = "white";
        ctx.lineWidth = 1;
        ctx.stroke();

        if (animProgress >= 1) {
          ctx.fillStyle = VTO_COLORS.target;
          ctx.font = "9px Tajawal, sans-serif";
          ctx.textAlign = "center";
          ctx.fillText(mv.landmark + "'", targetX, targetY - 8);
        }
      }
    });

    // Draw after profile connecting line (#22c55e solid)
    if (showTarget && vtoData && animProgress >= 1) {
      ctx.beginPath();
      ctx.strokeStyle = VTO_COLORS.target;
      ctx.lineWidth = 2;
      ctx.globalAlpha = 0.7;
      const softKeys = ['Pn', 'Cm', 'LS', 'LI'];
      softKeys.forEach((k, i) => {
        const mv = vtoData.targetMovements.find(m => m.landmark === k);
        const lm = landmarkMap.get(k);
        if (!lm) return;
        const tx = lm.x + (mv?.deltaX ?? 0);
        const ty = lm.y + (mv?.deltaY ?? 0);
        if (i === 0) ctx.moveTo(tx, ty);
        else ctx.lineTo(tx, ty);
      });
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
  }

  return (
    <div className="space-y-4" dir="rtl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Sparkles className="w-4 h-4" style={{ color: "#a855f7" }} />
          <h3 className="text-sm font-bold" style={{ color: "#0d2137" }}>VTO — الهدف العلاجي البصري</h3>
          <span
            className="text-[10px] rounded-full font-medium"
            style={{ padding: "2px 10px", backgroundColor: "#a855f718", color: "#a855f7" }}
          >AI مقترح</span>
        </div>
        <div className="flex gap-2">
          {vtoData && (
            <>
              <button onClick={startAnimation}
                className="text-xs px-3 py-1.5 rounded-xl flex items-center gap-1 transition"
                style={{ backgroundColor: "#3d7ab510", color: "#3d7ab5" }}
              >
                <Play className="w-3 h-3" />
                تشغيل الحركة
              </button>
              <button onClick={handleReset}
                className="text-xs px-3 py-1.5 rounded-xl flex items-center gap-1 transition"
                style={{ backgroundColor: "#f7fafd", color: "#64748b" }}
              >
                <RotateCcw className="w-3 h-3" />
                إعادة
              </button>
            </>
          )}
        </div>
      </div>

      {/* Generate button */}
      {!vtoData && !vtoMutation.isPending && (
        <button
          onClick={handleGenerateVto}
          disabled={!caseData}
          className="flex items-center gap-2 px-5 py-2.5 text-sm font-medium rounded-xl text-white hover:opacity-90 disabled:opacity-50 transition"
          style={{ backgroundColor: "#a855f7" }}
        >
          <Sparkles className="w-4 h-4" />
          إنشاء VTO بالذكاء الاصطناعي
        </button>
      )}

      {vtoMutation.isPending && (
        <div className="flex items-center justify-center gap-2 py-6">
          <div className="w-5 h-5 border-2 rounded-full animate-spin" style={{ borderColor: "#a855f740", borderTopColor: "#a855f7" }} />
          <span className="text-sm" style={{ color: "#a855f7" }}>جاري تحليل الحالة واقتراح VTO...</span>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 p-3 rounded-xl text-sm" style={{ backgroundColor: "#ef444410", color: "#ef4444" }}>
          <AlertTriangle className="w-4 h-4" />
          {error}
        </div>
      )}

      {/* Canvas */}
      {vtoData && (
        <div className="relative rounded-xl border overflow-hidden" style={{ backgroundColor: "#f7fafd", borderColor: "#e8f0f9" }}>
          <canvas ref={canvasRef} className="w-full" style={{ maxHeight: "500px" }} />

          {/* Legend */}
          <div className="absolute top-3 start-3 rounded-xl px-3 py-2 space-y-1.5 text-xs"
            style={{ backgroundColor: "rgba(255,255,255,0.92)", backdropFilter: "blur(4px)" }}>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={showOriginal} onChange={(e) => setShowOriginal(e.target.checked)} className="rounded accent-[#3d7ab5]" />
              <span className="w-3 h-0.5 rounded" style={{ backgroundColor: VTO_COLORS.original, borderStyle: "dashed" }} />
              <span style={{ color: "#0d2137" }}>الملف قبل العلاج (متقطع)</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={showTarget} onChange={(e) => setShowTarget(e.target.checked)} className="rounded accent-[#22c55e]" />
              <span className="w-3 h-0.5 rounded" style={{ backgroundColor: VTO_COLORS.target }} />
              <span style={{ color: "#0d2137" }}>الملف بعد العلاج</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={showArrows} onChange={(e) => setShowArrows(e.target.checked)} className="rounded accent-[#f5922e]" />
              <span className="w-3 h-0.5 rounded" style={{ backgroundColor: VTO_COLORS.arrow }} />
              <span style={{ color: "#0d2137" }}>اتجاه الحركة</span>
            </label>
          </div>
        </div>
      )}

      {/* Movement details */}
      {vtoData && (
        <div className="space-y-3">
          <h4 className="text-xs font-bold" style={{ color: "#0d2137" }}>تفاصيل التحركات المقترحة</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
            {vtoData.targetMovements.map((mv, i) => (
              <div key={i} className="flex items-center gap-2 border rounded-xl px-3 py-2"
                style={{ backgroundColor: "#ffffff", borderColor: "#e8f0f9" }}>
                <span className="text-xs font-mono font-bold" style={{ color: "#a855f7" }}>{mv.landmark}</span>
                <span className="text-xs" style={{ color: "#64748b" }}>
                  Δx={mv.deltaX > 0 ? "+" : ""}{mv.deltaX}mm Δy={mv.deltaY > 0 ? "+" : ""}{mv.deltaY}mm
                </span>
                <span className="text-xs flex-1" style={{ color: "#0d2137" }}>{mv.description}</span>
              </div>
            ))}
          </div>

          {vtoData.expectedOutcomes.length > 0 && (
            <div className="rounded-xl p-3" style={{ backgroundColor: "#22c55e10" }}>
              <h5 className="text-xs font-bold mb-1" style={{ color: "#22c55e" }}>النتائج المتوقعة</h5>
              <ul className="space-y-0.5">
                {vtoData.expectedOutcomes.map((o, i) => (
                  <li key={i} className="text-xs" style={{ color: "#0d2137" }}>• {o}</li>
                ))}
              </ul>
            </div>
          )}

          {vtoData.treatmentSequence.length > 0 && (
            <div className="rounded-xl p-3" style={{ backgroundColor: "#f0f5fb" }}>
              <h5 className="text-xs font-bold mb-1" style={{ color: "#3d7ab5" }}>التسلسل العلاجي المقترح</h5>
              <ol className="space-y-0.5 list-decimal list-inside">
                {vtoData.treatmentSequence.map((s, i) => (
                  <li key={i} className="text-xs" style={{ color: "#0d2137" }}>{s}</li>
                ))}
              </ol>
            </div>
          )}

          {vtoData.warnings.length > 0 && (
            <div className="rounded-xl p-3 flex items-start gap-2" style={{ backgroundColor: "#f59e0b10" }}>
              <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" style={{ color: "#f59e0b" }} />
              <div>
                <h5 className="text-xs font-bold mb-1" style={{ color: "#f59e0b" }}>تحذيرات</h5>
                <ul className="space-y-0.5">
                  {vtoData.warnings.map((w, i) => (
                    <li key={i} className="text-xs" style={{ color: "#0d2137" }}>• {w}</li>
                  ))}
                </ul>
              </div>
            </div>
          )}

          {vtoData.estimatedDuration && (
            <div className="flex items-center gap-2 text-xs" style={{ color: "#64748b" }}>
              <Info className="w-3.5 h-3.5" />
              المدة التقديرية: <span className="font-semibold" style={{ color: "#0d2137" }}>{vtoData.estimatedDuration}</span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
