
import { useState } from "react";
import { X, CalendarPlus, Loader2 } from "lucide-react";
import {
  APPOINTMENT_TYPES,
  inputCls,
} from "@/components/shared/journey/constants";
import type { DoctorOption, ServiceOption } from "@/components/shared/journey/types";

interface BookAppointmentModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string;
  patientName?: string;
  doctors: DoctorOption[];
  services: ServiceOption[];
  isPending: boolean;
  onConfirm: (data: {
    doctorId: string;
    date: string;
    startTime: string;
    endTime: string;
    serviceId: string;
    type: string;
    notes: string;
  }) => void;
  initialDoctorId?: string;
  initialServiceId?: string;
}

/**
 * Unified book appointment modal used by both daily-operations and patient-journey pages.
 *
 * - If `doctors` array is provided, shows a doctor selector dropdown.
 * - If `initialDoctorId` is provided, pre-selects that doctor.
 * - If only one doctor is in the list (or initialDoctorId is set and doctors
 *   list is empty), shows the doctor as read-only.
 */
export function BookAppointmentModal({
  open,
  onClose,
  patientName,
  doctors,
  services,
  isPending,
  onConfirm,
  initialDoctorId,
  initialServiceId,
}: BookAppointmentModalProps) {
  const [doctorId, setDoctorId] = useState(initialDoctorId ?? "");
  const [date, setDate] = useState("");
  const [startTime, setStartTime] = useState("");
  const [serviceId, setServiceId] = useState(initialServiceId ?? "");
  const [type, setType] = useState("FollowUp");
  const [notes, setNotes] = useState("");

  if (!open) return null;

  const selectedDoctor = doctors.find(d => d.id === doctorId);
  const isDoctorReadOnly = initialDoctorId && doctors.length <= 1;

  const handleSubmit = () => {
    if (!doctorId || !date || !startTime) return;
    const start = startTime;
    const [h, m] = start.split(":").map(Number);
    const endH = h + Math.floor((m + 30) / 60);
    const endM = (m + 30) % 60;
    const endTime = `${String(endH).padStart(2, "0")}:${String(endM).padStart(2, "0")}`;
    onConfirm({ doctorId, date, startTime: start, endTime, serviceId, type, notes });
  };

  const handleClose = () => {
    setDoctorId(initialDoctorId ?? "");
    setDate("");
    setStartTime("");
    setServiceId(initialServiceId ?? "");
    setType("FollowUp");
    setNotes("");
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={handleClose}>
      <div
        className="bg-white rounded-2xl shadow-xl w-full max-w-md max-h-[90vh] overflow-y-auto"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          <div className="w-9 h-9 rounded-lg flex items-center justify-center bg-[#3d7ab5]15">
            <CalendarPlus className="w-4.5 h-4.5" style={{ color: "#3d7ab5" }} />
          </div>
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>حجز موعد متابعة</h3>
          <button onClick={handleClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>

        <div className="p-5">
          {/* Patient info */}
          {patientName && (
            <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
              <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{patientName}</div>
            </div>
          )}

          <div className="space-y-3">
            {/* Doctor */}
            {isDoctorReadOnly ? (
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الطبيب</label>
                <input
                  value={selectedDoctor?.name ?? doctors[0]?.name ?? ""}
                  className={`${inputCls()} bg-gray-50`}
                  readOnly
                />
              </div>
            ) : (
              <div>
                <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الطبيب *</label>
                <select value={doctorId} onChange={e => setDoctorId(e.target.value)} className={inputCls()}>
                  <option value="">اختر الطبيب</option>
                  {doctors.map(d => <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` (${d.specialty})` : ""}</option>)}
                </select>
              </div>
            )}

            {/* Date & Time */}
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

            {/* Service */}
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الخدمة</label>
              <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
                <option value="">اختر الخدمة</option>
                {services.map(s => <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${s.defaultPrice} ر.ي` : ""}</option>)}
              </select>
            </div>

            {/* Appointment Type */}
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>نوع الموعد</label>
              <select value={type} onChange={e => setType(e.target.value)} className={inputCls()}>
                {APPOINTMENT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>

            {/* Notes */}
            <div>
              <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
              <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-2 mt-5">
            <button onClick={handleClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
              style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
            <button onClick={handleSubmit} disabled={!doctorId || !date || !startTime || isPending}
              className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
              style={{ background: "#3d7ab5", opacity: !doctorId || !date || !startTime || isPending ? 0.5 : 1 }}>
              {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CalendarPlus className="w-4 h-4" />}
              حجز الموعد
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
