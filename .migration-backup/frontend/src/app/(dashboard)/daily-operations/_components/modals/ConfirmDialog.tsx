/**
 * ConfirmDialog — generic confirmation modal for cancel / no-show / change
 * room / complete-visit actions.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

export function ConfirmDialog({
  open, onClose, type, patientName, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  type: "Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete";
  patientName: string;
  isPending: boolean;
  onConfirm: () => void;
}) {
  if (!open) return null;

  const typeLabels: Record<string, string> = {
    Cancel: "إلغاء الموعد",
    NoShow: "تسجيل عدم الحضور",
    CancelQueue: "إلغاء الانتظار",
    ChangeRoom: "تغيير الغرفة",
    Complete: "إكمال الزيارة",
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4">
        <div className="p-5 text-center">
          <h3 className="text-sm font-bold text-[#1a3a5c] mb-2">{typeLabels[type] ?? type}</h3>
          <p className="text-xs text-gray-600">
            {type === "Cancel"
              ? `هل تريد إلغاء موعد ${patientName}؟`
              : type === "NoShow"
                ? `هل تريد تسجيل عدم حضور ${patientName}؟`
                : type === "CancelQueue"
                  ? `هل تريد إلغاء ${patientName} من الانتظار؟`
                  : type === "Complete"
                    ? `هل تريد إكمال زيارة ${patientName}؟`
                    : `هل تريد تغيير الغرفة لـ ${patientName}؟`}
          </p>
        </div>
        <div className="flex gap-2 justify-center p-4 border-t">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
          >
            إلغاء
          </button>
          <button
            onClick={onConfirm}
            disabled={isPending}
            className="px-4 py-2 text-xs font-bold rounded-lg bg-red-600 text-white hover:bg-red-700 transition disabled:opacity-50"
          >
            {isPending ? "جارٍ..." : "تأكيد"}
          </button>
        </div>
      </div>
    </div>
  );
}
