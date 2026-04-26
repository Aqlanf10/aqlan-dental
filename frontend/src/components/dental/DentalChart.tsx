"use client";
import { useEffect, useState } from "react";
import { X } from "lucide-react";
import type { DentalChart as ChartType, ToothCondition } from "@/types/dental";
import api from "@/lib/api";
import { cn } from "@/lib/utils";

// FDI tooth numbering
// Upper right: 18-11, Upper left: 21-28
// Lower right: 48-41, Lower left: 31-38
const UPPER_RIGHT = ["18", "17", "16", "15", "14", "13", "12", "11"];
const UPPER_LEFT  = ["21", "22", "23", "24", "25", "26", "27", "28"];
const LOWER_RIGHT = ["48", "47", "46", "45", "44", "43", "42", "41"];
const LOWER_LEFT  = ["31", "32", "33", "34", "35", "36", "37", "38"];

const CONDITION_CONFIG: Record<string, { label: string; color: string; bgColor: string }> = {
  healthy:   { label: "سليم",       color: "text-gray-700",  bgColor: "bg-white border-gray-300" },
  decay:     { label: "تسوس",        color: "text-red-700",   bgColor: "bg-red-100 border-red-400" },
  filled:    { label: "محشو",        color: "text-yellow-700",bgColor: "bg-yellow-100 border-yellow-400" },
  rct:       { label: "معالج جذر",   color: "text-purple-700",bgColor: "bg-purple-100 border-purple-400" },
  crown:     { label: "تاج",         color: "text-blue-700",  bgColor: "bg-blue-100 border-blue-400" },
  extracted: { label: "مخلوع",       color: "text-gray-500",  bgColor: "bg-gray-200 border-gray-400" },
  missing:   { label: "مفقود",       color: "text-gray-400",  bgColor: "bg-white border-dashed border-gray-300" },
  implant:   { label: "زرع",         color: "text-orange-700",bgColor: "bg-orange-100 border-orange-400" },
};

const SURFACES = ["M", "O", "D", "B", "F"]; // Mesial, Occlusal, Distal, Buccal, Facial

interface Props {
  patientId: string;
}

export function DentalChart({ patientId }: Props) {
  const [chart, setChart] = useState<ChartType | null>(null);
  const [loading, setLoading] = useState(true);
  const [selectedTooth, setSelectedTooth] = useState<string | null>(null);

  useEffect(() => {
    api.get<ChartType>(`/api/dental-chart/${patientId}`)
      .then((r) => setChart(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [patientId]);

  const getTooth = (num: string): ToothCondition | undefined =>
    chart?.teeth.find((t) => t.toothNumber === num);

  const handleToothUpdate = (updated: ToothCondition) => {
    if (!chart) return;
    const existing = chart.teeth.find((t) => t.toothNumber === updated.toothNumber);
    const teeth = existing
      ? chart.teeth.map((t) => (t.toothNumber === updated.toothNumber ? updated : t))
      : [...chart.teeth, updated];
    setChart({ ...chart, teeth });
  };

  if (loading) {
    return <div className="h-64 bg-gray-100 rounded-xl animate-pulse" />;
  }

  return (
    <div className="space-y-4">
      {/* Legend */}
      <div className="flex flex-wrap gap-2">
        {Object.entries(CONDITION_CONFIG).map(([key, cfg]) => (
          <div key={key} className="flex items-center gap-1.5 text-xs">
            <div className={cn("w-3 h-3 rounded border", cfg.bgColor)} />
            <span className="text-gray-600">{cfg.label}</span>
          </div>
        ))}
      </div>

      {/* Chart */}
      <div className="bg-gradient-to-b from-gray-50 to-white rounded-xl border border-gray-200 p-6">
        {/* Upper jaw */}
        <div className="space-y-1">
          <div className="text-xs text-gray-400 text-center mb-2">الفك العلوي</div>
          <div className="grid grid-cols-16 gap-1" style={{ gridTemplateColumns: "repeat(16, 1fr)" }}>
            {[...UPPER_RIGHT, ...UPPER_LEFT].map((num) => (
              <ToothBtn
                key={num}
                toothNumber={num}
                tooth={getTooth(num)}
                onClick={() => setSelectedTooth(num)}
                isUpper
              />
            ))}
          </div>
          {/* Center divider */}
          <div className="flex items-center my-2">
            <div className="flex-1 h-px bg-gray-200" />
            <span className="px-3 text-xs text-gray-400">الخط المنتصف</span>
            <div className="flex-1 h-px bg-gray-200" />
          </div>
          {/* Lower jaw */}
          <div className="grid grid-cols-16 gap-1" style={{ gridTemplateColumns: "repeat(16, 1fr)" }}>
            {[...LOWER_RIGHT, ...LOWER_LEFT].map((num) => (
              <ToothBtn
                key={num}
                toothNumber={num}
                tooth={getTooth(num)}
                onClick={() => setSelectedTooth(num)}
                isUpper={false}
              />
            ))}
          </div>
          <div className="text-xs text-gray-400 text-center mt-2">الفك السفلي</div>
        </div>
      </div>

      {/* Edit modal */}
      {selectedTooth && (
        <ToothEditModal
          patientId={patientId}
          toothNumber={selectedTooth}
          current={getTooth(selectedTooth)}
          onClose={() => setSelectedTooth(null)}
          onSaved={(updated) => {
            handleToothUpdate(updated);
            setSelectedTooth(null);
          }}
        />
      )}
    </div>
  );
}

// ─── Tooth Button ─────────────────────────────────────────────────────────────
function ToothBtn({ toothNumber, tooth, onClick, isUpper }: {
  toothNumber: string;
  tooth?: ToothCondition;
  onClick: () => void;
  isUpper: boolean;
}) {
  const cfg = tooth?.condition ? CONDITION_CONFIG[tooth.condition] : undefined;
  const bgClass = cfg?.bgColor ?? "bg-white border-gray-300";

  return (
    <button
      type="button"
      onClick={onClick}
      title={`${toothNumber}${tooth?.condition ? ` — ${CONDITION_CONFIG[tooth.condition]?.label ?? tooth.condition}` : ""}`}
      className={cn(
        "aspect-[2/3] flex flex-col items-center justify-center rounded border-2 hover:ring-2 hover:ring-clinic-teal hover:ring-offset-1 transition text-xs",
        bgClass,
        !isUpper && "rotate-180"
      )}
    >
      <span className={cn("font-mono font-bold", !isUpper && "rotate-180", cfg?.color ?? "text-gray-600")}>
        {toothNumber}
      </span>
      {tooth?.surfacesAffected && (
        <span className={cn("text-[9px] opacity-70 font-mono", !isUpper && "rotate-180")}>
          {tooth.surfacesAffected}
        </span>
      )}
    </button>
  );
}

// ─── Edit Modal ───────────────────────────────────────────────────────────────
function ToothEditModal({ patientId, toothNumber, current, onClose, onSaved }: {
  patientId: string;
  toothNumber: string;
  current?: ToothCondition;
  onClose: () => void;
  onSaved: (t: ToothCondition) => void;
}) {
  const [condition, setCondition] = useState(current?.condition ?? "");
  const [surfaces, setSurfaces] = useState<string[]>(
    current?.surfacesAffected ? current.surfacesAffected.split("") : []
  );
  const [treatmentDone, setTreatmentDone] = useState(current?.treatmentDone ?? "");
  const [notes, setNotes] = useState(current?.notes ?? "");
  const [saving, setSaving] = useState(false);

  const toggleSurface = (s: string) => {
    setSurfaces(surfaces.includes(s) ? surfaces.filter((x) => x !== s) : [...surfaces, s]);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      const { data } = await api.put<ToothCondition>(`/api/dental-chart/${patientId}/teeth`, {
        toothNumber,
        condition: condition || null,
        surfacesAffected: surfaces.length ? surfaces.join("") : null,
        treatmentDone: treatmentDone || null,
        notes: notes || null,
      });
      onSaved(data);
    } catch {
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4"
      onClick={onClose}
    >
      <div
        className="bg-white rounded-xl shadow-xl max-w-md w-full p-5 space-y-4"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-bold text-gray-900">
            السن <span className="font-mono text-clinic-teal">{toothNumber}</span>
          </h3>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>

        {/* Condition */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">الحالة</label>
          <div className="grid grid-cols-2 gap-2">
            {Object.entries(CONDITION_CONFIG).map(([key, cfg]) => (
              <button
                key={key}
                type="button"
                onClick={() => setCondition(key)}
                className={cn(
                  "px-3 py-2 text-sm rounded-lg border-2 transition text-start",
                  condition === key
                    ? cfg.bgColor + " border-clinic-teal"
                    : "bg-white border-gray-200 hover:border-gray-300"
                )}
              >
                <span className={cn("font-medium", condition === key ? cfg.color : "text-gray-700")}>
                  {cfg.label}
                </span>
              </button>
            ))}
          </div>
        </div>

        {/* Surfaces */}
        {(condition === "decay" || condition === "filled") && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">الأسطح المصابة</label>
            <div className="flex gap-2 flex-wrap">
              {SURFACES.map((s) => (
                <button
                  key={s}
                  type="button"
                  onClick={() => toggleSurface(s)}
                  className={cn(
                    "w-9 h-9 rounded-lg border-2 font-mono font-bold text-sm transition",
                    surfaces.includes(s)
                      ? "bg-clinic-teal text-white border-clinic-teal"
                      : "bg-white text-gray-600 border-gray-200 hover:border-gray-300"
                  )}
                >
                  {s}
                </button>
              ))}
            </div>
            <p className="text-xs text-gray-400 mt-1">
              M=الأنسي، O=الإطباقي، D=الوحشي، B=الشدقي، F=الشفوي
            </p>
          </div>
        )}

        {/* Treatment done */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">العلاج المنجز</label>
          <input
            value={treatmentDone}
            onChange={(e) => setTreatmentDone(e.target.value)}
            className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
            placeholder="حشو، قلع، علاج جذر..."
          />
        </div>

        {/* Notes */}
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:outline-none focus:ring-2 focus:ring-clinic-teal"
          />
        </div>

        {/* Actions */}
        <div className="flex justify-end gap-2 pt-2">
          <button
            onClick={onClose}
            className="px-4 py-1.5 text-sm rounded-lg border border-gray-300 hover:bg-gray-50 transition"
          >
            إلغاء
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="px-4 py-1.5 text-sm font-medium rounded-lg bg-clinic-teal text-white hover:opacity-90 disabled:opacity-60 transition"
          >
            {saving ? "جارٍ الحفظ..." : "حفظ"}
          </button>
        </div>
      </div>
    </div>
  );
}
