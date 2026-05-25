/**
 * All modal components for Daily Operations page.
 * QuickPayment, CompleteVisit, BookAppointment, ConfirmDialog, WhatsAppMenu.
 */

"use client";

import { useState, useCallback } from "react";
import {
  X, CreditCard, CheckCircle, CalendarPlus, AlertTriangle,
  MessageCircle, ChevronDown, Loader2, FileText, Send,
} from "lucide-react";
import {
  PAYMENT_METHODS, APPOINTMENT_TYPES, inputCls, fmtRial,
  WHATSAPP_TEMPLATES, normalizePhone,
} from "../_lib/constants";
import type { TodayJourneyItem, RoomOption, DoctorOption, ServiceOption } from "../_lib/constants";
import type { DailyJourneySummary } from "@/types/journey";

/* ═══════════════════════════════════════════════════════════════════════════
   Shared overlay wrapper
   ═══════════════════════════════════════════════════════════════════════════ */
function ModalShell({ open, onClose, title, icon: Icon, iconColor, children, wide }: {
  open: boolean; onClose: () => void; title: string;
  icon?: React.ElementType; iconColor?: string; children: React.ReactNode; wide?: boolean;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex items-center justify-center p-4" onClick={onClose}>
      <div
        className={`bg-white rounded-2xl shadow-xl ${wide ? "w-full max-w-2xl" : "w-full max-w-md"} max-h-[90vh] overflow-y-auto`}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[#e8f0f9]">
          {Icon && (
            <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ background: (iconColor ?? "#3d7ab5") + "15" }}>
              <Icon className="w-4.5 h-4.5" style={{ color: iconColor ?? "#3d7ab5" }} />
            </div>
          )}
          <h3 className="flex-1 font-extrabold text-[15px]" style={{ color: "#1a3a5c" }}>{title}</h3>
          <button onClick={onClose} className="w-8 h-8 rounded-lg flex items-center justify-center hover:bg-gray-100 transition">
            <X className="w-4 h-4 text-gray-400" />
          </button>
        </div>
        {/* Body */}
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Quick Payment Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function QuickPaymentModal({
  open, onClose, item, summary, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  isPending: boolean;
  onConfirm: (amount: number, method: string, desc: string, notes: string) => void;
}) {
  const [amount, setAmount] = useState("");
  const [method, setMethod] = useState("Cash");
  const [desc, setDesc] = useState("");
  const [notes, setNotes] = useState("");

  const outstanding = summary?.financeSummary?.outstandingBalance ?? 0;
  const overdue = summary?.financeSummary?.overdueAmount ?? 0;
  const totalPaid = summary?.financeSummary?.totalPaid;
  const latestPayment = summary?.financeSummary?.latestPayment;

  const handleSubmit = () => {
    const num = parseFloat(amount);
    if (!num || num <= 0) return;
    onConfirm(num, method, desc, notes);
    setAmount(""); setMethod("Cash"); setDesc(""); setNotes("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="دفع سريع" icon={CreditCard} iconColor="#22c55e">
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{item?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>ملف: {item?.patientId?.slice(0, 8)}…</div>
      </div>

      {/* Finance summary */}
      {summary?.financeSummary && (
        <div className="grid grid-cols-2 gap-2 mb-4">
          <div className="p-2.5 rounded-lg" style={{ background: "#fff7ed" }}>
            <div className="text-[11px] font-medium" style={{ color: "#f5922e" }}>المستحق</div>
            <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(outstanding)}</div>
          </div>
          <div className="p-2.5 rounded-lg" style={{ background: overdue > 0 ? "#fef2f2" : "#f0fdf4" }}>
            <div className="text-[11px] font-medium" style={{ color: overdue > 0 ? "#ef4444" : "#16a34a" }}>متأخرات</div>
            <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(overdue)}</div>
          </div>
          {totalPaid != null && (
            <div className="p-2.5 rounded-lg" style={{ background: "#f0fdf4" }}>
              <div className="text-[11px] font-medium" style={{ color: "#16a34a" }}>المدفوع</div>
              <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(totalPaid)}</div>
            </div>
          )}
          {latestPayment && (
            <div className="p-2.5 rounded-lg" style={{ background: "#f5f5f5" }}>
              <div className="text-[11px] font-medium" style={{ color: "#64748b" }}>آخر دفعة</div>
              <div className="text-sm font-bold" style={{ color: "#1a3a5c" }}>{fmtRial(latestPayment.amount)}</div>
            </div>
          )}
        </div>
      )}

      {/* Form */}
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>المبلغ *</label>
          <div className="flex gap-2">
            <input type="number" value={amount} onChange={e => setAmount(e.target.value)}
              placeholder="0" className={inputCls()} min={0} step={0.01} />
            {outstanding > 0 && (
              <button onClick={() => setAmount(String(outstanding))}
                className="px-3 rounded-lg text-xs font-semibold whitespace-nowrap"
                style={{ background: "#f5922e15", color: "#f5922e", border: "1px solid #f5922e30" }}>
                الكل ({fmtRial(outstanding)})
              </button>
            )}
          </div>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>طريقة الدفع</label>
          <select value={method} onChange={e => setMethod(e.target.value)} className={inputCls()}>
            {PAYMENT_METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
          </select>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>وصف الخدمة</label>
          <input value={desc} onChange={e => setDesc(e.target.value)} placeholder="مثال: استشارة + أشعة" className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
          <input value={notes} onChange={e => setNotes(e.target.value)} placeholder="اختياري" className={inputCls()} />
        </div>
      </div>

      {/* Actions */}
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={!amount || isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#22c55a", opacity: !amount || isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
          تسجيل الدفع
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Complete Visit Modal
   ═══════════════════════════════════════════════════════════════════════════ */
export function CompleteVisitModal({
  open, onClose, item, summary, isPending, onConfirm, onCheckout,
}: {
  open: boolean; onClose: () => void;
  item: TodayJourneyItem | null;
  summary: DailyJourneySummary | null;
  isPending: boolean;
  onConfirm: (data: {
    serviceDesc: string; amountDue: number; isPaid: boolean;
    needsFollowUp: boolean; nextDate: string; notes: string;
  }) => void;
  onCheckout: (data: { paymentAmount: number; paymentMethod: string; notes: string; nextDate?: string; nextServiceId?: string }) => void;
}) {
  const [serviceDesc, setServiceDesc] = useState("");
  const [amountDue, setAmountDue] = useState("");
  const [isPaid, setIsPaid] = useState(false);
  const [needsFollowUp, setNeedsFollowUp] = useState(false);
  const [nextDate, setNextDate] = useState("");
  const [notes, setNotes] = useState("");

  const handleSubmit = () => {
    const num = parseFloat(amountDue) || 0;
    if (item?.checkoutStatus === "ReadyForCheckout" || item?.nextAction === "Checkout") {
      onCheckout({
        paymentAmount: isPaid ? num : 0,
        paymentMethod: "Cash",
        notes,
        nextDate: needsFollowUp ? nextDate : undefined,
      });
    } else {
      onConfirm({ serviceDesc, amountDue: num, isPaid, needsFollowUp, nextDate, notes });
    }
    setServiceDesc(""); setAmountDue(""); setIsPaid(false); setNeedsFollowUp(false); setNextDate(""); setNotes("");
  };

  return (
    <ModalShell open={open} onClose={onClose} title="إنهاء الزيارة" icon={CheckCircle} iconColor="#16a34a">
      {/* Patient info */}
      <div className="mb-4 p-3 rounded-xl" style={{ background: "#f0f5fb" }}>
        <div className="font-bold text-sm" style={{ color: "#1a3a5c" }}>{item?.patientName}</div>
        <div className="text-xs mt-0.5" style={{ color: "#64748b" }}>الطبيب: {item?.doctorName}</div>
      </div>

      {/* Finance info */}
      {summary?.financeSummary && (
        <div className="mb-4 p-3 rounded-lg" style={{ background: "#fff7ed" }}>
          <div className="flex justify-between items-center">
            <span className="text-xs font-medium" style={{ color: "#f5922e" }}>المتبقي</span>
            <span className="text-sm font-bold" style={{ color: "#1a3a5c" }}>
              {fmtRial(summary.financeSummary.outstandingBalance)}
            </span>
          </div>
        </div>
      )}

      {/* Form */}
      <div className="space-y-3">
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملخص الإجراء / الخدمة</label>
          <input value={serviceDesc} onChange={e => setServiceDesc(e.target.value)}
            placeholder="مثال: حشو + تنظيف" className={inputCls()} />
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>المبلغ المستحق</label>
          <input type="number" value={amountDue} onChange={e => setAmountDue(e.target.value)}
            placeholder="0" className={inputCls()} min={0} step={0.01} />
        </div>
        <div className="flex items-center gap-2">
          <input type="checkbox" id="isPaid" checked={isPaid} onChange={e => setIsPaid(e.target.checked)}
            className="w-4 h-4 rounded border-gray-300" />
          <label htmlFor="isPaid" className="text-sm font-medium" style={{ color: "#1a3a5c" }}>تم الدفع</label>
        </div>
        <div className="flex items-center gap-2">
          <input type="checkbox" id="needsFollowUp" checked={needsFollowUp} onChange={e => setNeedsFollowUp(e.target.checked)}
            className="w-4 h-4 rounded border-gray-300" />
          <label htmlFor="needsFollowUp" className="text-sm font-medium" style={{ color: "#1a3a5c" }}>يحتاج موعد متابعة</label>
        </div>
        {needsFollowUp && (
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>تاريخ الموعد القادم</label>
            <input type="date" value={nextDate} onChange={e => setNextDate(e.target.value)} className={inputCls()} />
          </div>
        )}
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>ملاحظات</label>
          <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={2}
            placeholder="ملاحظة للطبيب/الاستقبال" className={inputCls()} />
        </div>
      </div>

      {/* Actions */}
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>إلغاء</button>
        <button onClick={handleSubmit} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: "#16a34a", opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
          إنهاء الزيارة
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   Book Appointment Modal
   ═══════════════════════════════════════════════════════════════════════════ */
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
            {doctors.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>التاريخ *</label>
            <input type="date" value={date} onChange={e => setDate(e.target.value)} className={inputCls()} />
          </div>
          <div>
            <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الوقت *</label>
            <input type="time" value={startTime} onChange={e => setStartTime(e.target.value)} className={inputCls()} />
          </div>
        </div>
        <div>
          <label className="text-xs font-semibold block mb-1" style={{ color: "#1a3a5c" }}>الخدمة</label>
          <select value={serviceId} onChange={e => setServiceId(e.target.value)} className={inputCls()}>
            <option value="">اختر الخدمة</option>
            {services.map(s => <option key={s.id} value={s.id}>{s.arabicName}</option>)}
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

/* ═══════════════════════════════════════════════════════════════════════════
   Confirm Dialog
   ═══════════════════════════════════════════════════════════════════════════ */
export function ConfirmDialog({
  open, onClose, type, patientName, isPending, onConfirm,
}: {
  open: boolean; onClose: () => void;
  type: "Cancel" | "NoShow" | "CancelQueue" | "ChangeRoom" | "Complete";
  patientName: string;
  isPending: boolean;
  onConfirm: () => void;
}) {
  const configs: Record<string, { title: string; body: string; btnColor: string }> = {
    Cancel:      { title: "إلغاء الموعد",     body: `هل أنت متأكد من إلغاء موعد المريض ${patientName}؟`, btnColor: "#ef4444" },
    NoShow:      { title: "لم يحضر",          body: `هل تريد تسجيل المريض ${patientName} كـ "لم يحضر"؟`, btnColor: "#f5922e" },
    CancelQueue: { title: "إلغاء من الطابور", body: `هل أنت متأكد من إلغاء المريض ${patientName} من الطابور؟`, btnColor: "#ef4444" },
    ChangeRoom:  { title: "تغيير الغرفة",     body: `هل تريد تغيير غرفة المريض ${patientName}؟`, btnColor: "#3d7ab5" },
    Complete:    { title: "إنهاء الزيارة",     body: `هل تريد إنهاء زيارة المريض ${patientName}؟`, btnColor: "#16a34a" },
  };

  const cfg = configs[type] ?? configs.Cancel;

  return (
    <ModalShell open={open} onClose={onClose} title={cfg.title} icon={AlertTriangle} iconColor={cfg.btnColor}>
      <p className="text-sm leading-relaxed" style={{ color: "#475569" }}>{cfg.body}</p>
      <div className="flex gap-2 mt-5">
        <button onClick={onClose} className="flex-1 py-2.5 rounded-xl text-sm font-bold"
          style={{ background: "#f1f5f9", color: "#64748b" }}>تراجع</button>
        <button onClick={onConfirm} disabled={isPending}
          className="flex-1 py-2.5 rounded-xl text-sm font-bold text-white flex items-center justify-center gap-2"
          style={{ background: cfg.btnColor, opacity: isPending ? 0.5 : 1 }}>
          {isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
          تأكيد
        </button>
      </div>
    </ModalShell>
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   WhatsApp Menu
   ═══════════════════════════════════════════════════════════════════════════ */
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
  const aptDate = item.appointmentTime ? `اليوم ${item.appointmentTime}` : "اليوم";
  const aptTime = item.appointmentTime;
  const remaining = summary?.financeSummary?.outstandingBalance;

  const handleSend = (template: typeof WHATSAPP_TEMPLATES[number]) => {
    const msg = template.build({ patientName, clinicName, aptDate, aptTime, doctorName, remaining });
    window.open(`https://wa.me/${phone}?text=${encodeURIComponent(msg)}`, "_blank");
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
            <p className="text-xs" style={{ color: "#64748b" }}>{patientName}</p>
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
      </div>
    </div>
  );
}
