"use client";
import { useState } from "react";
import { X, Save } from "lucide-react";
import { cn } from "@/lib/utils";
import { CONDITION_LABELS, SURFACES } from "./EnhancedDentalChart";
import type { ToothData } from "./EnhancedDentalChart";

interface ToothEditorModalProps {
  toothNumber: number;
  data?: ToothData;
  onSave: (data: { toothNumber: number; condition: string; surfacesAffected: string; treatmentDone: string; notes: string }) => void;
  onClose: () => void;
}

const TREATMENT_OPTIONS = [
  "حشو مؤقت", "حشو أملغم", "حشو كومبوزيت", "حشو زجاجي",
  "معالجة جذر", "تاج بورسلين", "تاج زركون", "تاج金属",
  "جسر", "طقم جزئي", "طقم كامل", "خلع",
  "خلع جراحي", "زرعة", "تبييض", "ختم فلورايد",
  "ترميم", "أخرى",
];

export function ToothEditorModal({ toothNumber, data, onSave, onClose }: ToothEditorModalProps) {
  const [condition, setCondition] = useState(data?.condition || "healthy");
  const [surfaces, setSurfaces] = useState<string[]>(data?.surfacesAffected?.split(",").filter(Boolean) || []);
  const [treatmentDone, setTreatmentDone] = useState(data?.treatmentDone || "");
  const [notes, setNotes] = useState(data?.notes || "");
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    await onSave({
      toothNumber,
      condition,
      surfacesAffected: surfaces.join(","),
      treatmentDone,
      notes,
    });
    setSaving(false);
  };

  const toggleSurface = (s: string) => {
    setSurfaces((prev) => prev.includes(s) ? prev.filter((x) => x !== s) : [...prev, s]);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between p-4 border-b border-gray-100">
          <h3 className="font-bold text-gray-900">سن رقم {toothNumber}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-4 space-y-4">
          {/* Condition */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">الحالة</label>
            <div className="grid grid-cols-3 gap-1.5">
              {Object.entries(CONDITION_LABELS).map(([key, label]) => (
                <button
                  key={key}
                  onClick={() => setCondition(key)}
                  className={cn(
                    "px-2 py-1.5 text-xs rounded-lg border transition",
                    condition === key
                      ? "border-clinic-blue bg-clinic-blue-50 text-clinic-blue font-medium"
                      : "border-gray-200 text-gray-600 hover:bg-gray-50"
                  )}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>

          {/* Surfaces */}
          {condition !== "healthy" && condition !== "extraction" && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">الأسطح المتأثرة</label>
              <div className="flex gap-1.5">
                {SURFACES.map((s) => (
                  <button
                    key={s}
                    onClick={() => toggleSurface(s)}
                    className={cn(
                      "w-10 h-10 text-xs font-bold rounded-lg border transition",
                      surfaces.includes(s)
                        ? "border-clinic-blue bg-clinic-blue-50 text-clinic-blue"
                        : "border-gray-200 text-gray-400 hover:bg-gray-50"
                    )}
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Treatment Done */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">العلاج المُنفّذ</label>
            <select
              value={treatmentDone}
              onChange={(e) => setTreatmentDone(e.target.value)}
              className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none"
            >
              <option value="">لا يوجد</option>
              {TREATMENT_OPTIONS.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>

          {/* Notes */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1.5">ملاحظات</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              className="w-full px-3 py-2 text-sm rounded-lg border border-gray-300 focus:ring-2 focus:ring-clinic-blue focus:outline-none resize-none"
              placeholder="ملاحظات إضافية عن السن..."
            />
          </div>
        </div>

        <div className="flex gap-2 p-4 border-t border-gray-100">
          <button onClick={onClose}
            className="flex-1 px-4 py-2 text-sm rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50">
            إلغاء
          </button>
          <button onClick={handleSave} disabled={saving}
            className="flex-1 flex items-center justify-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg bg-clinic-blue text-white hover:bg-clinic-navy-700 disabled:opacity-60">
            <Save className="w-4 h-4" />
            {saving ? "جارٍ الحفظ..." : "حفظ"}
          </button>
        </div>
      </div>
    </div>
  );
}
