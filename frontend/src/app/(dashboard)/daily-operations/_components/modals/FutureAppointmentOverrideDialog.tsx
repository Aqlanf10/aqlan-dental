"use client";

import { useEffect, useState } from "react";
import { AlertTriangle } from "lucide-react";
import { ModalShell } from "./ModalShell";

const operationLabels = {
  Intake: "تسجيل الوصول",
  SendToQueue: "الإضافة إلى الانتظار",
  StartVisit: "بدء العلاج",
} as const;

export type FutureAppointmentOverrideOperation = keyof typeof operationLabels;

export function FutureAppointmentOverrideDialog({
  open,
  onClose,
  patientName,
  appointmentDate,
  operation,
  isPending,
  onConfirm,
}: {
  open: boolean;
  onClose: () => void;
  patientName: string;
  appointmentDate?: string;
  operation: FutureAppointmentOverrideOperation;
  isPending: boolean;
  onConfirm: (reason: string) => void;
}) {
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (!open) setReason("");
  }, [open]);

  const normalizedReason = reason.trim();

  return (
    <ModalShell
      open={open}
      onClose={onClose}
      title="تجاوز موعد مستقبلي"
      icon={AlertTriangle}
      iconColor="#d97706"
    >
      <div className="space-y-4" dir="rtl">
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs leading-6 text-amber-900">
          الموعد الخاص بالمريض <span className="font-extrabold">{patientName}</span>
          {appointmentDate ? (
            <> بتاريخ <span className="font-extrabold" dir="ltr">{appointmentDate}</span></>
          ) : null}
          ، وهو موعد لاحق لتاريخ عمل العيادة. سيتم تسجيل تجاوز إداري لعملية
          <span className="font-extrabold"> {operationLabels[operation]}</span> في سجل التدقيق.
        </div>

        <div>
          <label className="mb-1.5 block text-xs font-bold text-gray-700">
            سبب التجاوز الإداري *
          </label>
          <textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            rows={4}
            maxLength={500}
            autoFocus
            placeholder="مثال: المريض حضر من سفر بعيد وتمت الموافقة على خدمته اليوم"
            className="w-full rounded-xl border border-gray-200 px-3 py-2 text-sm outline-none focus:border-amber-500 focus:ring-2 focus:ring-amber-500/15"
          />
          <div className="mt-1 text-left text-[10px] text-gray-400" dir="ltr">
            {reason.length}/500
          </div>
        </div>

        <div className="flex gap-2 pt-1">
          <button
            type="button"
            onClick={onClose}
            disabled={isPending}
            className="flex-1 rounded-xl bg-gray-100 py-2.5 text-sm font-bold text-gray-600 disabled:opacity-50"
          >
            إلغاء
          </button>
          <button
            type="button"
            onClick={() => onConfirm(normalizedReason)}
            disabled={isPending || !normalizedReason}
            className="flex-1 rounded-xl bg-amber-600 py-2.5 text-sm font-bold text-white hover:bg-amber-700 disabled:opacity-50"
          >
            {isPending ? "جارٍ التنفيذ..." : "تأكيد التجاوز"}
          </button>
        </div>
      </div>
    </ModalShell>
  );
}
