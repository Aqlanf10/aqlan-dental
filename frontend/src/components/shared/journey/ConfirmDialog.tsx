"use client";

import { X, AlertTriangle, Loader2 } from "lucide-react";

interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  type: "Cancelled" | "Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete";
  patientName?: string;
  isPending: boolean;
  onConfirm: () => void;
  targetRoomName?: string;
  className?: string;
}

/**
 * Unified confirm dialog component used by both daily-operations and patient-journey pages.
 *
 * Supports multiple confirmation types with appropriate Arabic titles, messages,
 * and button colors.
 *
 * - "Cancelled" / "Cancel": Cancel appointment confirmation
 * - "NoShow": Mark patient as no-show confirmation
 * - "CancelQueue": Cancel from queue confirmation
 * - "ChangeRoom": Change room confirmation
 * - "Complete": Complete visit confirmation
 */
export function ConfirmDialog({
  open, onClose, type, patientName, isPending, onConfirm, targetRoomName,
}: ConfirmDialogProps) {
  if (!open) return null;

  const configs: Record<string, { title: string; body: string; btnColor: string }> = {
    Cancelled:  { title: "تأكيد إلغاء الموعد",   body: `هل أنت متأكد من إلغاء موعد المريض ${patientName ?? ""}؟ لا يمكن التراجع عن هذا الإجراء.`, btnColor: "#ef4444" },
    Cancel:     { title: "إلغاء الموعد",          body: `هل أنت متأكد من إلغاء موعد المريض ${patientName ?? ""}؟`, btnColor: "#ef4444" },
    NoShow:     { title: "لم يحضر",               body: `هل تريد تسجيل المريض ${patientName ?? ""} كـ "لم يحضر"؟ سيتم تغيير حالة الموعد.`, btnColor: "#f5922e" },
    CancelQueue:{ title: "إلغاء من الانتظار",     body: `هل أنت متأكد من إلغاء المريض ${patientName ?? ""} من قائمة الانتظار؟ سيتم إخراجه من قائمة الانتظار.`, btnColor: "#ef4444" },
    ChangeRoom: { title: "تأكيد تغيير الغرفة",    body: targetRoomName ? `هل أنت متأكد من نقل المريض ${patientName ?? ""} إلى غرفة "${targetRoomName}"؟` : `هل تريد تغيير غرفة المريض ${patientName ?? ""}؟`, btnColor: "#3d7ab5" },
    Complete:   { title: "إنهاء الزيارة",          body: `هل تريد إنهاء زيارة المريض ${patientName ?? ""}؟`, btnColor: "#16a34a" },
  };

  const cfg = configs[type] ?? configs.Cancel;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md mx-4">
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: cfg.btnColor + "15" }}>
            <AlertTriangle className="w-4.5 h-4.5" style={{ color: cfg.btnColor }} />
          </div>
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>{cfg.title}</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5">
          {patientName && (
            <div className="flex items-center gap-2 bg-gray-50 rounded-lg px-3 py-2 mb-3">
              <span className="text-[11px] text-gray-500">المريض:</span>
              <span className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{patientName}</span>
            </div>
          )}
          <p className="text-sm leading-relaxed" style={{ color: "#475569" }}>{cfg.body}</p>
        </div>

        {/* Actions */}
        <div className="flex gap-2 px-5 pb-5">
          <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
            style={{ background: "#f1f5f9", color: "#64748b" }}>
            {type === "Cancelled" ? "إلغاء" : "تراجع"}
          </button>
          <button onClick={onConfirm} disabled={isPending}
            className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
            style={{ background: cfg.btnColor, opacity: isPending ? 0.5 : 1 }}>
            {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
            تأكيد
          </button>
        </div>
      </div>
    </div>
  );
}
