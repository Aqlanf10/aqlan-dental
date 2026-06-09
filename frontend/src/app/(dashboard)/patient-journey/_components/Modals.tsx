"use client";

import {
  X, Send, Pill, Save, CalendarDays, Plus, Trash2, Upload,
} from "lucide-react";
import { useState } from "react";
import { cn } from "@/lib/utils";
import {
  PAYMENT_METHODS, inputCls, fmtRial,
} from "@/components/shared/journey/constants";
import type { ServiceOption } from "@/components/shared/journey/types";

// ─── Confirmation Dialog ──────────────────────────────────────────────────

interface ConfirmDialogProps {
  open: boolean;
  type: "Cancelled" | "NoShow" | "CancelQueue" | "ChangeRoom";
  patientName: string;
  pendingChangeRoomName?: string;
  isPending: boolean;
  onConfirm: () => void;
  onClose: () => void;
}

export function ConfirmDialog({
  open, type, patientName, pendingChangeRoomName, isPending, onConfirm, onClose,
}: ConfirmDialogProps) {
  if (!open) return null;

  const typeLabels: Record<string, string> = {
    Cancelled: "إلغاء الموعد",
    NoShow: "تسجيل عدم الحضور",
    CancelQueue: "إلغاء الانتظار",
    ChangeRoom: "تغيير الغرفة",
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm mx-4">
        <div className="p-5 text-center">
          <h3 className="text-sm font-bold text-[#1a3a5c] mb-2">{typeLabels[type] ?? type}</h3>
          <p className="text-xs text-gray-600">
            {type === "ChangeRoom"
              ? `هل تريد تغيير الغرفة إلى ${pendingChangeRoomName ?? ""}؟`
              : type === "Cancelled"
                ? `هل تريد إلغاء موعد ${patientName}؟`
                : type === "NoShow"
                  ? `هل تريد تسجيل عدم حضور ${patientName}؟`
                  : `هل تريد إلغاء ${patientName} من الانتظار؟`}
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

// ─── Record Payment Modal ───────────────────────────────────────────────────

interface RecordPaymentModalProps {
  open: boolean;
  outstandingBalance?: number;
  paymentAmount: number;
  setPaymentAmount: (v: number) => void;
  paymentMethod: string;
  setPaymentMethod: (v: string) => void;
  paymentServiceDesc: string;
  setPaymentServiceDesc: (v: string) => void;
  paymentNotes: string;
  setPaymentNotes: (v: string) => void;
  isPending: boolean;
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
}

export function RecordPaymentModal({
  open, outstandingBalance,
  paymentAmount, setPaymentAmount,
  paymentMethod, setPaymentMethod,
  paymentServiceDesc, setPaymentServiceDesc,
  paymentNotes, setPaymentNotes,
  isPending, onSubmit, onClose,
}: RecordPaymentModalProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-sm font-bold text-[#1a3a5c]">تسجيل دفعة</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>
        <form onSubmit={onSubmit} className="p-4 space-y-3">
          {/* Outstanding balance reference */}
          {outstandingBalance != null && outstandingBalance > 0 && (
            <div className="flex items-center justify-between bg-amber-50 border border-amber-200 rounded-lg px-3 py-2">
              <span className="text-[10px] text-amber-700 font-semibold">الرصيد المستحق</span>
              <span className="text-sm font-bold text-amber-800">{fmtRial(outstandingBalance)}</span>
            </div>
          )}
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">المبلغ</label>
            <input
              type="number"
              value={paymentAmount}
              onChange={(e) => setPaymentAmount(parseInt(e.target.value) || 0)}
              className={inputCls()}
              dir="ltr"
              min={1}
              required
            />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">طريقة الدفع</label>
            <select value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value)} className={inputCls()}>
              {PAYMENT_METHODS.map((m) => (
                <option key={m.value} value={m.value}>{m.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">وصف الخدمة</label>
            <input
              value={paymentServiceDesc}
              onChange={(e) => setPaymentServiceDesc(e.target.value)}
              className={inputCls()}
              placeholder="مثل: رسوم معاينة"
            />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
            <textarea
              value={paymentNotes}
              onChange={(e) => setPaymentNotes(e.target.value)}
              className={cn(inputCls(), "h-16 resize-none")}
            />
          </div>
          <div className="flex gap-2 justify-end pt-2 border-t">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="px-4 py-2 text-xs font-bold rounded-lg bg-green-600 text-white hover:bg-green-700 transition disabled:opacity-50"
            >
              {isPending ? "جارٍ..." : "تسجيل الدفعة"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Send SMS Modal ─────────────────────────────────────────────────────────

interface SendSmsModalProps {
  open: boolean;
  smsTo: string;
  setSmsTo: (v: string) => void;
  smsMessage: string;
  setSmsMessage: (v: string) => void;
  isPending: boolean;
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
}

export function SendSmsModal({
  open, smsTo, setSmsTo, smsMessage, setSmsMessage, isPending, onSubmit, onClose,
}: SendSmsModalProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-sm font-bold text-[#1a3a5c]">إرسال رسالة SMS</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>
        <form onSubmit={onSubmit} className="p-4 space-y-3">
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">رقم الهاتف</label>
            <input
              value={smsTo}
              onChange={(e) => setSmsTo(e.target.value)}
              className={inputCls()}
              dir="ltr"
              required
            />
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الرسالة</label>
            <textarea
              value={smsMessage}
              onChange={(e) => setSmsMessage(e.target.value)}
              className={cn(inputCls(), "h-28 resize-none")}
              placeholder="اكتب الرسالة هنا..."
              required
            />
          </div>
          <div className="flex gap-2 justify-end pt-2 border-t">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Send className="w-3 h-3" />
              {isPending ? "جارٍ الإرسال..." : "إرسال"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Quick Prescription Modal ───────────────────────────────────────────────

interface PrescriptionItem {
  name: string;
  dose: string;
  frequency: string;
  duration: string;
  notes: string;
}

interface PrescriptionModalProps {
  open: boolean;
  prescDiagnosis: string;
  setPrescDiagnosis: (v: string) => void;
  prescNotes: string;
  setPrescNotes: (v: string) => void;
  prescItems: PrescriptionItem[];
  setPrescItems: (v: PrescriptionItem[]) => void;
  isPending: boolean;
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
}

export function PrescriptionModal({
  open, prescDiagnosis, setPrescDiagnosis,
  prescNotes, setPrescNotes,
  prescItems, setPrescItems,
  isPending, onSubmit, onClose,
}: PrescriptionModalProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-sm font-bold text-[#1a3a5c]">وصفة طبية</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>
        <form onSubmit={onSubmit} className="p-4 space-y-3">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
              <input
                value={prescDiagnosis}
                onChange={(e) => setPrescDiagnosis(e.target.value)}
                className={inputCls()}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
              <input
                value={prescNotes}
                onChange={(e) => setPrescNotes(e.target.value)}
                className={inputCls()}
              />
            </div>
          </div>

          {/* Medication Items */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-[#1a3a5c]">الأدوية</span>
              <button
                type="button"
                onClick={() => setPrescItems([...prescItems, { name: "", dose: "", frequency: "", duration: "", notes: "" }])}
                className="flex items-center gap-1 px-2 py-1 text-[10px] rounded-lg bg-[#3d7ab5]/10 text-[#3d7ab5] hover:bg-[#3d7ab5]/20 transition"
              >
                <Plus className="w-3 h-3" />
                إضافة دواء
              </button>
            </div>
            {prescItems.map((item, idx) => (
              <div key={idx} className="border border-gray-200 rounded-lg p-2.5 space-y-2 bg-gray-50/50">
                <div className="flex items-center justify-between">
                  <span className="text-[10px] font-bold text-gray-500">دواء {idx + 1}</span>
                  {prescItems.length > 1 && (
                    <button
                      type="button"
                      onClick={() => setPrescItems(prescItems.filter((_, i) => i !== idx))}
                      className="p-1 rounded hover:bg-red-50 transition"
                    >
                      <Trash2 className="w-3 h-3 text-red-500" />
                    </button>
                  )}
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  <div>
                    <label className="block text-[9px] font-medium text-gray-500">اسم الدواء *</label>
                    <input
                      value={item.name}
                      onChange={(e) => {
                        const updated = [...prescItems];
                        updated[idx] = { ...updated[idx], name: e.target.value };
                        setPrescItems(updated);
                      }}
                      className={inputCls()}
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-[9px] font-medium text-gray-500">الجرعة</label>
                    <input
                      value={item.dose}
                      onChange={(e) => {
                        const updated = [...prescItems];
                        updated[idx] = { ...updated[idx], dose: e.target.value };
                        setPrescItems(updated);
                      }}
                      className={inputCls()}
                      placeholder="مثل: 500mg"
                    />
                  </div>
                  <div>
                    <label className="block text-[9px] font-medium text-gray-500">التكرار</label>
                    <input
                      value={item.frequency}
                      onChange={(e) => {
                        const updated = [...prescItems];
                        updated[idx] = { ...updated[idx], frequency: e.target.value };
                        setPrescItems(updated);
                      }}
                      className={inputCls()}
                      placeholder="مثل: مرتين يومياً"
                    />
                  </div>
                  <div>
                    <label className="block text-[9px] font-medium text-gray-500">المدة</label>
                    <input
                      value={item.duration}
                      onChange={(e) => {
                        const updated = [...prescItems];
                        updated[idx] = { ...updated[idx], duration: e.target.value };
                        setPrescItems(updated);
                      }}
                      className={inputCls()}
                      placeholder="مثل: 7 أيام"
                    />
                  </div>
                  <div className="sm:col-span-2">
                    <label className="block text-[9px] font-medium text-gray-500">ملاحظات</label>
                    <input
                      value={item.notes}
                      onChange={(e) => {
                        const updated = [...prescItems];
                        updated[idx] = { ...updated[idx], notes: e.target.value };
                        setPrescItems(updated);
                      }}
                      className={inputCls()}
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div className="flex gap-2 justify-end pt-2 border-t">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-purple-600 text-white hover:bg-purple-700 transition disabled:opacity-50"
            >
              <Pill className="w-3 h-3" />
              {isPending ? "جارٍ..." : "إنشاء وصفة"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Edit Visit Notes Modal ─────────────────────────────────────────────────

interface EditVisitForm {
  chiefComplaint: string;
  clinicalNotes: string;
  treatmentDone: string;
  diagnosis: string;
  instructions: string;
  nextVisitPlan: string;
  cost: number;
}

interface EditVisitModalProps {
  open: boolean;
  form: EditVisitForm;
  setForm: (v: EditVisitForm) => void;
  isPending: boolean;
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
}

export function EditVisitModal({
  open, form, setForm, isPending, onSubmit, onClose,
}: EditVisitModalProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-sm font-bold text-[#1a3a5c]">تعديل ملاحظات الزيارة</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>
        <form onSubmit={onSubmit} className="p-4 space-y-3">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الشكوى الرئيسية</label>
              <textarea
                value={form.chiefComplaint}
                onChange={(e) => setForm({ ...form, chiefComplaint: e.target.value })}
                className={cn(inputCls(), "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات سريرية</label>
              <textarea
                value={form.clinicalNotes}
                onChange={(e) => setForm({ ...form, clinicalNotes: e.target.value })}
                className={cn(inputCls(), "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ما تم عمله</label>
              <textarea
                value={form.treatmentDone}
                onChange={(e) => setForm({ ...form, treatmentDone: e.target.value })}
                className={cn(inputCls(), "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التشخيص</label>
              <input
                value={form.diagnosis}
                onChange={(e) => setForm({ ...form, diagnosis: e.target.value })}
                className={inputCls()}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">تعليمات للمريض</label>
              <textarea
                value={form.instructions}
                onChange={(e) => setForm({ ...form, instructions: e.target.value })}
                className={cn(inputCls(), "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">خطة الزيارة القادمة</label>
              <textarea
                value={form.nextVisitPlan}
                onChange={(e) => setForm({ ...form, nextVisitPlan: e.target.value })}
                className={cn(inputCls(), "h-16 resize-none")}
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التكلفة</label>
              <input
                type="number"
                value={form.cost}
                onChange={(e) => setForm({ ...form, cost: parseInt(e.target.value) || 0 })}
                className={inputCls()}
                dir="ltr"
                min={0}
              />
            </div>
          </div>
          <div className="flex gap-2 justify-end pt-2 border-t">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Save className="w-3 h-3" />
              {isPending ? "جارٍ الحفظ..." : "حفظ التعديلات"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Book Next Appointment Modal ────────────────────────────────────────────

interface BookAppointmentModalProps {
  open: boolean;
  bookAptDate: string;
  setBookAptDate: (v: string) => void;
  bookAptStartTime: string;
  setBookAptStartTime: (v: string) => void;
  bookAptEndTime: string;
  setBookAptEndTime: (v: string) => void;
  bookAptDoctorId: string;
  doctorName: string;
  bookAptServiceId: string;
  setBookAptServiceId: (v: string) => void;
  bookAptType: string;
  setBookAptType: (v: string) => void;
  bookAptNotes: string;
  setBookAptNotes: (v: string) => void;
  services: ServiceOption[];
  isPending: boolean;
  onSubmit: (e: React.FormEvent) => void;
  onClose: () => void;
}

export function BookAppointmentModal({
  open,
  bookAptDate, setBookAptDate,
  bookAptStartTime, setBookAptStartTime,
  bookAptEndTime, setBookAptEndTime,
  doctorName,
  bookAptServiceId, setBookAptServiceId,
  bookAptType, setBookAptType,
  bookAptNotes, setBookAptNotes,
  services,
  isPending, onSubmit, onClose,
}: BookAppointmentModalProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-sm font-bold text-[#1a3a5c]">حجز موعد قادم</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>
        <form onSubmit={onSubmit} className="p-4 space-y-3">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">التاريخ *</label>
              <input
                type="date"
                value={bookAptDate}
                onChange={(e) => setBookAptDate(e.target.value)}
                className={inputCls()}
                dir="ltr"
                required
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">وقت البداية *</label>
              <input
                type="time"
                value={bookAptStartTime}
                onChange={(e) => setBookAptStartTime(e.target.value)}
                className={inputCls()}
                dir="ltr"
                required
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">وقت النهاية</label>
              <input
                type="time"
                value={bookAptEndTime}
                onChange={(e) => setBookAptEndTime(e.target.value)}
                className={inputCls()}
                dir="ltr"
              />
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الطبيب</label>
              <div className="flex items-center gap-2">
                <input
                  value={doctorName}
                  className={cn(inputCls(), "bg-gray-50")}
                  readOnly
                />
              </div>
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الخدمة</label>
              <select
                value={bookAptServiceId}
                onChange={(e) => setBookAptServiceId(e.target.value)}
                className={inputCls()}
              >
                <option value="">— اختر خدمة —</option>
                {services.map((s) => (
                  <option key={s.id} value={s.id}>{s.arabicName}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">نوع الموعد</label>
              <select
                value={bookAptType}
                onChange={(e) => setBookAptType(e.target.value)}
                className={inputCls()}
              >
                <option value="FollowUp">متابعة</option>
                <option value="Consultation">معاينة</option>
                <option value="Procedure">إجراء</option>
                <option value="Emergency">حالة إسعافية</option>
              </select>
            </div>
          </div>
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
            <textarea
              value={bookAptNotes}
              onChange={(e) => setBookAptNotes(e.target.value)}
              className={cn(inputCls(), "h-16 resize-none")}
            />
          </div>
          <div className="flex gap-2 justify-end pt-2 border-t">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#1a3a5c] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <CalendarDays className="w-3 h-3" />
              {isPending ? "جارٍ الحجز..." : "حجز الموعد"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Upload Document Modal ──────────────────────────────────────────────────

interface UploadDocumentModalProps {
  open: boolean;
  patientId: string | null;
  onClose: () => void;
  onUploaded?: () => void;
}

const ALLOWED_TYPES = [
  "image/jpeg", "image/png", "image/webp", "image/gif",
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
];
const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

export function UploadDocumentModal({
  open, patientId, onClose, onUploaded,
}: UploadDocumentModalProps) {
  const [title, setTitle] = useState("");
  const [documentType, setDocumentType] = useState("Other");
  const [notes, setNotes] = useState("");
  const [fileUrl, setFileUrl] = useState("");
  const [fileName, setFileName] = useState("");
  const [fileSize, setFileSize] = useState<number | null>(null);
  const [mimeType, setMimeType] = useState("");
  const [uploading, setUploading] = useState(false);
  const [saving, setSaving] = useState(false);

  if (!open) return null;

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!ALLOWED_TYPES.includes(file.type)) {
      alert("نوع الملف غير مدعوم. المسموح: JPG, PNG, WebP, PDF, Word");
      return;
    }
    if (file.size > MAX_FILE_SIZE) {
      alert("حجم الملف يتجاوز 10 ميجابايت");
      return;
    }

    setUploading(true);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const apiModule = await import("@/lib/api");
      const { data } = await apiModule.default.post<{
        url: string; fileName: string; originalName: string; size: number; contentType: string;
      }>("/api/uploads", formData, { headers: { "Content-Type": "multipart/form-data" } });
      setFileUrl(data.url);
      setFileName(data.originalName);
      setFileSize(data.size);
      setMimeType(data.contentType);
      if (!title) setTitle(data.originalName);
    } catch {
      alert("فشل رفع الملف");
    } finally {
      setUploading(false);
    }
  };

  const handleSave = async () => {
    if (!title.trim() || !fileUrl || !patientId) return;
    setSaving(true);
    try {
      const apiModule = await import("@/lib/api");
      await apiModule.default.post("/api/documents", {
        patientId,
        title: title.trim(),
        documentType: documentType || null,
        fileUrl,
        fileName,
        fileSize,
        mimeType,
        notes: notes || null,
      });
      // Reset & close
      setTitle(""); setDocumentType("Other"); setNotes("");
      setFileUrl(""); setFileName(""); setFileSize(null); setMimeType("");
      onUploaded?.();
      onClose();
    } catch {
      alert("فشل حفظ المستند");
    } finally {
      setSaving(false);
    }
  };

  const handleClose = () => {
    setTitle(""); setDocumentType("Other"); setNotes("");
    setFileUrl(""); setFileName(""); setFileSize(null); setMimeType("");
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[90vh] overflow-y-auto">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-sm font-bold text-[#1a3a5c]">رفع مستند</h3>
          <button onClick={handleClose} className="p-1 rounded-lg hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-500" />
          </button>
        </div>
        <div className="p-4 space-y-3">
          {/* File Upload */}
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">الملف</label>
            <input
              type="file"
              accept=".jpg,.jpeg,.png,.webp,.gif,.pdf,.doc,.docx"
              onChange={handleFileChange}
              disabled={uploading}
              className="w-full text-sm file:mr-2 file:py-1.5 file:px-3 file:rounded-lg file:border-0 file:text-xs file:font-bold file:bg-[#3d7ab5] file:text-white hover:file:bg-[#2d5e8e] file:cursor-pointer disabled:opacity-50"
            />
            {uploading && (
              <p className="text-[10px] text-[#3d7ab5] mt-1 animate-pulse">جارٍ رفع الملف...</p>
            )}
            {fileUrl && !uploading && (
              <p className="text-[10px] text-green-700 mt-1">تم رفع: {fileName}</p>
            )}
          </div>
          {/* Title */}
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">عنوان المستند *</label>
            <input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              className={inputCls()}
              placeholder="مثل: أشعة أسنان"
            />
          </div>
          {/* Document Type */}
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">نوع المستند</label>
            <select value={documentType} onChange={(e) => setDocumentType(e.target.value)} className={inputCls()}>
              <option value="Radiograph">أشعة</option>
              <option value="LabReport">تقرير مخبري</option>
              <option value="Consent">موافقة</option>
              <option value="Referral">تحويل</option>
              <option value="Prescription">وصفة</option>
              <option value="Other">أخرى</option>
            </select>
          </div>
          {/* Notes */}
          <div>
            <label className="block text-[10px] font-bold text-[#2d5e8e] mb-0.5">ملاحظات</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className={cn(inputCls(), "h-16 resize-none")}
            />
          </div>
          {/* Actions */}
          <div className="flex gap-2 justify-end pt-2 border-t">
            <button
              onClick={handleClose}
              className="px-4 py-2 text-xs font-bold rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-50 transition"
            >
              إلغاء
            </button>
            <button
              onClick={handleSave}
              disabled={saving || !fileUrl || !title.trim()}
              className="flex items-center gap-1 px-4 py-2 text-xs font-bold rounded-lg bg-[#3d7ab5] text-white hover:bg-[#2d5e8e] transition disabled:opacity-50"
            >
              <Upload className="w-3 h-3" />
              {saving ? "جارٍ الحفظ..." : "حفظ المستند"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
