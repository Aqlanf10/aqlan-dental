/**
 * WalkInModal — register a walk-in patient (creates patient + appointment +
 * arrival + queue entry).
 *
 * Extracted from `_components/Modals.tsx` (CLEANUP-1). No behavior changes —
 * pure file extraction. Arabic RTL preserved.
 */

"use client";

import { useState } from "react";
import { Loader2, UserPlus, AlertCircle } from "lucide-react";
import { inputCls, NAVY, ORANGE } from "../../_lib/constants";
import type { DoctorOption, ServiceOption, BranchOption } from "../../_lib/constants";
import { ModalShell } from "./ModalShell";

export function WalkInModal({
  open, onClose, doctors, services, branches, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  doctors: DoctorOption[];
  services: ServiceOption[];
  branches: BranchOption[];
  isPending: boolean;
  onConfirm: (data: {
    patientName: string; patientPhone: string; doctorId: string;
    serviceId: string; branchId: string; notes: string;
  }) => void;
}) {
  const [patientName, setPatientName] = useState("");
  const [patientPhone, setPatientPhone] = useState("");
  const [doctorId, setDoctorId] = useState("");
  const [serviceId, setServiceId] = useState("");
  const [branchId, setBranchId] = useState("");
  const [notes, setNotes] = useState("");

  const handleSubmit = () => {
    if (!patientName.trim() || !doctorId) return;
    onConfirm({
      patientName: patientName.trim(),
      patientPhone: patientPhone.trim(),
      doctorId,
      serviceId,
      branchId,
      notes: notes.trim(),
    });
    setPatientName(""); setPatientPhone(""); setDoctorId("");
    setServiceId(""); setBranchId(""); setNotes("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="مريض مشي (Walk-in)" icon={UserPlus} iconColor={ORANGE}>
      <div className="mb-3 p-2.5 rounded-lg flex items-center gap-2" style={{ background: "#fff7ed" }}>
        <AlertCircle className="w-4 h-4 flex-shrink-0" style={{ color: ORANGE }} />
        <span className="text-xs font-medium" style={{ color: "#92400e" }}>
          سيتم إنشاء مريض + موعد + تسجيل وصول + إضافة لقائمة الانتظار تلقائياً
        </span>
      </div>

      <div className="space-y-3">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>اسم المريض *</label>
            <input value={patientName} onChange={e => setPatientName(e.target.value)}
              placeholder="الاسم الكامل" className={inputCls()} />
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>رقم الهاتف</label>
            <input value={patientPhone} onChange={e => setPatientPhone(e.target.value)}
              placeholder="7XXXXXXXX" className={inputCls()} dir="ltr" />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الطبيب *</label>
            <select value={doctorId} onChange={e => setDoctorId(e.target.value)} className={inputCls()}>
              <option value="">اختر الطبيب</option>
              {doctors.map(d => <option key={d.id} value={d.id}>{d.name}{d.specialty ? ` (${d.specialty})` : ""}</option>)}
            </select>
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الخدمة</label>
            <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
              <option value="">اختر الخدمة</option>
              {services.map(s => <option key={s.id} value={s.id}>{s.arabicName}{s.defaultPrice ? ` — ${s.defaultPrice} ر.ي` : ""}</option>)}
            </select>
          </div>
        </div>
        {branches.length > 1 && (
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>الفرع</label>
            <select value={branchId} onChange={e => setBranchId(e.target.value)} className={inputCls()}>
              <option value="">الفرع الرئيسي</option>
              {branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
            </select>
          </div>
        )}
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: NAVY }}>ملاحظات</label>
          <input value={notes} onChange={e => setNotes(e.target.value)}
            placeholder="الشكوى الرئيسية..." className={inputCls()} />
        </div>
      </div>

      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!patientName.trim() || !doctorId || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: ORANGE, opacity: !patientName.trim() || !doctorId || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserPlus className="w-4 h-4" />}
          تسجيل وإضافة لقائمة الانتظار
        </button>
      </div>
    </ModalShell>
  );
}
