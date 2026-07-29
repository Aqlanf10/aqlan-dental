/**
 * WhatsAppMenu — pick a WhatsApp template and send it for the current patient.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { X, MessageCircle, Send } from "lucide-react";
import {
  WHATSAPP_TEMPLATES, normalizePhone, fmtDate, fmtTime,
} from "../../_lib/constants";
import type { TodayJourneyItem } from "../../_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";

export function WhatsAppMenu({
  open, onClose, item, summary, clinicName,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  clinicName: string;
}) {
  if (!open || !item) return null;

  const phone = normalizePhone(item.patientPhone);
  const patientName = item.patientName;
  const doctorName = item.doctorName;
  const todayDate = new Date();
  const aptDate = fmtDate(todayDate);
  const aptTime = fmtTime(item.appointmentTime);
  const remaining = summary?.financeSummary?.outstandingBalance;

  const handleSend = (template: typeof WHATSAPP_TEMPLATES[number]) => {
    const msg = template.build({ patientName, clinicName, aptDate, aptTime, doctorName, remaining });
    // Use WhatsApp Web (web.whatsapp.com) for desktop, fallback to wa.me for mobile
    const isMobile = /Android|iPhone|iPad/i.test(navigator.userAgent);
    if (isMobile) {
      window.open(`https://wa.me/${phone}?text=${encodeURIComponent(msg)}`, "_blank");
    } else {
      window.open(`https://web.whatsapp.com/send?phone=${phone}&text=${encodeURIComponent(msg)}`, "_blank");
    }
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-sm" onClick={e => e.stopPropagation()}>
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: "#25D36620" }}>
            <MessageCircle className="w-4.5 h-4.5" style={{ color: "#25D366" }} />
          </div>
          <div className="flex-1">
            <h3 className="font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>واتساب</h3>
            <p className="text-xs" style={{ color: "#64748b" }}>{patientName} — {item.patientPhone ?? "—"}</p>
          </div>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        <div className="p-3 space-y-1.5">
          {WHATSAPP_TEMPLATES.map(t => (
            <button key={t.key} onClick={() => handleSend(t)}
              className="w-full text-right px-4 py-3 rounded-xl text-sm font-medium flex items-center gap-3 transition hover:bg-[#25D36608]"
              style={{ color: "#1a3a5c" }}>
              <Send className="w-4 h-4 flex-shrink-0" style={{ color: "#25D366" }} />
              {t.label}
            </button>
          ))}
        </div>
        <div className="px-5 pb-4">
          <p className="text-[10px] text-center" style={{ color: "#94a3b8" }}>
            سيتم فتح واتساب ويب لإرسال الرسالة
          </p>
        </div>
      </div>
    </div>
  );
}
