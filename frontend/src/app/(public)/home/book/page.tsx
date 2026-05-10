"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { CheckCircle2, Loader2, ChevronRight, CalendarDays, MessageCircle, Phone, MapPin, Clock, AlertCircle } from "lucide-react";

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "";

const SERVICES = [
  "تقويم الأسنان",
  "طب الأسنان العام",
  "جراحة الوجه والفكين",
  "تنظيف الأسنان",
  "تجميل الأسنان",
  "علاج جذور",
  "زراعة أسنان",
  "استشارة",
];

interface TimeSlot {
  time: string;
  available: boolean;
  reason?: string;
}

interface AvailabilityResponse {
  date: string;
  serviceType: string | null;
  slots: TimeSlot[];
  isClosed: boolean;
  message?: string;
}

interface FormData {
  patientName: string;
  phoneNumber: string;
  email: string;
  serviceType: string;
  preferredDate: string;
  preferredTime: string;
  notes: string;
}

interface FormErrors {
  patientName?: string;
  phoneNumber?: string;
  email?: string;
  preferredDate?: string;
}

/** Convert "HH:mm" 24h → Arabic display like "9:00 ص" */
function toArabicTime(time24: string): string {
  const [hStr, mStr] = time24.split(":");
  let h = parseInt(hStr, 10);
  const m = mStr;
  if (h === 0) return `12:${m} ص`;
  if (h === 12) return `12:${m} م`;
  if (h < 12) return `${h}:${m} ص`;
  return `${h - 12}:${m} م`;
}

/** Get tomorrow's date in YYYY-MM-DD (Asia/Aden = UTC+3) */
function getTomorrowDate(): string {
  const now = new Date();
  now.setDate(now.getDate() + 1);
  return now.toISOString().split("T")[0];
}

/** Get today's date in YYYY-MM-DD */
function getTodayDate(): string {
  return new Date().toISOString().split("T")[0];
}

export default function BookPage() {
  const [form, setForm] = useState<FormData>({
    patientName: "",
    phoneNumber: "",
    email: "",
    serviceType: "",
    preferredDate: "",
    preferredTime: "",
    notes: "",
  });
  const [errors, setErrors] = useState<FormErrors>({});
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [serverError, setServerError] = useState("");

  // ── Availability state ──
  const [slots, setSlots] = useState<TimeSlot[]>([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotsError, setSlotsError] = useState("");
  const [isClosed, setIsClosed] = useState(false);
  const [closedMessage, setClosedMessage] = useState("");

  // Fetch availability when date or service changes
  const fetchAvailability = useCallback(async (date: string, serviceType: string) => {
    if (!date) {
      setSlots([]);
      setSlotsError("");
      setIsClosed(false);
      setClosedMessage("");
      return;
    }

    setSlotsLoading(true);
    setSlotsError("");
    setIsClosed(false);
    setClosedMessage("");

    try {
      const params = new URLSearchParams({ date });
      if (serviceType) params.set("serviceType", serviceType);

      const res = await fetch(`${API_URL}/api/public/booking-availability?${params}`);

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        setSlotsError(data?.message || "تعذّر تحميل الأوقات المتاحة");
        setSlots([]);
        setSlotsLoading(false);
        return;
      }

      const data: AvailabilityResponse = await res.json();

      if (data.isClosed) {
        setIsClosed(true);
        setClosedMessage(data.message || "");
        setSlots([]);
      } else {
        setSlots(data.slots || []);
      }
    } catch {
      setSlotsError("تعذّر الاتصال بالخادم لتحميل الأوقات");
      setSlots([]);
    } finally {
      setSlotsLoading(false);
    }
  }, []);

  // Re-fetch when date or serviceType changes (debounced)
  useEffect(() => {
    // Clear selected time when date/service changes
    setForm(prev => ({ ...prev, preferredTime: "" }));

    const timer = setTimeout(() => {
      fetchAvailability(form.preferredDate, form.serviceType);
    }, 300);

    return () => clearTimeout(timer);
  }, [form.preferredDate, form.serviceType, fetchAvailability]);

  function validate(): boolean {
    const errs: FormErrors = {};
    if (!form.patientName.trim()) errs.patientName = "الاسم مطلوب";
    if (!form.phoneNumber.trim()) errs.phoneNumber = "رقم الهاتف مطلوب";
    else if (!/^[\d\s\-\+\(\)]{7,20}$/.test(form.phoneNumber.trim())) {
      errs.phoneNumber = "رقم الهاتف غير صحيح";
    }
    if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
      errs.email = "البريد الإلكتروني غير صحيح";
    }
    // Date validation for Friday
    if (form.preferredDate) {
      const day = new Date(form.preferredDate + "T00:00:00").getDay();
      if (day === 5) {
        errs.preferredDate = "المركز مغلق يوم الجمعة، يرجى اختيار يوم آخر";
      }
    }
    setErrors(errs);
    return Object.keys(errs).length === 0;
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!validate()) return;

    setLoading(true);
    setServerError("");

    try {
      const res = await fetch(`${API_URL}/api/public/booking-requests`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          patientName: form.patientName.trim(),
          phoneNumber: form.phoneNumber.trim(),
          email: form.email.trim() || null,
          serviceType: form.serviceType || null,
          preferredDate: form.preferredDate || null,
          // Send time in 24h format so backend can check availability accurately
          preferredTime: form.preferredTime || null,
          notes: form.notes.trim() || null,
        }),
      });

      if (res.ok || res.status === 201) {
        setSuccess(true);
      } else if (res.status === 409) {
        // Slot no longer available — refresh slots and show error
        const data = await res.json().catch(() => ({}));
        setServerError(data?.message || "هذا الموعد لم يعد متاحًا، يرجى اختيار وقت آخر.");
        // Refresh availability
        fetchAvailability(form.preferredDate, form.serviceType);
      } else {
        setServerError("حدث خطأ أثناء إرسال طلبك. يرجى المحاولة مرة أخرى أو الاتصال بنا مباشرة.");
      }
    } catch {
      setServerError("تعذّر الاتصال بالخادم. تحقق من اتصالك بالإنترنت وحاول مجدداً.");
    } finally {
      setLoading(false);
    }
  }

  // ── Success screen ──
  if (success) {
    return (
      <div dir="rtl" className="min-h-[70vh] flex items-center justify-center px-4 py-20 bg-[#F8FAFC]">
        <div className="max-w-md w-full bg-white rounded-3xl shadow-xl p-10 text-center border border-slate-100">
          <div
            className="inline-block text-xs font-semibold px-3 py-1 rounded-full mb-5 border"
            style={{ backgroundColor: "rgba(135,206,235,0.1)", borderColor: "rgba(135,206,235,0.3)", color: "#0284c7" }}
          >
            تم بنجاح
          </div>

          <div
            className="w-24 h-24 rounded-full flex items-center justify-center mx-auto mb-6 shadow-lg"
            style={{ background: "linear-gradient(135deg, #87CEEB, #0284c7)" }}
          >
            <CheckCircle2 className="w-12 h-12 text-white" />
          </div>

          <h2 className="text-2xl font-extrabold text-slate-900 mb-2">تم إرسال طلبك بنجاح!</h2>
          <p className="text-slate-400 text-sm mb-4">شكراً لاختيارك مركز د. عقلان الكامل</p>
          <p className="text-slate-500 text-sm leading-relaxed mb-8">
            سيتواصل معك فريقنا على الرقم{" "}
            <strong className="font-semibold" style={{ color: "#87CEEB" }}>{form.phoneNumber}</strong>{" "}
            لتأكيد موعد <strong className="text-slate-900">{form.patientName}</strong> في أقرب وقت ممكن.
          </p>

          <div className="flex flex-col gap-3 mb-6">
            <Link
              href="/home"
              className="text-white font-bold px-6 py-3.5 rounded-xl transition-opacity hover:opacity-90 inline-flex items-center justify-center gap-2 shadow-md"
              style={{ backgroundColor: "#FF8C00" }}
            >
              العودة للرئيسية
              <ChevronRight className="w-4 h-4" />
            </Link>
            <a
              href="https://wa.me/967770245745"
              target="_blank"
              rel="noopener noreferrer"
              className="bg-green-500 hover:bg-green-600 text-white font-semibold px-6 py-3.5 rounded-xl transition-colors inline-flex items-center justify-center gap-2"
            >
              <MessageCircle className="w-4 h-4" />
              تواصل عبر واتساب
            </a>
          </div>

          <div className="flex items-center justify-center gap-1.5 text-xs text-slate-400 border-t border-slate-100 pt-5">
            <MapPin className="w-3.5 h-3.5" style={{ color: "#FF8C00" }} />
            تعز، اليمن — شارع التحرير الأعلى
            <span className="mx-1 text-slate-200">|</span>
            <Clock className="w-3.5 h-3.5" style={{ color: "#87CEEB" }} />
            السبت – الخميس: 8 ص – 8 م
          </div>
        </div>
      </div>
    );
  }

  // ── Booking form ──
  const availableSlots = slots.filter(s => s.available);
  const hasNoAvailableSlots = slots.length > 0 && availableSlots.length === 0;

  return (
    <div dir="rtl" className="min-h-screen bg-[#F8FAFC]">
      {/* Page Header */}
      <div className="relative text-white py-14 overflow-hidden" style={{ backgroundColor: "#0F172A" }}>
        <div
          className="absolute inset-0 opacity-5"
          style={{
            backgroundImage: "radial-gradient(circle, #87CEEB 1px, transparent 1px)",
            backgroundSize: "28px 28px",
          }}
        />
        <div className="relative max-w-2xl mx-auto px-4 text-center">
          <div className="flex justify-center mb-5">
            <div className="bg-white rounded-2xl p-2 shadow-sm inline-flex">
              <img src="/logo.png" alt="مركز الدكتور عقلان الكامل" className="h-10 w-auto object-contain" />
            </div>
          </div>
          <div
            className="w-14 h-14 rounded-2xl border flex items-center justify-center mx-auto mb-5"
            style={{ backgroundColor: "rgba(135,206,235,0.12)", borderColor: "rgba(135,206,235,0.2)" }}
          >
            <CalendarDays className="w-7 h-7" style={{ color: "#FF8C00" }} />
          </div>
          <h1 className="text-3xl md:text-4xl font-extrabold mb-3">احجز موعدك</h1>
          <p className="text-sm leading-relaxed max-w-lg mx-auto mb-5" style={{ color: "#94a3b8" }}>
            أرسل طلبك وسيقوم فريق الاستقبال بالتواصل معك لتأكيد الموعد.
          </p>
          <Link
            href="/home"
            className="inline-flex items-center gap-1 text-xs transition-colors hover:text-white"
            style={{ color: "rgba(135,206,235,0.6)" }}
          >
            <ChevronRight className="w-3 h-3" />
            العودة للرئيسية
          </Link>
        </div>
      </div>

      <div className="max-w-2xl mx-auto px-4 py-10">
        {/* Form card */}
        <div className="bg-white rounded-3xl shadow-xl border border-slate-100 border-t-4 border-t-[#87CEEB] overflow-hidden">
          <div className="p-8">
            <form onSubmit={handleSubmit} noValidate className="space-y-5">

              {/* ── معلومات الاتصال ── */}
              <div>
                <div className="flex items-center gap-3 mb-5">
                  <div className="flex-1 h-px bg-slate-100" />
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider whitespace-nowrap">
                    معلومات الاتصال
                  </span>
                  <div className="flex-1 h-px bg-slate-100" />
                </div>

                <div className="space-y-5">
                  {/* Name */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      الاسم الكامل <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={form.patientName}
                      onChange={(e) => setForm({ ...form, patientName: e.target.value })}
                      placeholder="أدخل اسمك الكامل"
                      className={`w-full px-4 py-3.5 rounded-xl border text-right transition-all duration-200 outline-none focus:ring-2 ${
                        errors.patientName
                          ? "border-red-400 bg-red-50 focus:ring-red-300"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.patientName && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.patientName}</p>
                    )}
                  </div>

                  {/* Phone */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      رقم الهاتف <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="tel"
                      value={form.phoneNumber}
                      onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
                      placeholder="مثال: 04-253028"
                      dir="ltr"
                      className={`w-full px-4 py-3.5 rounded-xl border text-left transition-all duration-200 outline-none focus:ring-2 ${
                        errors.phoneNumber
                          ? "border-red-400 bg-red-50 focus:ring-red-300"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.phoneNumber && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.phoneNumber}</p>
                    )}
                  </div>

                  {/* Email */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      البريد الإلكتروني{" "}
                      <span className="text-slate-400 font-normal">(اختياري)</span>
                    </label>
                    <input
                      type="email"
                      value={form.email}
                      onChange={(e) => setForm({ ...form, email: e.target.value })}
                      placeholder="example@email.com"
                      dir="ltr"
                      className={`w-full px-4 py-3.5 rounded-xl border text-left transition-all duration-200 outline-none focus:ring-2 ${
                        errors.email
                          ? "border-red-400 bg-red-50"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.email && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.email}</p>
                    )}
                  </div>
                </div>
              </div>

              {/* ── تفاصيل الموعد ── */}
              <div>
                <div className="flex items-center gap-3 mb-5 pt-2">
                  <div className="flex-1 h-px bg-slate-100" />
                  <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider whitespace-nowrap">
                    تفاصيل الموعد
                  </span>
                  <div className="flex-1 h-px bg-slate-100" />
                </div>

                <div className="space-y-5">
                  {/* Service */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      نوع الخدمة
                    </label>
                    <select
                      value={form.serviceType}
                      onChange={(e) => setForm({ ...form, serviceType: e.target.value })}
                      className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 bg-white text-right"
                    >
                      <option value="">اختر الخدمة...</option>
                      {SERVICES.map((s) => (
                        <option key={s} value={s}>{s}</option>
                      ))}
                    </select>
                  </div>

                  {/* Preferred Date */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      التاريخ المفضل
                    </label>
                    <input
                      type="date"
                      value={form.preferredDate}
                      onChange={(e) => setForm({ ...form, preferredDate: e.target.value, preferredTime: "" })}
                      min={getTodayDate()}
                      className={`w-full px-4 py-3.5 rounded-xl border transition-all duration-200 outline-none focus:ring-2 ${
                        errors.preferredDate
                          ? "border-red-400 bg-red-50 focus:ring-red-300"
                          : "border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] focus:ring-[#87CEEB]/20"
                      }`}
                    />
                    {errors.preferredDate && (
                      <p className="text-red-500 text-xs mt-1.5">{errors.preferredDate}</p>
                    )}
                  </div>

                  {/* ── Dynamic Time Slot Selector ── */}
                  {form.preferredDate && !errors.preferredDate && (
                    <div>
                      <label className="block text-sm font-semibold text-slate-700 mb-3">
                        الوقت المفضل
                      </label>

                      {/* Loading state */}
                      {slotsLoading && (
                        <div className="flex items-center justify-center py-8 gap-2 text-slate-400">
                          <Loader2 className="w-5 h-5 animate-spin" />
                          <span className="text-sm">جاري تحميل الأوقات المتاحة...</span>
                        </div>
                      )}

                      {/* Error loading slots */}
                      {slotsError && !slotsLoading && (
                        <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-amber-700 text-sm text-center flex items-center justify-center gap-2">
                          <AlertCircle className="w-4 h-4 shrink-0" />
                          {slotsError}
                        </div>
                      )}

                      {/* Friday / Closed message */}
                      {isClosed && !slotsLoading && (
                        <div className="bg-amber-50 border border-amber-200 rounded-xl p-4 text-amber-700 text-sm text-center">
                          {closedMessage || "المركز مغلق يوم الجمعة، يرجى اختيار يوم آخر."}
                        </div>
                      )}

                      {/* No available slots */}
                      {hasNoAvailableSlots && !slotsLoading && !isClosed && !slotsError && (
                        <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 text-slate-500 text-sm text-center">
                          لا توجد أوقات متاحة في هذا اليوم، يرجى اختيار يوم آخر.
                        </div>
                      )}

                      {/* Time slot grid */}
                      {slots.length > 0 && !slotsLoading && !isClosed && !slotsError && (
                        <div className="grid grid-cols-4 sm:grid-cols-5 gap-2">
                          {slots.map((slot) => {
                            const isSelected = form.preferredTime === slot.time;
                            const isUnavailable = !slot.available;

                            return (
                              <button
                                key={slot.time}
                                type="button"
                                disabled={isUnavailable}
                                onClick={() => setForm({ ...form, preferredTime: slot.time })}
                                title={isUnavailable ? slot.reason || "محجوز" : toArabicTime(slot.time)}
                                className={`
                                  relative px-2 py-2.5 rounded-xl text-sm font-medium transition-all duration-150 border
                                  ${isUnavailable
                                    ? "bg-slate-50 border-slate-100 text-slate-300 cursor-not-allowed line-through"
                                    : isSelected
                                      ? "bg-orange-500 border-orange-500 text-white shadow-md scale-[1.02]"
                                      : "bg-white border-slate-200 text-slate-700 hover:border-[#87CEEB] hover:bg-sky-50 cursor-pointer"
                                  }
                                `}
                              >
                                <span className="block text-center" dir="ltr">
                                  {toArabicTime(slot.time)}
                                </span>
                                {isUnavailable && (
                                  <span className="absolute -top-1 -left-1 w-4 h-4 bg-red-400 rounded-full flex items-center justify-center">
                                    <span className="text-white text-[8px] font-bold">✕</span>
                                  </span>
                                )}
                              </button>
                            );
                          })}
                        </div>
                      )}

                      {/* Selected time indicator */}
                      {form.preferredTime && !slotsLoading && (
                        <p className="mt-2 text-xs text-slate-400 text-center">
                          الوقت المختار: <strong className="text-slate-700" dir="ltr">{toArabicTime(form.preferredTime)}</strong>
                        </p>
                      )}
                    </div>
                  )}
                  {/* Hint if no date selected yet */}
                  {!form.preferredDate && (
                    <p className="text-xs text-slate-400 text-center py-2">
                      اختر التاريخ أولاً لعرض الأوقات المتاحة
                    </p>
                  )}

                  {/* Notes */}
                  <div>
                    <label className="block text-sm font-semibold text-slate-700 mb-1.5">
                      ملاحظات إضافية{" "}
                      <span className="text-slate-400 font-normal">(اختياري)</span>
                    </label>
                    <textarea
                      value={form.notes}
                      onChange={(e) => setForm({ ...form, notes: e.target.value })}
                      rows={3}
                      placeholder="أي معلومات إضافية تود إضافتها..."
                      className="w-full px-4 py-3.5 rounded-xl border border-slate-200 hover:border-slate-300 focus:border-[#87CEEB] outline-none focus:ring-2 focus:ring-[#87CEEB]/20 transition-all duration-200 resize-none text-right"
                      maxLength={500}
                    />
                  </div>
                </div>
              </div>

              {serverError && (
                <div className="bg-red-50 border border-red-200 rounded-xl p-4 text-red-600 text-sm text-center flex items-center justify-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  {serverError}
                </div>
              )}

              {/* Submit button */}
              <button
                type="submit"
                disabled={loading}
                className="w-full disabled:opacity-60 text-white font-bold py-4 rounded-xl text-lg transition-opacity hover:opacity-90 flex items-center justify-center gap-2 shadow-md mt-2"
                style={{ backgroundColor: "#FF8C00" }}
              >
                {loading ? (
                  <>
                    <Loader2 className="w-5 h-5 animate-spin" />
                    جاري الإرسال...
                  </>
                ) : (
                  <>
                    إرسال طلب الحجز
                    <ChevronRight className="w-5 h-5" />
                  </>
                )}
              </button>

              <p className="text-center text-xs text-slate-400">
                بإرسال هذا النموذج أنت توافق على التواصل معك لتأكيد موعدك
              </p>
            </form>
          </div>
        </div>

        {/* Contact help box */}
        <div className="mt-6 bg-white rounded-2xl shadow-sm border border-slate-100 overflow-hidden">
          <div className="px-6 pt-6 pb-4 text-center border-b border-slate-100">
            <p className="text-slate-900 font-bold text-base">تفضل بالتواصل المباشر معنا</p>
            <p className="text-slate-400 text-xs mt-1">نرد عليك خلال ساعات العمل: السبت – الخميس 8 ص – 8 م</p>
          </div>
          <div className="p-5 grid grid-cols-2 gap-3">
            <a
              href="tel:04253028"
              className="text-white font-semibold px-4 py-3 rounded-xl text-sm transition-opacity hover:opacity-90 flex items-center justify-center gap-2"
              style={{ backgroundColor: "#87CEEB" }}
            >
              <Phone className="w-4 h-4" />
              04-253028
            </a>
            <a
              href="https://wa.me/967770245745"
              target="_blank"
              rel="noopener noreferrer"
              className="bg-green-500 hover:bg-green-600 text-white font-semibold px-4 py-3 rounded-xl text-sm transition-colors flex items-center justify-center gap-2"
            >
              <MessageCircle className="w-4 h-4" />
              واتساب
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}
