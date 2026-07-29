/**
 * BookAppointmentModal — book a follow-up appointment.
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { useState } from "react";
import { CalendarPlus, Loader2 } from "lucide-react";
import { APPOINTMENT_TYPES, inputCls } from "../../_lib/constants";
import type { TodayJourneyItem, DoctorOption, ServiceOption } from "../../_lib/constants";
import { ModalShell } from "./ModalShell";

export function BookAppointmentModal({
  open, onClose, item, doctors, services, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  doctors: DoctorOption[];
  services: ServiceOption[];
  isPending: boolean;
  onConfirm: (data: {
    doctorId: string; date: string; startTime: string; endTime: string;
    serviceId: string; type: string; notes: string;
  }) => void;
}) {
  const [doctorId, setDoctorId] = useState(item?.doctorId ?? "");
  const [date, setDate] = useState("");
  const [startTime, setStartTime] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [type, setType] = useState("FollowUp");
  const [notes, setNotes] = useState("");

  const handleSubmit = () => {
    if (!doctorId || !date || !startTime) return;
    const start = startTime;
    const [h, m] = start.split(":").map(Number);
    const endH = h + Math.floor((m + 30) / 60);
    const endM = (m + 30) % 60;
    const endTime = `${String(endH).padStart(2, "0")}:${String(endM).padStart(2, "0")}`;
    onConfirm({ doctorId, date, startTime: start, endTime, serviceId, type, notes });
  };

  return (
    <ModalShell open={open} onClose={onClose} title="حجز موعد متابعة" icon={CalendarPlus} iconColor="#3d7ab5">
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{item?.patientName}</div>
      </div>

      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الطبيب *</label>
          <select value={doctorId} onChange={e => setDoctorId(e.target.value)} className={inputCls()}>
            <option value="">اختر الطبيب</option>
            {doctors.map(d => <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` (${d.specialty})` : ""}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>التاريخ *</label>
            <input type="date" value={date} onChange={e => setDate(e.target.value)} className={inputCls()} />
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الوقت *</label>
            <input type="time" value={startTime} onChange={e => setStartTime(e.target.value)} className={inputCls()} dir="ltr" />
          </div>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الخدمة</label>
          <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
            <option value="">اختر الخدمة</option>
            {services.map(s => <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${s.defaultPrice} ر.ي` : ""}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>نوع الموعد</label>
          <select value={type} onChange={e => setType(e.target.value)} className={inputCls()}>
            {APPOINTMENT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
          <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
        </div>
      </div>

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!doctorId || !date || !startTime || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#3d7ab5", opacity: !doctorId || !date || !startTime || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CalendarPlus className="w-4 h-4" />}
          حجز الموعد
        </button>
      </div>
    </ModalShell>
  );
}
