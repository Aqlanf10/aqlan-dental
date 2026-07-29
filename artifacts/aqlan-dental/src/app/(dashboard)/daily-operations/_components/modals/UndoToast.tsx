/**
 * UndoToast — bottom-center toast offering a 5-second window to undo a
 * destructive journey action (cancel / no-show / cancel-queue).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */


import { useState, useEffect, useRef } from "react";
import { X, Undo2 } from "lucide-react";
import { NAVY, ORANGE, type UndoAction } from "../../_lib/constants";

export function UndoToast({
  action, onUndo, onDismiss,
}: {
  action: UndoAction;
  onUndo: () => void;
  onDismiss: () => void;
}) {
  const [secondsLeft, setSecondsLeft] = useState(5);
  const timerRef = useRef<ReturnType<typeof setInterval>>(undefined);

  useEffect(() => {
    timerRef.current = setInterval(() => {
      setSecondsLeft(prev => {
        if (prev <= 1) {
          if (timerRef.current) clearInterval(timerRef.current);
          onDismiss();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, [onDismiss]);

  const actionLabels: Record<string, string> = {
    Cancel: "إلغاء الموعد",
    NoShow: "لم يحضر",
    CancelQueue: "إلغاء من الانتظار",
  };

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-[60] flex items-center gap-3 px-5 py-3 rounded-xl shadow-xl border"
      style={{ background: "#fff", borderColor: "#e8f0f9", minWidth: 320 }}>
      <Undo2 className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
      <div className="flex-1">
        <div className="text-xs font-bold" style={{ color: NAVY }}>
          {actionLabels[action.type] ?? "إجراء"} — {action.patientName}
        </div>
        <div className="text-[10px]" style={{ color: "#94a3b8" }}>
          تراجع خلال {secondsLeft} ثانية
        </div>
      </div>
      <button onClick={onUndo}
        className="px-3 py-1.5 rounded-lg text-xs font-bold"
        style={{ background: `${ORANGE}15`, color: ORANGE, border: `1px solid ${ORANGE}30` }}>
        تراجع
      </button>
      <button onClick={onDismiss} className="p-1 rounded hover:bg-gray-100">
        <X className="w-3 h-3 text-gray-400" />
      </button>
    </div>
  );
}
