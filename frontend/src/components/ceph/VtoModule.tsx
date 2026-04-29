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

const VTO_COLORS = {
  original: "#3B82F6",    // blue
  target: "#10B981",      // green
  arrow: "#F59E0B",       // amber
  arrowHover: "#EF4444",  // red
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
      // Ease-out curve
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

    // Draw background image if available
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

    // Draw original landmarks
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

      // Draw target position
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
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Sparkles className="w-4 h-4 text-purple-600" />
          <h3 className="text-sm font-bold text-gray-800">VTO — الهدف العلاجي البصري</h3>
          <span className="text-[10px] px-1.5 py-0.5 bg-purple-100 text-purple-600 rounded-full font-medium">AI مقترح</span>
        </div>
        <div className="flex gap-2">
          {vtoData && (
            <>
              <button onClick={startAnimation} className="text-xs px-3 py-1.5 rounded-lg bg-teal-50 text-teal-700 hover:bg-teal-100 transition flex items-center gap-1">
                <Play className="w-3 h-3" />
                تشغيل الحركة
              </button>
              <button onClick={handleReset} className="text-xs px-3 py-1.5 rounded-lg bg-gray-50 text-gray-600 hover:bg-gray-100 transition flex items-center gap-1">
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
          className="flex items-center gap-2 px-5 py-2.5 text-sm font-medium rounded-lg bg-purple-600 text-white hover:bg-purple-700 disabled:opacity-50 transition"
        >
          <Sparkles className="w-4 h-4" />
          إنشاء VTO بالذكاء الاصطناعي
        </button>
      )}

      {vtoMutation.isPending && (
        <div className="flex items-center justify-center gap-2 py-6">
          <div className="w-5 h-5 border-2 border-purple-300 border-t-purple-600 rounded-full animate-spin" />
          <span className="text-sm text-purple-600">جاري تحليل الحالة واقتراح VTO...</span>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 p-3 bg-red-50 rounded-lg text-sm text-red-600">
          <AlertTriangle className="w-4 h-4" />
          {error}
        </div>
      )}

      {/* Canvas */}
      {vtoData && (
        <div className="relative bg-gray-50 rounded-xl border border-gray-200 overflow-hidden">
          <canvas ref={canvasRef} className="w-full" style={{ maxHeight: "500px" }} />

          {/* Legend */}
          <div className="absolute top-3 start-3 bg-white/90 backdrop-blur-sm rounded-lg px-3 py-2 space-y-1.5 text-xs">
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={showOriginal} onChange={(e) => setShowOriginal(e.target.checked)} className="rounded" />
              <span className="w-3 h-3 rounded-full" style={{ backgroundColor: VTO_COLORS.original }} />
              المواقع الأصلية
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={showTarget} onChange={(e) => setShowTarget(e.target.checked)} className="rounded" />
              <span className="w-3 h-3 rounded-full" style={{ backgroundColor: VTO_COLORS.target }} />
              المواقع المستهدفة
            </label>
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={showArrows} onChange={(e) => setShowArrows(e.target.checked)} className="rounded" />
              <span className="w-3 h-0.5 bg-amber-400 rounded" />
              اتجاه الحركة
            </label>
          </div>
        </div>
      )}

      {/* Movement details */}
      {vtoData && (
        <div className="space-y-3">
          <h4 className="text-xs font-bold text-gray-700">تفاصيل التحركات المقترحة</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
            {vtoData.targetMovements.map((mv, i) => (
              <div key={i} className="flex items-center gap-2 bg-white border border-gray-100 rounded-lg px-3 py-2">
                <span className="text-xs font-mono font-bold text-purple-700">{mv.landmark}</span>
                <span className="text-xs text-gray-500">
                  Δx={mv.deltaX > 0 ? "+" : ""}{mv.deltaX}mm Δy={mv.deltaY > 0 ? "+" : ""}{mv.deltaY}mm
                </span>
                <span className="text-xs text-gray-700 flex-1">{mv.description}</span>
              </div>
            ))}
          </div>

          {vtoData.expectedOutcomes.length > 0 && (
            <div className="bg-green-50 rounded-lg p-3">
              <h5 className="text-xs font-bold text-green-700 mb-1">النتائج المتوقعة</h5>
              <ul className="space-y-0.5">
                {vtoData.expectedOutcomes.map((o, i) => (
                  <li key={i} className="text-xs text-green-800">• {o}</li>
                ))}
              </ul>
            </div>
          )}

          {vtoData.treatmentSequence.length > 0 && (
            <div className="bg-blue-50 rounded-lg p-3">
              <h5 className="text-xs font-bold text-blue-700 mb-1">التسلسل العلاجي المقترح</h5>
              <ol className="space-y-0.5 list-decimal list-inside">
                {vtoData.treatmentSequence.map((s, i) => (
                  <li key={i} className="text-xs text-blue-800">{s}</li>
                ))}
              </ol>
            </div>
          )}

          {vtoData.warnings.length > 0 && (
            <div className="bg-amber-50 rounded-lg p-3 flex items-start gap-2">
              <AlertTriangle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
              <div>
                <h5 className="text-xs font-bold text-amber-700 mb-1">تحذيرات</h5>
                <ul className="space-y-0.5">
                  {vtoData.warnings.map((w, i) => (
                    <li key={i} className="text-xs text-amber-800">• {w}</li>
                  ))}
                </ul>
              </div>
            </div>
          )}

          {vtoData.estimatedDuration && (
            <div className="flex items-center gap-2 text-xs text-gray-600">
              <Info className="w-3.5 h-3.5" />
              المدة التقديرية: <span className="font-semibold">{vtoData.estimatedDuration}</span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
