"use client";
import { useCallback } from "react";
import { cn } from "@/lib/utils";

// FDI tooth numbering system
const UPPER_RIGHT = [18, 17, 16, 15, 14, 13, 12, 11];
const UPPER_LEFT = [21, 22, 23, 24, 25, 26, 27, 28];
const LOWER_LEFT = [31, 32, 33, 34, 35, 36, 37, 38];
const LOWER_RIGHT = [41, 42, 43, 44, 45, 46, 47, 48];

const CONDITION_COLORS: Record<string, string> = {
  healthy: "#4ade80",
  caries: "#f97316",
  filling: "#60a5fa",
  crown: "#a78bfa",
  root_canal: "#f472b6",
  extraction: "#9ca3af",
  bridge: "#facc15",
  implant: "#2dd4bf",
  sealant: "#818cf8",
};

const CONDITION_LABELS: Record<string, string> = {
  healthy: "سليم",
  caries: "تسوس",
  filling: "حشو",
  crown: "تاج",
  root_canal: "معالجة جذر",
  extraction: "مخلوع",
  bridge: "جسر",
  implant: "زرعة",
  sealant: "ختم فلورايد",
};

const SURFACES = ["M", "D", "B", "L", "O", "I"]; // Mesial, Distal, Buccal, Lingual, Occlusal, Incisal

interface ToothData {
  toothNumber: number;
  condition: string;
  surfacesAffected: string;
  treatmentDone: string;
  notes: string;
}

interface EnhancedDentalChartProps {
  teeth: ToothData[];
  onToothClick: (toothNumber: number) => void;
  selectedTooth?: number | null;
  readOnly?: boolean;
}

export function EnhancedDentalChart({ teeth, onToothClick, selectedTooth, readOnly }: EnhancedDentalChartProps) {
  const getToothData = useCallback((num: number) => teeth.find((t) => t.toothNumber === num), [teeth]);

  const renderTooth = (num: number) => {
    const data = getToothData(num);
    const condition = data?.condition || "healthy";
    const color = CONDITION_COLORS[condition] || CONDITION_COLORS.healthy;
    const isSelected = selectedTooth === num;

    return (
      <button
        key={num}
        onClick={() => !readOnly && onToothClick(num)}
        disabled={readOnly}
        className={cn(
          "relative flex flex-col items-center justify-center w-8 h-10 rounded-md text-[10px] font-bold transition-all",
          "hover:scale-110 hover:shadow-md",
          isSelected ? "ring-2 ring-clinic-blue shadow-lg" : "",
          readOnly ? "cursor-default" : "cursor-pointer"
        )}
        style={{ backgroundColor: color + "40", color: "#374151" }}
        title={`سن ${num}: ${CONDITION_LABELS[condition] || condition}${data?.notes ? ` - ${data.notes}` : ""}`}
      >
        <span className="text-[9px] leading-none">{num}</span>
        {data?.treatmentDone && (
          <span className="absolute -top-1 -right-1 w-2.5 h-2.5 bg-clinic-blue-500 rounded-full border border-white" />
        )}
      </button>
    );
  };

  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-bold text-gray-900 text-sm">المخطط السني</h3>
        <div className="flex flex-wrap gap-1">
          {Object.entries(CONDITION_LABELS).map(([key, label]) => (
            <div key={key} className="flex items-center gap-1">
              <div className="w-2.5 h-2.5 rounded-sm" style={{ backgroundColor: CONDITION_COLORS[key] + "60" }} />
              <span className="text-[9px] text-gray-500">{label}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Upper Jaw */}
      <div className="mb-1">
        <div className="text-[10px] text-gray-400 mb-1 text-center">الفك العلوي</div>
        <div className="flex items-center justify-center gap-0.5">
          <div className="flex gap-0.5">
            {UPPER_RIGHT.map(renderTooth)}
          </div>
          <div className="w-px h-8 bg-gray-300 mx-1" />
          <div className="flex gap-0.5">
            {UPPER_LEFT.map(renderTooth)}
          </div>
        </div>
      </div>

      {/* Divider */}
      <div className="border-t-2 border-dashed border-gray-200 my-2" />

      {/* Lower Jaw */}
      <div>
        <div className="flex items-center justify-center gap-0.5">
          <div className="flex gap-0.5">
            {LOWER_RIGHT.map(renderTooth)}
          </div>
          <div className="w-px h-8 bg-gray-300 mx-1" />
          <div className="flex gap-0.5">
            {LOWER_LEFT.map(renderTooth)}
          </div>
        </div>
        <div className="text-[10px] text-gray-400 mt-1 text-center">الفك السفلي</div>
      </div>
    </div>
  );
}

export { CONDITION_COLORS, CONDITION_LABELS, SURFACES };
export type { ToothData };
